Shader "StreetTurf/MaskPreview"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MaskTex("Removal Mask", 2D) = "black" {}
        _MaskOpacity("Red Mask Opacity", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 baseUv : TEXCOORD0;
                float2 maskUv : TEXCOORD1;
            };

            sampler2D _BaseMap;
            sampler2D _MaskTex;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            half _MaskOpacity;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.maskUv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 baseColor = tex2D(_BaseMap, input.baseUv) * _BaseColor;
                half mask = tex2D(_MaskTex, input.maskUv).r;
                half blend = saturate(mask * _MaskOpacity);
                baseColor.rgb = lerp(baseColor.rgb, fixed3(1.0, 0.0, 0.0), blend);
                return baseColor;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
