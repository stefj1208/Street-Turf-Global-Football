from __future__ import annotations

import base64
import ipaddress
import os
import socket
from io import BytesIO
from typing import Literal
from urllib.parse import urlparse

import httpx
from fastapi import FastAPI, File, HTTPException, UploadFile, status
from fastapi.concurrency import run_in_threadpool
from PIL import Image, ImageOps, UnidentifiedImageError
from pydantic import BaseModel, ConfigDict, Field


MAX_IMAGE_BYTES = int(os.getenv("STREET_TURF_VISION_MAX_BYTES", str(8 * 1024 * 1024)))
OPENAI_MODEL = os.getenv("STREET_TURF_VISION_MODEL", "gpt-5.6")
VISION_MODE = os.getenv("STREET_TURF_VISION_MODE", "demo").strip().lower()
ALLOWED_IMAGE_TYPES = {"image/jpeg", "image/png", "image/webp"}
FORBIDDEN_GOOGLE_SUFFIXES = (
    "google.com",
    "googleapis.com",
    "googleusercontent.com",
    "gstatic.com",
    "ggpht.com",
)


def to_camel(value: str) -> str:
    first, *remaining = value.split("_")
    return first + "".join(part.capitalize() for part in remaining)


class ApiModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        extra="forbid",
        str_strip_whitespace=True,
    )


class RoadBiome(ApiModel):
    material_id: Literal[
        "asphalt_worn",
        "asphalt_clean",
        "concrete",
        "cobblestone",
        "gray_pavers",
    ]
    base_color: str = Field(pattern=r"^#[0-9A-Fa-f]{6}$")
    roughness: float = Field(ge=0, le=1)
    friction: float = Field(ge=0.1, le=1)
    bounce: float = Field(ge=0, le=1)


class SidewalkBiome(ApiModel):
    material_id: Literal["gray_pavers", "concrete", "stone", "red_pavers"]
    base_color: str = Field(pattern=r"^#[0-9A-Fa-f]{6}$")
    curb_height_meters: float = Field(ge=0.05, le=0.35)


class BuildingBiome(ApiModel):
    material_id: Literal[
        "red_brick",
        "light_stucco",
        "dark_stucco",
        "concrete",
        "stone",
    ]
    style: Literal[
        "industrial",
        "historic_european",
        "modern",
        "residential",
        "mixed",
    ]
    base_color: str = Field(pattern=r"^#[0-9A-Fa-f]{6}$")
    estimated_floors: int = Field(ge=1, le=20)


class LightingBiome(ApiModel):
    preset: Literal["day_clear", "day_overcast", "sunset", "night", "rain"]
    sun_intensity: float = Field(ge=0, le=2)
    fog_density: float = Field(ge=0, le=0.1)


class StreetBiome(ApiModel):
    schema_version: Literal[1] = 1
    road: RoadBiome
    sidewalk: SidewalkBiome
    buildings: BuildingBiome
    lighting: LightingBiome
    confidence: float = Field(ge=0, le=1)
    notes: list[str] = Field(default_factory=list, max_length=6)


class AnalyzeUrlRequest(ApiModel):
    image_url: str = Field(min_length=12, max_length=2048)


class HealthResponse(ApiModel):
    status: Literal["ok"] = "ok"
    mode: str
    model: str


app = FastAPI(
    title="Street Turf Vision API",
    version="0.1.0",
    description=(
        "Extracts a constrained material biome from a player-owned or licensed street image. "
        "Google Street View URLs are deliberately rejected."
    ),
)


def demo_biome() -> StreetBiome:
    return StreetBiome(
        road=RoadBiome(
            material_id="asphalt_worn",
            base_color="#4A4B4D",
            roughness=0.84,
            friction=0.62,
            bounce=0.36,
        ),
        sidewalk=SidewalkBiome(
            material_id="gray_pavers",
            base_color="#888A8D",
            curb_height_meters=0.14,
        ),
        buildings=BuildingBiome(
            material_id="red_brick",
            style="industrial",
            base_color="#87483F",
            estimated_floors=3,
        ),
        lighting=LightingBiome(
            preset="day_overcast",
            sun_intensity=0.75,
            fog_density=0.008,
        ),
        confidence=0.88,
        notes=[
            "PoC simulated biome",
            "Use licensed captures before enabling OpenAI mode",
        ],
    )


def _hostname_is_forbidden(hostname: str) -> bool:
    normalized = hostname.rstrip(".").lower()
    if "google" in normalized.split("."):
        return True
    return any(
        normalized == suffix or normalized.endswith(f".{suffix}")
        for suffix in FORBIDDEN_GOOGLE_SUFFIXES
    )


def _validate_public_https_url(raw_url: str) -> str:
    parsed = urlparse(raw_url)
    if parsed.scheme != "https" or not parsed.hostname:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="imageUrl must be a public HTTPS URL",
        )
    try:
        port = parsed.port
    except ValueError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="imageUrl contains an invalid port",
        ) from error
    if parsed.username or parsed.password or port not in {None, 443}:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="imageUrl cannot contain credentials or a custom port",
        )
    if _hostname_is_forbidden(parsed.hostname):
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=(
                "Google Maps and Street View images cannot be analyzed or stored by this API. "
                "Upload a player-owned capture or a licensed image."
            ),
        )

    try:
        address_records = socket.getaddrinfo(parsed.hostname, 443, type=socket.SOCK_STREAM)
    except socket.gaierror as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="imageUrl hostname cannot be resolved",
        ) from error

    for record in address_records:
        address = ipaddress.ip_address(record[4][0])
        if not address.is_global:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail="imageUrl must resolve only to public internet addresses",
            )
    return raw_url


async def _download_image(raw_url: str) -> tuple[bytes, str]:
    url = _validate_public_https_url(raw_url)
    timeout = httpx.Timeout(12.0, connect=5.0)
    async with httpx.AsyncClient(timeout=timeout, follow_redirects=False) as client:
        async with client.stream(
            "GET",
            url,
            headers={"Accept": "image/jpeg,image/png,image/webp"},
        ) as response:
            if response.status_code != 200:
                raise HTTPException(
                    status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                    detail=f"image server returned HTTP {response.status_code}",
                )

            content_type = response.headers.get("content-type", "").split(";", 1)[0].lower()
            if content_type not in ALLOWED_IMAGE_TYPES:
                raise HTTPException(
                    status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
                    detail="remote resource is not a supported image",
                )

            try:
                declared_size = int(response.headers.get("content-length", "0") or "0")
            except ValueError as error:
                raise HTTPException(
                    status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                    detail="image server returned an invalid content length",
                ) from error
            if declared_size > MAX_IMAGE_BYTES:
                raise HTTPException(
                    status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                    detail="remote image is too large",
                )

            chunks: list[bytes] = []
            received = 0
            async for chunk in response.aiter_bytes():
                received += len(chunk)
                if received > MAX_IMAGE_BYTES:
                    raise HTTPException(
                        status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                        detail="remote image is too large",
                    )
                chunks.append(chunk)
    return b"".join(chunks), content_type


def _canonicalize_image(image_bytes: bytes) -> bytes:
    try:
        with Image.open(BytesIO(image_bytes)) as source:
            image = ImageOps.exif_transpose(source).convert("RGB")
            image.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
            output = BytesIO()
            image.save(output, format="JPEG", quality=88, optimize=True)
            return output.getvalue()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="uploaded data is not a valid image",
        ) from error


def _analyze_with_openai(canonical_jpeg: bytes) -> StreetBiome:
    if not os.getenv("OPENAI_API_KEY"):
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="OPENAI_API_KEY is required when STREET_TURF_VISION_MODE=openai",
        )

    try:
        from openai import OpenAI
    except ImportError as error:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Install the backend dependencies before enabling OpenAI mode",
        ) from error

    encoded = base64.b64encode(canonical_jpeg).decode("ascii")
    data_url = f"data:image/jpeg;base64,{encoded}"
    client = OpenAI()
    response = client.responses.parse(
        model=OPENAI_MODEL,
        input=[
            {
                "role": "system",
                "content": (
                    "You analyze licensed street reference images for a mobile football game. "
                    "Describe only reusable material and lighting characteristics. Do not identify "
                    "people, license plates, addresses, businesses, or exact locations. Choose only "
                    "values allowed by the provided schema."
                ),
            },
            {
                "role": "user",
                "content": [
                    {
                        "type": "input_text",
                        "text": (
                            "Extract one conservative Street Turf biome. Ignore vehicles, people, "
                            "signs, temporary objects and readable text. Return the dominant road, "
                            "sidewalk, building facade and lighting style."
                        ),
                    },
                    {
                        "type": "input_image",
                        "image_url": data_url,
                        "detail": "high",
                    },
                ],
            },
        ],
        text_format=StreetBiome,
    )
    if response.output_parsed is None:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="vision model did not return a valid biome",
        )
    return response.output_parsed


async def _analyze(image_bytes: bytes) -> StreetBiome:
    canonical = await run_in_threadpool(_canonicalize_image, image_bytes)
    if VISION_MODE == "demo":
        return demo_biome()
    if VISION_MODE != "openai":
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="STREET_TURF_VISION_MODE must be demo or openai",
        )
    return await run_in_threadpool(_analyze_with_openai, canonical)


@app.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    return HealthResponse(mode=VISION_MODE, model=OPENAI_MODEL)


@app.get("/v1/vision/demo-biome", response_model=StreetBiome)
async def get_demo_biome() -> StreetBiome:
    return demo_biome()


@app.post("/v1/vision/analyze-url", response_model=StreetBiome)
async def analyze_url(request: AnalyzeUrlRequest) -> StreetBiome:
    image_bytes, _ = await _download_image(request.image_url)
    return await _analyze(image_bytes)


@app.post("/v1/vision/analyze-upload", response_model=StreetBiome)
async def analyze_upload(image: UploadFile = File(...)) -> StreetBiome:
    content_type = (image.content_type or "").split(";", 1)[0].lower()
    if content_type not in ALLOWED_IMAGE_TYPES:
        await image.close()
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail="upload must be JPEG, PNG or WebP",
        )

    try:
        image_bytes = await image.read(MAX_IMAGE_BYTES + 1)
    finally:
        await image.close()
    if len(image_bytes) > MAX_IMAGE_BYTES:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="uploaded image is too large",
        )
    return await _analyze(image_bytes)
