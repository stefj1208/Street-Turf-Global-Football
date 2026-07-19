from __future__ import annotations

import unittest
from io import BytesIO

from PIL import Image

from app.image_processing import (
    ImageValidationError,
    composite_target_region,
    create_target_region,
    decode_image,
    encode_png,
    normalize_mask,
)


class ImageProcessingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original = Image.new("RGB", (128, 128), (20, 40, 60))
        self.mask = Image.new("L", (128, 128), 0)
        for y in range(48, 80):
            for x in range(48, 80):
                self.mask.putpixel((x, y), 255)

    def test_target_region_contains_mask_and_margin(self) -> None:
        target = create_target_region(
            self.original,
            self.mask,
            max_mask_ratio=0.45,
            margin=8,
        )
        self.assertEqual(target.box, (40, 40, 88, 88))
        self.assertAlmostEqual(target.mask_ratio, 1024 / 16384)

    def test_composition_changes_only_white_pixels(self) -> None:
        target = create_target_region(
            self.original,
            self.mask,
            max_mask_ratio=0.45,
            margin=8,
        )
        generated = Image.new("RGB", target.image.size, (200, 10, 10))
        result = composite_target_region(
            self.original,
            self.mask,
            generated,
            target.box,
        )
        self.assertEqual(result.getpixel((10, 10)), (20, 40, 60))
        self.assertEqual(result.getpixel((60, 60)), (200, 10, 10))

    def test_empty_mask_is_rejected(self) -> None:
        with self.assertRaises(ImageValidationError):
            create_target_region(
                self.original,
                Image.new("L", (128, 128), 0),
                max_mask_ratio=0.45,
                margin=8,
            )

    def test_large_mask_is_rejected(self) -> None:
        with self.assertRaises(ImageValidationError):
            create_target_region(
                self.original,
                Image.new("L", (128, 128), 255),
                max_mask_ratio=0.45,
                margin=8,
            )

    def test_png_round_trip(self) -> None:
        encoded = encode_png(self.original)
        decoded = decode_image(encoded, "RGB", max_image_pixels=128 * 128)
        self.assertEqual(decoded.size, self.original.size)
        self.assertEqual(decoded.getpixel((0, 0)), (20, 40, 60))

    def test_mask_threshold(self) -> None:
        mask = Image.new("L", (64, 64), 127)
        mask.putpixel((1, 1), 128)
        normalized = normalize_mask(mask)
        self.assertEqual(normalized.getpixel((0, 0)), 0)
        self.assertEqual(normalized.getpixel((1, 1)), 255)

    def test_corrupt_image_is_rejected(self) -> None:
        with self.assertRaises(ImageValidationError):
            decode_image(BytesIO(b"not-an-image").getvalue(), "RGB", 100_000)


if __name__ == "__main__":
    unittest.main()

