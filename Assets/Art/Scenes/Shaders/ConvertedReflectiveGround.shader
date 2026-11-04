Shader "ConvertedResources/ReflectiveGround"
{
    Properties
    {
        _MainTex ("Baked environment", 2D) = "black" {}
        _Color ("Reflection tint", Color) = (.85,.85,.85,1)
        _FadeStart ("Fade start (metres)", Float) = 5
        _FadeEnd ("Fade end (metres)", Float) = 14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;float _FadeStart;float _FadeEnd;
            CBUFFER_END
            v2f vert(appdata v)
            { v2f o;o.position=TransformObjectToHClip(v.vertex.xyz);o.world=TransformObjectToWorld(v.vertex.xyz);o.normal=TransformObjectToWorldNormal(v.normal);return o; }
            float4 frag(v2f i):SV_Target
            {
                float3 incident=normalize(i.world-_WorldSpaceCameraPos.xyz);
                float3 direction=reflect(incident,normalize(i.normal));
                float2 uv=float2(atan2(direction.x,direction.z)*.159154943+.5,asin(clamp(direction.y,-1,1))*.318309886+.5);
                float3 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb*_Color.rgb;
                float alpha=(1-smoothstep(_FadeStart,_FadeEnd,distance(i.world.xz,_WorldSpaceCameraPos.xz)))*_Color.a;
                return float4(color,alpha);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            sampler2D _MainTex;float4 _Color;float _FadeStart;float _FadeEnd;
            v2f vert(appdata v)
            { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o; }
            float4 frag(v2f i):SV_Target
            {
                float3 incident=normalize(i.world-_WorldSpaceCameraPos.xyz);
                float3 direction=reflect(incident,normalize(i.normal));
                float2 uv=float2(atan2(direction.x,direction.z)*.159154943+.5,asin(clamp(direction.y,-1,1))*.318309886+.5);
                float3 color=tex2D(_MainTex,uv).rgb*_Color.rgb;
                float alpha=(1-smoothstep(_FadeStart,_FadeEnd,distance(i.world.xz,_WorldSpaceCameraPos.xz)))*_Color.a;
                return float4(color,alpha);
            }
            ENDCG
        }
    }
}
