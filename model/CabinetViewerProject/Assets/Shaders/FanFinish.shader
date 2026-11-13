Shader "Fan/Physical Finish"
{
    Properties
    {
        _BaseMap("Base", 2D) = "white" {}
        _BaseMapStrength("Base texture strength", Range(0,1)) = 1
        [HideInInspector] _CabinetVisibility("Harness visibility", Range(0,1)) = 1
        _BaseColor("Tint", Color) = (1,1,1,1)
        _BumpMap("Micro normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Float) = 0.16
        _RoughMap("Roughness variation", 2D) = "gray" {}
        _MetalMap("Metallic variation", 2D) = "white" {}
        _EmissionMap("Emission", 2D) = "white" {}
        _EmissionColor("Emission color", Vector) = (0,0,0,0)
        _UVChannel("Physical UV channel", Float) = 0
        _Repeat("Physical repeat", Float) = 1
        _RoughBias("Roughness bias", Float) = 0.5
        _RoughScale("Roughness map scale", Float) = 0
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Grain("Grain", Float) = 0.02
        _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughMap); SAMPLER(sampler_RoughMap);
            TEXTURE2D(_MetalMap); SAMPLER(sampler_MetalMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float _UVChannel, _Repeat, _RoughBias, _RoughScale;
                half _Metallic, _Smoothness, _BumpScale, _Grain, _Cutoff, _BaseMapStrength, _CabinetVisibility;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0; float2 finishUV:TEXCOORD1; };
            struct V
            {
                float4 p:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                float2 uv:TEXCOORD3;
                half fog:TEXCOORD4;
            };
            V Vert(A a)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(a.p.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(a.n, a.t);
                o.p = p.positionCS;
                o.world = p.positionWS;
                o.normal = n.normalWS;
                o.uv = lerp(a.uv, a.finishUV, _UVChannel) * _Repeat;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                if (_CabinetVisibility < 1)
                    clip(_CabinetVisibility - InterleavedGradientNoise(i.p.xy, 0));
                half3 baseColor = lerp(half3(1,1,1),
                    SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb, _BaseMapStrength) * _BaseColor.rgb;
                half3 ts = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                half3 n = normalize(i.normal);
                float3 dpdx = ddx(i.world);
                float3 dpdy = ddy(i.world);
                float2 duvdx = ddx(i.uv);
                float2 duvdy = ddy(i.uv);
                float determinant = duvdx.x * duvdy.y - duvdx.y * duvdy.x;
                if (_BumpScale > 0 && abs(determinant) > 1e-8)
                {
                    float orientation = sign(determinant);
                    half3 t = SafeNormalize((dpdx * duvdy.y - dpdy * duvdx.y) * orientation);
                    half3 b = SafeNormalize((-dpdx * duvdy.x + dpdy * duvdx.x) * orientation);
                    n = normalize(ts.x*t + ts.y*b + ts.z*n);
                }
                // Sub-millimetre finish fades below pixel resolution.
                float3 q = i.world * 2100.0;
                float fade = saturate(1.0 - max(length(ddx(q)),length(ddy(q))) * .35);
                half3 grain = sin(q.yzx*1.17 + sin(q.zxy*.73));
                n = normalize(n + (grain - n*dot(n,grain))*_Grain*fade);
                InputData data = (InputData)0;
                data.positionWS = i.world;
                data.normalWS = n;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.world);
                data.shadowCoord = TransformWorldToShadowCoord(i.world);
                data.bakedGI = SampleSH(n);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.p);
                data.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseColor;
                surface.metallic = saturate(_Metallic * SAMPLE_TEXTURE2D(
                    _MetalMap,sampler_MetalMap,i.uv).r);
                surface.smoothness = 1 - saturate(_RoughBias + _RoughScale *
                    SAMPLE_TEXTURE2D(_RoughMap,sampler_RoughMap,i.uv).r);
                surface.emission = _EmissionColor.rgb *
                    SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb;
                surface.normalTS = ts;
                surface.occlusion = 1;
                surface.alpha = 1;
                half4 result = UniversalFragmentPBR(data,surface);
                result.rgb = MixFog(result.rgb,i.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
