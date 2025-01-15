Shader "Unlit/FMChromaKey"
{
	Properties
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Texture", 2D) = "white" {}

		[MaterialToggle(_TOPBOTTOM_ON)] _Toggle_TOPBOTTOM_ON("Enable Top-Bottom", Float) = 0
		[MaterialToggle(_KEYCOLOR_ON)] _Toggle_KEYCOLOR("Enable Key Color", Float) = 1
        _keyingColor ("Key Colour", Color) = (0,1,0,1)
		_thresh ("Threshold", Range (0, 16)) = 0.8
        _slope ("Slope", Range (0, 1)) = 0.2

		_VerticalFlip("VerticalFlip", Range(0,1)) = 0
		_MaskTop("MaskTop", Range(0,1)) = 0
		_MaskBottom("MaskBottom", Range(0,1)) = 0

        _CutEdgeX ("CutEdgeX", Range(0,0.5)) = 0
        _CutEdgeY ("CutEdgeY", Range(0,0.5)) = 0
		
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
		_Gamma("Gamma", float) = 0
		_Mono("Mono", Range(0,1)) = 0
	}
	SubShader
	{
		//Tags {"RenderType"="Transparent" "Queue"="Transparent"}
        //Blend SrcAlpha OneMinusSrcAlpha

		Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" }
        Cull Off // Double-sided rendering
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
		
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _KEYCOLOR_OFF _KEYCOLOR_ON
			#pragma multi_compile _TOPBOTTOM_OFF _TOPBOTTOM_ON
            #pragma multi_compile_fog
			
			#include "UnityCG.cginc"
			float4 _Color;
			sampler2D _MainTex;

			float3 _keyingColor;
            float _thresh;
            float _slope;

			float _VerticalFlip;
			float _MaskTop;
			float _MaskBottom;

            float _CutEdgeX;
            float _CutEdgeY;

			float _Cutoff;

			float _Gamma;
			float _Mono;

			struct appdata
			{
				float4 vertex : POSITION;
                UNITY_FOG_COORDS(1)
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				#if UNITY_UV_STARTS_AT_TOP
                if(_ProjectionParams.x>0){ }
                #else
                if(_ProjectionParams.x<0){ o.uv.y = 1-o.uv.y; }
                #endif

                UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			float3 RGBToMono(float3 rgb)
			{
				// Convert the RGB color to grayscale using the luminance formula
				float gray = dot(rgb, float3(0.2126, 0.7152, 0.0722));
    
				// Return a float4 with the grayscale value for R, G, and B channels, and 1 for alpha
				return float3(gray, gray, gray);
			}


			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = i.uv;
				if (_VerticalFlip > 0.5) uv.y = 1.0 - uv.y;

				float2 uvTex = uv;
				float2 uvAlpha = uv;

				float4 col = float4(1,1,1,1);
#if _TOPBOTTOM_ON
				uvTex.y /= 2.0;
				if (_VerticalFlip < 0.5) uvTex.y += 0.5;

				uvAlpha.y /= 2.0;
				if (_VerticalFlip > 0.5) uvAlpha.y += 0.5;

				col.a = tex2D(_MainTex, uvAlpha).r;
#endif
				col.rgb = tex2D(_MainTex, uvTex).rgb;


#if _KEYCOLOR_ON
				float d = abs(length(abs(_keyingColor.rgb -  col.rgb)));
				float edge0 = _thresh * (1.0 - _slope);

				col.a *= smoothstep(edge0, _thresh, d);
#endif

				if (uvTex.y > (1.0-_MaskBottom))
				{
					col.a = 0;
                }
				if (uvTex.y < _MaskTop)
				{
					col.a = 0;
                }

				if (_Mono > 0.5)
				{
					col.rgb = RGBToMono(col.rgb);
                }

                bool insideX = uvTex.x > _CutEdgeX && uvTex.x < (1.0 - _CutEdgeX);
                bool insideY = uvTex.y > _CutEdgeY && uvTex.y < (1.0 - _CutEdgeY);
				if(!insideX || !insideY) col.a = 0;

				col.rgb *= _Color.rgb;
				col.rgb = pow(col.rgb, _Gamma);
				col.a *= _Color.a;

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

				clip(col.a - _Cutoff); // Discard pixels below the alpha cutoff
				return col;
			}
			ENDCG
		}
	}
}
