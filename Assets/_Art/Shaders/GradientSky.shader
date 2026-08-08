// Three-stop gradient skybox.
//
// A flat gradient suits the art direction better than a photographic sky: it stays out of
// the way, it reads as illustration, and it gives the yard a horizon without any texture
// memory. The horizon band is deliberately the brightest stop — that is what makes a
// stylised sky feel sunlit rather than flat.

Shader "HouseFlip/GradientSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.35, 0.55, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.85, 0.90, 0.96, 1)
        _BottomColor ("Bottom", Color) = (0.45, 0.42, 0.40, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.2
        _Exposure ("Exposure", Range(0, 3)) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            half _HorizonSharpness;
            half _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float height = normalize(i.dir).y;

                // Blend up from the horizon and down from it separately, so the horizon
                // stop stays a crisp band rather than being smeared across the whole sky.
                float up = pow(saturate(height), 1.0 / _HorizonSharpness);
                float down = pow(saturate(-height), 1.0 / _HorizonSharpness);

                fixed3 col = _HorizonColor.rgb;
                col = lerp(col, _TopColor.rgb, up);
                col = lerp(col, _BottomColor.rgb, down);

                return fixed4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
