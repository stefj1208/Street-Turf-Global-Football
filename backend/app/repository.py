from __future__ import annotations

import json
import os
import re
from pathlib import Path
from uuid import uuid4

from app.schemas import HomeTurfDraft, HomeTurfManifest


class TurfNotFoundError(FileNotFoundError):
    pass


class LocalTurfRepository:
    """Filesystem repository for the POC. Replace it with DB + object storage."""

    _TURF_ID_PATTERN = re.compile(r"^turf_[a-f0-9]{16}$")

    def __init__(self, data_dir: Path, public_base_url: str) -> None:
        self._data_dir = data_dir
        self._public_base_url = public_base_url.rstrip("/")
        self._data_dir.mkdir(parents=True, exist_ok=True)

    def _directory_for(self, turf_id: str) -> Path:
        if self._TURF_ID_PATTERN.fullmatch(turf_id) is None:
            raise TurfNotFoundError(turf_id)
        return self._data_dir / turf_id

    @staticmethod
    def _atomic_write(path: Path, data: bytes) -> None:
        temporary = path.with_suffix(path.suffix + ".tmp")
        temporary.write_bytes(data)
        os.replace(temporary, path)

    def save(
        self,
        draft: HomeTurfDraft,
        environment_png: bytes,
        environment_sha256: str,
        original_sha256: str,
        provider_name: str,
        mask_ratio: float,
        target_box: tuple[int, int, int, int],
    ) -> HomeTurfManifest:
        turf_id = f"turf_{uuid4().hex[:16]}"
        turf_directory = self._directory_for(turf_id)
        turf_directory.mkdir(parents=False, exist_ok=False)

        manifest = HomeTurfManifest(
            schema_version=1,
            turf_id=turf_id,
            turf_name=draft.turf_name,
            status="ready",
            environment_url=(
                f"{self._public_base_url}/v1/home-turfs/{turf_id}/environment"
            ),
            environment_sha256=environment_sha256,
            length_meters=draft.length_meters,
            width_meters=draft.width_meters,
            goals=draft.goals,
            boundaries=draft.boundaries,
            cosmetics=draft.cosmetics,
        )

        manifest_data = json.dumps(
            manifest.model_dump(by_alias=True, mode="json"),
            ensure_ascii=False,
            indent=2,
        ).encode("utf-8")
        private_audit_data = json.dumps(
            {
                "sourceKind": draft.source_kind,
                "sourceReference": draft.source_reference,
                "originalSha256": original_sha256,
                "provider": provider_name,
                "maskRatio": mask_ratio,
                "targetBox": list(target_box),
            },
            ensure_ascii=False,
            indent=2,
        ).encode("utf-8")
        self._atomic_write(turf_directory / "environment.png", environment_png)
        self._atomic_write(turf_directory / "manifest.json", manifest_data)
        self._atomic_write(turf_directory / "private-audit.json", private_audit_data)
        return manifest

    def read_manifest(self, turf_id: str) -> HomeTurfManifest:
        path = self._directory_for(turf_id) / "manifest.json"
        if not path.is_file():
            raise TurfNotFoundError(turf_id)
        return HomeTurfManifest.model_validate_json(path.read_bytes())

    def environment_path(self, turf_id: str) -> Path:
        path = self._directory_for(turf_id) / "environment.png"
        if not path.is_file():
            raise TurfNotFoundError(turf_id)
        return path
