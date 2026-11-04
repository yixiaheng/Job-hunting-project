Shader "ConvertedResources/Particles"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct v2f { float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;float4 _Color;
            CBUFFER_END
            v2f vert(appdata v) { v2f o;o.position=TransformObjectToHClip(v.vertex.xyz);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color*_Color;return o; }
            float4 frag(v2f i):SV_Target { float4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);float mask=max(tex.r,max(tex.g,tex.b))*tex.a;return float4(i.color.rgb,mask*i.color.a); }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct v2f { float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            sampler2D _MainTex;float4 _MainTex_ST;float4 _Color;
            v2f vert(appdata v) { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.color=v.color*_Color;return o; }
            float4 frag(v2f i):SV_Target { float4 tex=tex2D(_MainTex,i.uv);float mask=max(tex.r,max(tex.g,tex.b))*tex.a;return float4(i.color.rgb,mask*i.color.a); }
            ENDCG
        }
    }
}
