from __future__ import annotations

import re
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator


def _to_camel(value: str) -> str:
    first, *rest = value.split("_")
    return first + "".join(word.capitalize() for word in rest)


class ApiModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=_to_camel,
        populate_by_name=True,
        extra="forbid",
        str_strip_whitespace=True,
    )


class Vector3Data(ApiModel):
    x: float = Field(ge=-1000, le=1000)
    y: float = Field(ge=-1000, le=1000)
    z: float = Field(ge=-1000, le=1000)


class QuaternionData(ApiModel):
    x: float = Field(ge=-1, le=1)
    y: float = Field(ge=-1, le=1)
    z: float = Field(ge=-1, le=1)
    w: float = Field(ge=-1, le=1)

    @model_validator(mode="after")
    def quaternion_must_not_be_zero(self) -> "QuaternionData":
        magnitude_squared = self.x**2 + self.y**2 + self.z**2 + self.w**2
        if magnitude_squared < 0.01:
            raise ValueError("rotation quaternion cannot be zero")
        return self


class GoalTransform(ApiModel):
    position: Vector3Data
    rotation: QuaternionData


class CosmeticLoadout(ApiModel):
    graffiti_sku: str = Field(default="graffiti_free_01", min_length=3, max_length=64)
    goal_net_sku: str = Field(default="net_classic_white", min_length=3, max_length=64)
    weather_sku: str = Field(default="weather_day_clear", min_length=3, max_length=64)

    @field_validator("graffiti_sku", "goal_net_sku", "weather_sku")
    @classmethod
    def sku_is_safe(cls, value: str) -> str:
        if re.fullmatch(r"[a-z0-9][a-z0-9_-]*", value) is None:
            raise ValueError("cosmetic SKU contains unsupported characters")
        return value


class HomeTurfDraft(ApiModel):
    schema_version: Literal[1] = 1
    turf_name: str = Field(min_length=3, max_length=50)
    source_kind: Literal["user_capture", "licensed_asset"]
    source_reference: str = Field(min_length=3, max_length=200)
    length_meters: float = Field(ge=12, le=50)
    width_meters: float = Field(ge=6, le=30)
    goals: list[GoalTransform] = Field(min_length=2, max_length=2)
    boundaries: list[Vector3Data] = Field(min_length=4, max_length=32)
    cosmetics: CosmeticLoadout = Field(default_factory=CosmeticLoadout)

    @model_validator(mode="after")
    def dimensions_are_coherent(self) -> "HomeTurfDraft":
        if self.length_meters < self.width_meters:
            raise ValueError("lengthMeters must be greater than or equal to widthMeters")

        first_goal = self.goals[0].position
        second_goal = self.goals[1].position
        goal_distance_squared = (
            (first_goal.x - second_goal.x) ** 2
            + (first_goal.y - second_goal.y) ** 2
            + (first_goal.z - second_goal.z) ** 2
        )
        if goal_distance_squared < 100:
            raise ValueError("the two goals must be at least 10 meters apart")

        polygon = [(point.x, point.z) for point in self.boundaries]
        signed_double_area = 0.0
        for index, point in enumerate(polygon):
            next_point = polygon[(index + 1) % len(polygon)]
            edge_length_squared = (
                (point[0] - next_point[0]) ** 2
                + (point[1] - next_point[1]) ** 2
            )
            if edge_length_squared < 0.01:
                raise ValueError("consecutive boundary points must be distinct")
            signed_double_area += (
                point[0] * next_point[1] - next_point[0] * point[1]
            )

        if abs(signed_double_area) * 0.5 < 20:
            raise ValueError("boundary polygon must cover at least 20 square meters")
        return self


class HomeTurfManifest(ApiModel):
    schema_version: Literal[1] = 1
    turf_id: str
    turf_name: str
    status: Literal["ready"] = "ready"
    environment_url: str
    environment_sha256: str
    length_meters: float
    width_meters: float
    goals: list[GoalTransform]
    boundaries: list[Vector3Data]
    cosmetics: CosmeticLoadout


class HealthResponse(ApiModel):
    status: Literal["ok"] = "ok"
    provider: str
