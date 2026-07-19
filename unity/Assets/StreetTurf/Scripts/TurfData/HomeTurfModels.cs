using System;
using UnityEngine;

namespace StreetTurf.TurfData
{
    [Serializable]
    public sealed class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data()
        {
        }

        public Vector3Data(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public sealed class QuaternionData
    {
        public float x;
        public float y;
        public float z;
        public float w = 1f;

        public QuaternionData()
        {
        }

        public QuaternionData(Quaternion value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
            w = value.w;
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    [Serializable]
    public sealed class GoalTransformData
    {
        public Vector3Data position = new Vector3Data();
        public QuaternionData rotation = new QuaternionData();

        public GoalTransformData()
        {
        }

        public GoalTransformData(Transform goal)
        {
            position = new Vector3Data(goal.position);
            rotation = new QuaternionData(goal.rotation);
        }
    }

    [Serializable]
    public sealed class CosmeticLoadout
    {
        public string graffitiSku = "graffiti_free_01";
        public string goalNetSku = "net_classic_white";
        public string weatherSku = "weather_day_clear";
    }

    [Serializable]
    public sealed class HomeTurfDraft
    {
        public int schemaVersion = 1;
        public string turfName = "Mon Home Turf";
        public string sourceKind = "user_capture";
        public string sourceReference = "mobile-capture";
        public float lengthMeters = 24f;
        public float widthMeters = 12f;
        public GoalTransformData[] goals = Array.Empty<GoalTransformData>();
        public Vector3Data[] boundaries = Array.Empty<Vector3Data>();
        public CosmeticLoadout cosmetics = new CosmeticLoadout();
    }

    [Serializable]
    public sealed class HomeTurfManifest
    {
        public int schemaVersion;
        public string turfId = string.Empty;
        public string turfName = string.Empty;
        public string status = string.Empty;
        public string environmentUrl = string.Empty;
        public string environmentSha256 = string.Empty;
        public float lengthMeters;
        public float widthMeters;
        public GoalTransformData[] goals = Array.Empty<GoalTransformData>();
        public Vector3Data[] boundaries = Array.Empty<Vector3Data>();
        public CosmeticLoadout cosmetics = new CosmeticLoadout();
    }
}

