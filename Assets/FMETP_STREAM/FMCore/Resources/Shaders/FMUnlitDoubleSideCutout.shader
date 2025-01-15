Shader "Unlit/FMUnlitDoubleSideCutout"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Invert ("Invert", Range(0,1)) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        //Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" }
        Cull Off // Double-sided rendering
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Invert;
            float _Cutoff;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                #if UNITY_UV_STARTS_AT_TOP
                if(_ProjectionParams.x>0){ }
                #else
                if(_ProjectionParams.x<0){ o.uv.y = 1-o.uv.y; }
                #endif

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                if(_Invert > 0.5) col.rgb = float3(1.0, 1.0, 1.0) - col.rgb;
                clip(col.a - _Cutoff); // Discard pixels below the alpha cutoff
                return col;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent Cutout"
}
