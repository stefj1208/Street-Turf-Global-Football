using System;

namespace StreetTurf.Poc
{
    [Serializable]
    public sealed class RoadBiomeData
    {
        public string materialId = "asphalt_worn";
        public string baseColor = "#4A4B4D";
        public float roughness = 0.84f;
        public float friction = 0.62f;
        public float bounce = 0.36f;
    }

    [Serializable]
    public sealed class SidewalkBiomeData
    {
        public string materialId = "gray_pavers";
        public string baseColor = "#888A8D";
        public float curbHeightMeters = 0.14f;
    }

    [Serializable]
    public sealed class BuildingBiomeData
    {
        public string materialId = "red_brick";
        public string style = "industrial";
        public string baseColor = "#87483F";
        public int estimatedFloors = 3;
    }

    [Serializable]
    public sealed class LightingBiomeData
    {
        public string preset = "day_overcast";
        public float sunIntensity = 0.75f;
        public float fogDensity = 0.008f;
    }

    [Serializable]
    public sealed class StreetBiomeData
    {
        public int schemaVersion = 1;
        public RoadBiomeData road = new RoadBiomeData();
        public SidewalkBiomeData sidewalk = new SidewalkBiomeData();
        public BuildingBiomeData buildings = new BuildingBiomeData();
        public LightingBiomeData lighting = new LightingBiomeData();
        public float confidence = 1f;
        public string[] notes = Array.Empty<string>();
    }
}

