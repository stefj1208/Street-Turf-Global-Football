from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
from io import BytesIO
from math import ceil, floor

from PIL import Image, ImageOps, UnidentifiedImageError


class ImageValidationError(ValueError):
    """Raised when a mobile image or mask is unsafe or inconsistent."""


@dataclass(frozen=True, slots=True)
class TargetRegion:
    box: tuple[int, int, int, int]
    image: Image.Image
    mask: Image.Image
    mask_ratio: float


def decode_image(data: bytes, mode: str, max_image_pixels: int) -> Image.Image:
    if not data:
        raise ImageValidationError("uploaded image is empty")

    previous_limit = Image.MAX_IMAGE_PIXELS
    Image.MAX_IMAGE_PIXELS = max_image_pixels
    try:
        with Image.open(BytesIO(data)) as probe:
            probe.verify()

        with Image.open(BytesIO(data)) as opened:
            transposed = ImageOps.exif_transpose(opened)
            converted = transposed.convert(mode)
            converted.load()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as error:
        raise ImageValidationError("uploaded file is not a valid supported image") from error
    finally:
        Image.MAX_IMAGE_PIXELS = previous_limit

    width, height = converted.size
    if width < 64 or height < 64:
        raise ImageValidationError("image must be at least 64 x 64 pixels")
    if width * height > max_image_pixels:
        raise ImageValidationError("image contains too many pixels")
    return converted


def normalize_mask(mask: Image.Image) -> Image.Image:
    grayscale = mask.convert("L")
    return grayscale.point(lambda value: 255 if value >= 128 else 0, mode="L")


def validate_pair(
    original: Image.Image,
    mask: Image.Image,
    max_mask_ratio: float,
) -> float:
    if original.size != mask.size:
        raise ImageValidationError("image and mask must have exactly the same dimensions")

    histogram = mask.histogram()
    selected_pixels = histogram[255]
    total_pixels = original.width * original.height
    ratio = selected_pixels / total_pixels

    if selected_pixels == 0:
        raise ImageValidationError("mask is empty; paint at least one obstacle")
    if ratio > max_mask_ratio:
        raise ImageValidationError(
            f"mask covers {ratio:.1%}; maximum allowed is {max_mask_ratio:.1%}"
        )
    return ratio


def _aligned_box(
    box: tuple[int, int, int, int],
    image_size: tuple[int, int],
    margin: int,
    alignment: int = 8,
) -> tuple[int, int, int, int]:
    left, top, right, bottom = box
    width, height = image_size

    left = max(0, floor((left - margin) / alignment) * alignment)
    top = max(0, floor((top - margin) / alignment) * alignment)
    right = min(width, ceil((right + margin) / alignment) * alignment)
    bottom = min(height, ceil((bottom + margin) / alignment) * alignment)
    return left, top, right, bottom


def create_target_region(
    original: Image.Image,
    mask: Image.Image,
    max_mask_ratio: float,
    margin: int,
) -> TargetRegion:
    binary_mask = normalize_mask(mask)
    ratio = validate_pair(original, binary_mask, max_mask_ratio)
    raw_box = binary_mask.getbbox()
    if raw_box is None:
        raise ImageValidationError("mask is empty")

    box = _aligned_box(raw_box, original.size, margin)
    return TargetRegion(
        box=box,
        image=original.crop(box),
        mask=binary_mask.crop(box),
        mask_ratio=ratio,
    )


def composite_target_region(
    original: Image.Image,
    full_mask: Image.Image,
    generated_crop: Image.Image,
    box: tuple[int, int, int, int],
) -> Image.Image:
    left, top, right, bottom = box
    expected_size = (right - left, bottom - top)
    generated_rgb = generated_crop.convert("RGB")
    if generated_rgb.size != expected_size:
        generated_rgb = generated_rgb.resize(expected_size, Image.Resampling.LANCZOS)

    crop_mask = normalize_mask(full_mask).crop(box)
    result = original.convert("RGB").copy()
    result.paste(generated_rgb, (left, top), crop_mask)
    return result


def encode_png(image: Image.Image) -> bytes:
    output = BytesIO()
    image.save(output, format="PNG", optimize=True)
    return output.getvalue()


def bytes_sha256(data: bytes) -> str:
    return sha256(data).hexdigest()

