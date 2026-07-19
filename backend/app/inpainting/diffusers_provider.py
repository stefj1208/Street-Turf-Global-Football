from __future__ import annotations

from threading import Lock

from PIL import Image


class DiffusersInpaintingProvider:
    """Lazy Stable Diffusion inpainting provider for a single worker process."""

    def __init__(
        self,
        model_id: str,
        device: str,
        torch_dtype: str,
        model_max_side: int,
    ) -> None:
        self._model_id = model_id
        self._requested_device = device
        self._torch_dtype_name = torch_dtype
        self._model_max_side = model_max_side
        self._pipeline = None
        self._torch = None
        self._device = "cpu"
        self._lock = Lock()

    @property
    def name(self) -> str:
        return f"diffusers:{self._model_id}"

    def _load_pipeline(self) -> None:
        if self._pipeline is not None:
            return

        try:
            import torch
            from diffusers import AutoPipelineForInpainting
        except ImportError as error:
            raise RuntimeError(
                "Diffusers provider requires: pip install -e '.[ai]'"
            ) from error

        device = self._requested_device
        if device == "auto":
            device = "cuda" if torch.cuda.is_available() else "cpu"

        dtype_by_name = {
            "float16": torch.float16,
            "bfloat16": torch.bfloat16,
            "float32": torch.float32,
        }
        if self._torch_dtype_name not in dtype_by_name:
            raise RuntimeError(
                "STREET_TURF_TORCH_DTYPE must be float16, bfloat16 or float32"
            )
        dtype = dtype_by_name[self._torch_dtype_name]
        if device == "cpu" and dtype == torch.float16:
            dtype = torch.float32

        pipeline = AutoPipelineForInpainting.from_pretrained(
            self._model_id,
            torch_dtype=dtype,
            use_safetensors=True,
        )
        pipeline = pipeline.to(device)
        pipeline.set_progress_bar_config(disable=True)

        self._pipeline = pipeline
        self._torch = torch
        self._device = device

    def _model_size(self, width: int, height: int) -> tuple[int, int]:
        scale = min(1.0, self._model_max_side / max(width, height))
        resized_width = max(64, int(round(width * scale / 8)) * 8)
        resized_height = max(64, int(round(height * scale / 8)) * 8)
        return resized_width, resized_height

    def inpaint(
        self,
        image: Image.Image,
        mask: Image.Image,
        prompt: str,
        seed: int,
    ) -> Image.Image:
        with self._lock:
            self._load_pipeline()
            assert self._pipeline is not None
            assert self._torch is not None

            original_size = image.size
            model_size = self._model_size(*original_size)
            model_image = image.convert("RGB").resize(
                model_size, Image.Resampling.LANCZOS
            )
            model_mask = mask.convert("L").resize(
                model_size, Image.Resampling.NEAREST
            )
            generator = self._torch.Generator(device=self._device).manual_seed(seed)

            generated = self._pipeline(
                prompt=prompt,
                negative_prompt=(
                    "car, vehicle, person, text, logo, watermark, distorted road, "
                    "duplicate object"
                ),
                image=model_image,
                mask_image=model_mask,
                generator=generator,
                num_inference_steps=28,
                guidance_scale=6.5,
                strength=0.99,
            ).images[0]

            return generated.convert("RGB").resize(
                original_size, Image.Resampling.LANCZOS
            )

