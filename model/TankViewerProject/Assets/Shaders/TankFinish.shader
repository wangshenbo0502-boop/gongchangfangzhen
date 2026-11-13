Shader "Tank/Physical Finish"
{
    Properties
    {
        _BaseMap("Base", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _BaseMapStrength("Base variation", Range(0,1)) = .3
        _EmissionColor("Emission", Vector) = (0,0,0,0)
        [PerRendererData] _InspectionTint("Selected tint", Vector) = (0,0,0,0)
        _BumpMap("Micro normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Float) = 0.16
        _RoughMap("Roughness variation", 2D) = "gray" {}
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
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _EmissionColor, _InspectionTint;
                half _BaseMapStrength;
                half _Metallic, _Smoothness, _BumpScale, _Grain, _Cutoff;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0; };
            struct V
            {
                float4 p:SV_POSITION;
                float3 world:TEXCOORD0;
                half3 normal:TEXCOORD1;
                half4 tangent:TEXCOORD2;
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
                o.tangent = half4(n.tangentWS, a.t.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                half3 baseColor = lerp(half3(1,1,1), SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,
                    _BaseMapStrength) * _BaseColor.rgb;
                half3 ts = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                half3 n = normalize(i.normal);
                half3 t = SafeNormalize(i.tangent.xyz);
                half3 b = cross(n,t)*i.tangent.w;
                if (_BumpScale > 0) n = normalize(ts.x*t + ts.y*b + ts.z*n);
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
                surface.albedo = lerp(baseColor, _InspectionTint.rgb, _InspectionTint.a);
                surface.emission = _EmissionColor.rgb;
                surface.metallic = _Metallic;
                surface.smoothness = saturate(_Smoothness + (SAMPLE_TEXTURE2D(_RoughMap,sampler_RoughMap,i.uv).r-.5)*.12);
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
