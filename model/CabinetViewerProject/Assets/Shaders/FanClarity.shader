Shader "Hidden/FanClarity"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                // Two source pixels correspond to one output pixel at 2x sampling.
                float2 d = _BlitTexture_TexelSize.xy * 2;
                half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half3 n = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(0,d.y)).rgb;
                half3 s = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv-float2(0,d.y)).rgb;
                half3 e = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(d.x,0)).rgb;
                half3 w = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv-float2(d.x,0)).rgb;
                half3 lo = min(c.rgb,min(min(n,s),min(e,w)));
                half3 hi = max(c.rgb,max(max(n,s),max(e,w)));
                half3 detail = c.rgb-(n+s+e+w)*.25;
                return half4(clamp(c.rgb+detail*.22,lo,hi),c.a);
            }
            ENDHLSL
        }
    }
}
