from __future__ import annotations

from typing import Protocol

from PIL import Image


class InpaintingProvider(Protocol):
    @property
    def name(self) -> str:
        """Return the provider name shown by the health endpoint."""

    def inpaint(
        self,
        image: Image.Image,
        mask: Image.Image,
        prompt: str,
        seed: int,
    ) -> Image.Image:
        """Return a generated crop with the same visual framing as image."""

