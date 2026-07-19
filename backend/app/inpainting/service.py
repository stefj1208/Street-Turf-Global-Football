from __future__ import annotations

from dataclasses import dataclass

from PIL import Image

from app.image_processing import composite_target_region, create_target_region
from app.inpainting.base import InpaintingProvider


@dataclass(frozen=True, slots=True)
class InpaintingResult:
    image: Image.Image
    mask_ratio: float
    target_box: tuple[int, int, int, int]


class InpaintingService:
    def __init__(
        self,
        provider: InpaintingProvider,
        max_mask_ratio: float,
        margin_pixels: int,
    ) -> None:
        self.provider = provider
        self.max_mask_ratio = max_mask_ratio
        self.margin_pixels = margin_pixels

    def process(
        self,
        original: Image.Image,
        mask: Image.Image,
        prompt: str,
        seed: int,
    ) -> InpaintingResult:
        target = create_target_region(
            original=original,
            mask=mask,
            max_mask_ratio=self.max_mask_ratio,
            margin=self.margin_pixels,
        )
        generated_crop = self.provider.inpaint(
            image=target.image,
            mask=target.mask,
            prompt=prompt,
            seed=seed,
        )
        composed = composite_target_region(
            original=original,
            full_mask=mask,
            generated_crop=generated_crop,
            box=target.box,
        )
        return InpaintingResult(
            image=composed,
            mask_ratio=target.mask_ratio,
            target_box=target.box,
        )

