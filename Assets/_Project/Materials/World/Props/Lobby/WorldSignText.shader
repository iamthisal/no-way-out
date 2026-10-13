Shader "Museum/World Sign Text"
{
    Properties { _MainTex ("Font Atlas", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varying { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varying vert(Input v) { Varying o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o; }
            half4 frag(Varying i):SV_Target { return half4(i.color.rgb,i.color.a*tex2D(_MainTex,i.uv).a); }
            ENDHLSL
        }
    }
}
