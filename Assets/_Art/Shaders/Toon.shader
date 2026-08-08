// Flat, banded cartoon shading for the built-in render pipeline (GDD 1: cartoon /
// stylized / low-poly).
//
// Three things do the work here:
//  - Lighting is quantised into a few bands instead of a smooth ramp, which is what
//    reads as "drawn" rather than "rendered".
//  - Shadowed bands are tinted cool rather than just darkened. Real cartoon art almost
//    never uses grey shadow; a blue-violet shade against warm light is what gives the
//    picture its bounce.
//  - A cheap rim light keeps silhouettes legible, which matters a lot in a game played
//    indoors in small rooms with a close third-person camera.
//
// Colour comes from _Color so a MaterialPropertyBlock can tint instances without
// creating a material per object.

Shader "HouseFlip/Toon"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _Steps ("Shade Bands", Range(2,6)) = 3
        _ShadowStrength ("Shadow Depth", Range(0,1)) = 0.45
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.60, 0.82, 1)
        _RimColor ("Rim Colour", Color) = (1, 0.98, 0.92, 1)
        _RimPower ("Rim Falloff", Range(0.5, 8)) = 3.0
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows
        #pragma target 3.0

        fixed4 _Color;
        half _Steps;
        half _ShadowStrength;
        fixed4 _ShadowTint;
        fixed4 _RimColor;
        half _RimPower;
        half _RimStrength;

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Alpha = _Color.a;

            // Rim term lives in Emission so it survives the banded lighting below
            // instead of being quantised away with everything else.
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower) * _RimStrength;
        }

        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            // Half-Lambert before banding: plain N.L puts everything past 90 degrees into
            // one flat black band, which loses the shape of anything facing away.
            half wrapped = dot(s.Normal, lightDir) * 0.5 + 0.5;

            half banded = floor(saturate(wrapped) * _Steps) / max(1.0, _Steps - 1.0);
            banded = saturate(banded);

            half shade = lerp(1.0 - _ShadowStrength, 1.0, banded);
            half3 tint = lerp(_ShadowTint.rgb, half3(1, 1, 1), banded);

            // Attenuation is banded too, otherwise shadow edges go soft and the whole
            // effect falls apart wherever a shadow lands.
            half shadowBand = floor(saturate(atten) * _Steps) / max(1.0, _Steps - 1.0);
            shadowBand = lerp(1.0 - _ShadowStrength, 1.0, saturate(shadowBand));

            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * tint * shade * shadowBand;
            c.a = s.Alpha;
            return c;
        }
        ENDCG
    }

    // If the toon pass fails to compile on a target, fall back to something that renders
    // rather than turning the whole house magenta.
    Fallback "Diffuse"
}
