Shader "Workshop/FiniteElementField"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _SurfaceShade ("Surface shading", Range(0,1)) = 0
        _Contours ("Contour lines", Range(0,1)) = 0
        _Ambient ("Ambient lighting", Range(0,1)) = .72
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float3 normalWS : TEXCOORD0; float scalar : TEXCOORD1; };
            float4 _Tint;
            float _SurfaceShade, _Contours, _Ambient;
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color * _Tint;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.scalar = i.uv.x;
                return o;
            }
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS + 1e-8) * (front ? 1 : -1);
                float light = _Ambient + (1-_Ambient) * saturate(dot(n, normalize(float3(-.3,.8,-.6))));
                float bands = i.scalar * 12;
                float edge = min(frac(bands), 1 - frac(bands));
                float contourEdge = 1 - smoothstep(0, max(fwidth(bands), .005), edge);
                float3 rgb = i.color.rgb * lerp(1, light, _SurfaceShade);
                rgb *= 1 - contourEdge * .22 * _Contours;
                return half4(rgb, i.color.a);
            }
            ENDHLSL
        }
    }
}
