from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


def _read_int(name: str, default: int) -> int:
    value = int(os.getenv(name, str(default)))
    if value <= 0:
        raise ValueError(f"{name} must be greater than zero")
    return value


def _read_float(name: str, default: float) -> float:
    value = float(os.getenv(name, str(default)))
    if value <= 0:
        raise ValueError(f"{name} must be greater than zero")
    return value


@dataclass(frozen=True, slots=True)
class Settings:
    inpaint_provider: str
    model_id: str
    device: str
    torch_dtype: str
    max_upload_bytes: int
    max_image_pixels: int
    max_mask_ratio: float
    inpaint_margin_pixels: int
    model_max_side: int
    data_dir: Path
    public_base_url: str

    @classmethod
    def from_environment(cls) -> "Settings":
        default_data_dir = Path(__file__).resolve().parents[1] / "data"
        provider = os.getenv("STREET_TURF_INPAINT_PROVIDER", "mock").strip().lower()
        if provider not in {"mock", "diffusers"}:
            raise ValueError(
                "STREET_TURF_INPAINT_PROVIDER must be 'mock' or 'diffusers'"
            )

        max_mask_ratio = _read_float("STREET_TURF_MAX_MASK_RATIO", 0.45)
        if max_mask_ratio > 1:
            raise ValueError("STREET_TURF_MAX_MASK_RATIO cannot be greater than 1")

        data_dir = Path(
            os.getenv("STREET_TURF_DATA_DIR", str(default_data_dir))
        ).expanduser()

        return cls(
            inpaint_provider=provider,
            model_id=os.getenv(
                "STREET_TURF_MODEL_ID",
                "stable-diffusion-v1-5/stable-diffusion-inpainting",
            ).strip(),
            device=os.getenv("STREET_TURF_DEVICE", "auto").strip().lower(),
            torch_dtype=os.getenv("STREET_TURF_TORCH_DTYPE", "float16")
            .strip()
            .lower(),
            max_upload_bytes=_read_int("STREET_TURF_MAX_UPLOAD_BYTES", 20 * 1024 * 1024),
            max_image_pixels=_read_int("STREET_TURF_MAX_IMAGE_PIXELS", 4096 * 4096),
            max_mask_ratio=max_mask_ratio,
            inpaint_margin_pixels=_read_int(
                "STREET_TURF_INPAINT_MARGIN_PIXELS", 64
            ),
            model_max_side=_read_int("STREET_TURF_MODEL_MAX_SIDE", 1024),
            data_dir=data_dir.resolve(),
            public_base_url=os.getenv(
                "STREET_TURF_PUBLIC_BASE_URL", "http://localhost:8000"
            ).rstrip("/"),
        )

