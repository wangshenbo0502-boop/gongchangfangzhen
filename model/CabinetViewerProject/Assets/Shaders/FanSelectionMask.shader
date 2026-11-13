Shader "Fan/SelectionMask"
{
    Properties { _BaseColor("Mask", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            float4 Vert(float4 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position.xyz);
            }
            half4 Frag() : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
