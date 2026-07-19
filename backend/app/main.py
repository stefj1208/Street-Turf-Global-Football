from __future__ import annotations

from io import BytesIO

from fastapi import FastAPI, File, Form, HTTPException, UploadFile, status
from fastapi.concurrency import run_in_threadpool
from fastapi.responses import FileResponse, StreamingResponse
from pydantic import ValidationError

from app.config import Settings
from app.image_processing import (
    ImageValidationError,
    bytes_sha256,
    decode_image,
    encode_png,
)
from app.inpainting import InpaintingService, create_provider
from app.repository import LocalTurfRepository, TurfNotFoundError
from app.schemas import HealthResponse, HomeTurfDraft, HomeTurfManifest


DEFAULT_PROMPT = (
    "empty urban asphalt and wall, realistic continuation of the surrounding "
    "street, consistent perspective, consistent light"
)
ALLOWED_IMAGE_TYPES = {"image/png", "image/jpeg", "image/jpg"}

settings = Settings.from_environment()
provider = create_provider(settings)
inpainting_service = InpaintingService(
    provider=provider,
    max_mask_ratio=settings.max_mask_ratio,
    margin_pixels=settings.inpaint_margin_pixels,
)
repository = LocalTurfRepository(settings.data_dir, settings.public_base_url)

app = FastAPI(
    title="Street Turf Inpainting API",
    version="0.1.0",
    description=(
        "Receives a player-owned street image and a player-painted mask. "
        "Google Maps imagery is not accepted for derivative generation."
    ),
)


async def _read_upload(upload: UploadFile, allowed_types: set[str]) -> bytes:
    content_type = (upload.content_type or "").lower()
    if content_type not in allowed_types:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail=f"unsupported content type: {content_type or 'missing'}",
        )

    data = await upload.read(settings.max_upload_bytes + 1)
    if len(data) > settings.max_upload_bytes:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail=f"file exceeds {settings.max_upload_bytes} bytes",
        )
    return data


def _validate_prompt(prompt: str) -> str:
    cleaned = " ".join(prompt.split())
    if not 3 <= len(cleaned) <= 240:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="prompt must contain between 3 and 240 characters",
        )
    return cleaned


def _decode_pair(image_data: bytes, mask_data: bytes):
    try:
        original = decode_image(image_data, "RGB", settings.max_image_pixels)
        mask = decode_image(mask_data, "L", settings.max_image_pixels)
        return original, mask
    except ImageValidationError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=str(error),
        ) from error


def _not_found(error: TurfNotFoundError) -> HTTPException:
    return HTTPException(
        status_code=status.HTTP_404_NOT_FOUND,
        detail=f"home turf not found: {error.args[0]}",
    )


@app.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    return HealthResponse(provider=provider.name)


@app.post("/v1/inpaint", response_class=StreamingResponse)
async def inpaint(
    image: UploadFile = File(...),
    mask: UploadFile = File(...),
    prompt: str = Form(DEFAULT_PROMPT),
    seed: int = Form(42, ge=0, le=2_147_483_647),
) -> StreamingResponse:
    try:
        image_data = await _read_upload(image, ALLOWED_IMAGE_TYPES)
        mask_data = await _read_upload(mask, {"image/png"})
    finally:
        await image.close()
        await mask.close()

    original, mask_image = _decode_pair(image_data, mask_data)
    try:
        result = await run_in_threadpool(
            inpainting_service.process,
            original,
            mask_image,
            _validate_prompt(prompt),
            seed,
        )
    except ImageValidationError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=str(error),
        ) from error

    png = encode_png(result.image)
    box = ",".join(str(value) for value in result.target_box)
    return StreamingResponse(
        BytesIO(png),
        media_type="image/png",
        headers={
            "Content-Disposition": 'inline; filename="cleaned.png"',
            "X-Street-Turf-Mask-Ratio": f"{result.mask_ratio:.6f}",
            "X-Street-Turf-Target-Box": box,
            "X-Content-Type-Options": "nosniff",
        },
    )


@app.post(
    "/v1/home-turfs",
    response_model=HomeTurfManifest,
    status_code=status.HTTP_201_CREATED,
)
async def create_home_turf(
    environment: UploadFile = File(...),
    mask: UploadFile = File(...),
    manifest_json: str = Form(...),
    prompt: str = Form(DEFAULT_PROMPT),
    seed: int = Form(42, ge=0, le=2_147_483_647),
) -> HomeTurfManifest:
    try:
        draft = HomeTurfDraft.model_validate_json(manifest_json)
    except ValidationError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=error.errors(include_url=False, include_context=False),
        ) from error

    try:
        image_data = await _read_upload(environment, ALLOWED_IMAGE_TYPES)
        mask_data = await _read_upload(mask, {"image/png"})
    finally:
        await environment.close()
        await mask.close()

    original, mask_image = _decode_pair(image_data, mask_data)
    try:
        result = await run_in_threadpool(
            inpainting_service.process,
            original,
            mask_image,
            _validate_prompt(prompt),
            seed,
        )
    except ImageValidationError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=str(error),
        ) from error

    environment_png = encode_png(result.image)
    return repository.save(
        draft=draft,
        environment_png=environment_png,
        environment_sha256=bytes_sha256(environment_png),
        original_sha256=bytes_sha256(image_data),
        provider_name=provider.name,
        mask_ratio=result.mask_ratio,
        target_box=result.target_box,
    )


@app.get("/v1/home-turfs/{turf_id}", response_model=HomeTurfManifest)
async def get_home_turf(turf_id: str) -> HomeTurfManifest:
    try:
        return repository.read_manifest(turf_id)
    except TurfNotFoundError as error:
        raise _not_found(error) from error


@app.get("/v1/home-turfs/{turf_id}/environment", response_class=FileResponse)
async def get_home_turf_environment(turf_id: str) -> FileResponse:
    try:
        path = repository.environment_path(turf_id)
    except TurfNotFoundError as error:
        raise _not_found(error) from error
    return FileResponse(
        path=path,
        media_type="image/png",
        filename=f"{turf_id}.png",
        headers={"X-Content-Type-Options": "nosniff"},
    )
