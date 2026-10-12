Shader "UI/Blur Gaussian"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Size ("Blur Size", Range(0, 30)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragHorizontal
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Size;

            fixed4 fragHorizontal(v2f_img input) : SV_Target
            {
                float2 offset = float2(_MainTex_TexelSize.x * _Size, 0);
                fixed4 color = 0;
                color += tex2D(_MainTex, input.uv + offset * -4.0) * 0.05;
                color += tex2D(_MainTex, input.uv + offset * -3.0) * 0.09;
                color += tex2D(_MainTex, input.uv + offset * -2.0) * 0.12;
                color += tex2D(_MainTex, input.uv + offset * -1.0) * 0.15;
                color += tex2D(_MainTex, input.uv) * 0.18;
                color += tex2D(_MainTex, input.uv + offset * 1.0) * 0.15;
                color += tex2D(_MainTex, input.uv + offset * 2.0) * 0.12;
                color += tex2D(_MainTex, input.uv + offset * 3.0) * 0.09;
                color += tex2D(_MainTex, input.uv + offset * 4.0) * 0.05;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragVertical
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Size;

            fixed4 fragVertical(v2f_img input) : SV_Target
            {
                float2 offset = float2(0, _MainTex_TexelSize.y * _Size);
                fixed4 color = 0;
                color += tex2D(_MainTex, input.uv + offset * -4.0) * 0.05;
                color += tex2D(_MainTex, input.uv + offset * -3.0) * 0.09;
                color += tex2D(_MainTex, input.uv + offset * -2.0) * 0.12;
                color += tex2D(_MainTex, input.uv + offset * -1.0) * 0.15;
                color += tex2D(_MainTex, input.uv) * 0.18;
                color += tex2D(_MainTex, input.uv + offset * 1.0) * 0.15;
                color += tex2D(_MainTex, input.uv + offset * 2.0) * 0.12;
                color += tex2D(_MainTex, input.uv + offset * 3.0) * 0.09;
                color += tex2D(_MainTex, input.uv + offset * 4.0) * 0.05;
                return color;
            }
            ENDHLSL
        }
    }
}
