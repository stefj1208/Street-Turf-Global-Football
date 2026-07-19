from __future__ import annotations

import pytest
from fastapi import HTTPException

from vision_api import _hostname_is_forbidden, _validate_public_https_url, demo_biome


def test_demo_biome_serializes_for_unity() -> None:
    data = demo_biome().model_dump(by_alias=True, mode="json")
    assert data["schemaVersion"] == 1
    assert data["road"]["materialId"] == "asphalt_worn"
    assert 0 <= data["road"]["bounce"] <= 1


@pytest.mark.parametrize(
    "hostname",
    [
        "maps.google.com",
        "maps.google.fr",
        "streetview.google.co.uk",
        "streetviewpixels-pa.googleapis.com",
        "lh3.googleusercontent.com",
    ],
)
def test_google_image_hosts_are_forbidden(hostname: str) -> None:
    assert _hostname_is_forbidden(hostname)


def test_non_https_url_is_rejected_before_network_lookup() -> None:
    with pytest.raises(HTTPException) as error:
        _validate_public_https_url("http://example.com/street.jpg")
    assert error.value.status_code == 422


def test_invalid_port_is_rejected_without_crashing() -> None:
    with pytest.raises(HTTPException) as error:
        _validate_public_https_url("https://example.com:not-a-port/street.jpg")
    assert error.value.status_code == 422
