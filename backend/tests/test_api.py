from __future__ import annotations

import json
from io import BytesIO

from fastapi.testclient import TestClient
from PIL import Image

from app.main import app


client = TestClient(app)


def _png(color: int, with_mask_square: bool = False) -> bytes:
    image = Image.new("L", (128, 128), color)
    if with_mask_square:
        for y in range(48, 80):
            for x in range(48, 80):
                image.putpixel((x, y), 255)
    output = BytesIO()
    image.save(output, "PNG")
    return output.getvalue()


def _draft() -> dict:
    return {
        "schemaVersion": 1,
        "turfName": "Rue des Champions",
        "sourceKind": "user_capture",
        "sourceReference": "test-capture",
        "lengthMeters": 24,
        "widthMeters": 12,
        "goals": [
            {
                "position": {"x": 0, "y": 0, "z": -12},
                "rotation": {"x": 0, "y": 0, "z": 0, "w": 1},
            },
            {
                "position": {"x": 0, "y": 0, "z": 12},
                "rotation": {"x": 0, "y": 1, "z": 0, "w": 0},
            },
        ],
        "boundaries": [
            {"x": -6, "y": 0, "z": -12},
            {"x": 6, "y": 0, "z": -12},
            {"x": 6, "y": 0, "z": 12},
            {"x": -6, "y": 0, "z": 12},
        ],
        "cosmetics": {
            "graffitiSku": "graffiti_free_01",
            "goalNetSku": "net_classic_white",
            "weatherSku": "weather_day_clear",
        },
    }


def test_health() -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["provider"] == "mock"


def test_inpaint_returns_png() -> None:
    response = client.post(
        "/v1/inpaint",
        files={
            "image": ("original.png", _png(80), "image/png"),
            "mask": ("mask.png", _png(0, with_mask_square=True), "image/png"),
        },
        data={"prompt": "empty asphalt continuation", "seed": "42"},
    )
    assert response.status_code == 200
    assert response.headers["content-type"] == "image/png"
    assert response.content.startswith(b"\x89PNG")


def test_create_and_get_home_turf() -> None:
    created = client.post(
        "/v1/home-turfs",
        files={
            "environment": ("original.png", _png(80), "image/png"),
            "mask": ("mask.png", _png(0, with_mask_square=True), "image/png"),
        },
        data={
            "manifest_json": json.dumps(_draft()),
            "prompt": "empty asphalt continuation",
            "seed": "42",
        },
    )
    assert created.status_code == 201
    manifest = created.json()
    turf_id = manifest["turfId"]
    assert manifest["status"] == "ready"
    assert len(manifest["environmentSha256"]) == 64

    loaded = client.get(f"/v1/home-turfs/{turf_id}")
    assert loaded.status_code == 200
    assert loaded.json()["turfId"] == turf_id

    environment = client.get(f"/v1/home-turfs/{turf_id}/environment")
    assert environment.status_code == 200
    assert environment.content.startswith(b"\x89PNG")

