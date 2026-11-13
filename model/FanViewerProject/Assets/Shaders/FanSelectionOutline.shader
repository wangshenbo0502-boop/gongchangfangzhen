Shader "Fan/SelectionOutline"
{
    Properties { _MainTex("Mask", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 Frag(v2f_img input) : SV_Target
            {
                float center = tex2D(_MainTex, input.uv).r;
                float outside = 0;
                [unroll] for (int i = 0; i < 12; i++)
                {
                    float a = i * 6.2831853 / 12;
                    float2 d = float2(cos(a),sin(a)) * _MainTex_TexelSize.xy;
                    outside = max(outside, tex2D(_MainTex,input.uv+d*1.65).r);
                }
                return fixed4(.98,.99,1,saturate(outside-center)*.85);
            }
            ENDCG
        }
    }
}
