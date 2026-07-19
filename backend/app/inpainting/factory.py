from __future__ import annotations

from app.config import Settings
from app.inpainting.base import InpaintingProvider
from app.inpainting.diffusers_provider import DiffusersInpaintingProvider
from app.inpainting.mock import MockInpaintingProvider


def create_provider(settings: Settings) -> InpaintingProvider:
    if settings.inpaint_provider == "mock":
        return MockInpaintingProvider()
    if settings.inpaint_provider == "diffusers":
        return DiffusersInpaintingProvider(
            model_id=settings.model_id,
            device=settings.device,
            torch_dtype=settings.torch_dtype,
            model_max_side=settings.model_max_side,
        )
    raise ValueError(f"unsupported provider: {settings.inpaint_provider}")

