using System;
using UnityEngine;

namespace StreetTurf.Poc
{
    internal static class RuntimeSurfaceMaterial
    {
        private const string ShaderResourceName = "StreetTurfRuntime";
        private static Shader shader;

        public static Material Create(string materialName, Color color)
        {
            Material material = new Material(RequireShader())
            {
                name = materialName,
                color = color,
            };
            return material;
        }

        private static Shader RequireShader()
        {
            if (shader != null)
            {
                return shader;
            }

            shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null)
            {
                shader = Shader.Find("StreetTurf/Runtime");
            }
            if (shader == null)
            {
                throw new InvalidOperationException("The Street Turf runtime shader is unavailable.");
            }

            return shader;
        }
    }
}
