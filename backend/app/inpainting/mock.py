from __future__ import annotations

from PIL import Image, ImageFilter


class MockInpaintingProvider:
    """Fast non-AI provider used to verify mobile/backend integration."""

    @property
    def name(self) -> str:
        return "mock"

    def inpaint(
        self,
        image: Image.Image,
        mask: Image.Image,
        prompt: str,
        seed: int,
    ) -> Image.Image:
        del mask, prompt, seed
        return image.convert("RGB").filter(ImageFilter.GaussianBlur(radius=18))

