// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/PlanetShader"
{
	Properties
	{
		_ShoreLevel("Shore Level", Range(0, 200)) = 1.1
		_CliffThreshold("Cliff Threshold", Range(0, 90)) = 90
		_SandColor("Sand Color", Color) = (1, 1, 0, 1)
		_GrassColor("Grass Color", Color) = (0, 1, 0, 1)
		_RockColor("Rock Color", Color) = (0.4, 0.4, 0.4, 1)
    }
    SubShader
    {
			// Pass to render object as a shadow caster
	Pass {
		Name "ShadowCaster"
		Tags { "LightMode" = "ShadowCaster" }

		Fog {Mode Off}
		ZWrite On ZTest LEqual Cull Off
		Offset 1, 1

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma multi_compile_shadowcaster
		#pragma fragmentoption ARB_precision_hint_fastest
		#include "UnityCG.cginc"

		float4 _QOffset;
		float _Dist;

		struct v2f {
			V2F_SHADOW_CASTER;
		};

		v2f vert(appdata_base v) {
			float4 vPos = mul(UNITY_MATRIX_MV, v.vertex);
			float zOff = vPos.z / _Dist;
			vPos += _QOffset * zOff*zOff;
			v.vertex = mul(vPos, UNITY_MATRIX_IT_MV);
			v2f o;
			TRANSFER_SHADOW_CASTER(o)
			return o;
		}

		float4 frag(v2f i) : COLOR {
			SHADOW_CASTER_FRAGMENT(i)
		}
		ENDCG
	}

			// Pass to render object as a shadow collector
			Pass {
				Name "ShadowCollector"
				Tags { "LightMode" = "ShadowCollector" }

				Fog {Mode Off}
				ZWrite On ZTest LEqual

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest
				#pragma multi_compile_shadowcollector

				#define SHADOW_COLLECTOR_PASS
				#include "UnityCG.cginc"

				float4 _QOffset;
				float _Dist;

				struct appdata {
					float4 vertex : POSITION;
				};

				struct v2f {
					V2F_SHADOW_COLLECTOR;
				};

				v2f vert(appdata v) {
					float4 vPos = mul(UNITY_MATRIX_MV, v.vertex);
					float zOff = vPos.z / _Dist;
					vPos += _QOffset * zOff*zOff;
					v.vertex = mul(vPos, UNITY_MATRIX_IT_MV);
					v2f o;
					TRANSFER_SHADOW_COLLECTOR(o)
					return o;
				}

				fixed4 frag(v2f i) : COLOR {
					SHADOW_COLLECTOR_FRAGMENT(i)
				}
				ENDCG
			}


        Tags { "RenderType"="Opaque" "Queue"="Geometry"}
		LOD 150

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Lambert vertex:vert

		#include "WhiteNoise.cginc"
		#include "Easing.cginc"

        struct Input
        {
			float vertexHeight;
			float rockRatio;
        };
		
		float _ShoreLevel;
		float _CliffThreshold;
		float4 _SandColor;
		float4 _GrassColor;
		float4 _RockColor;

		// Run for every vertex
		void vert(inout appdata_full v, out Input o) {
			o.vertexHeight = length(v.vertex);
			float3 dirOut = normalize(v.vertex).xyz;
			float3 dirNorm = normalize(v.normal);
			float angle = acos(dot(dirOut, dirNorm));
			const float pi = 3.141592653589793238462;
			angle *= (180 / pi);

			o.rockRatio = min(max(angle-_CliffThreshold, 0) / ((90-_CliffThreshold) * 0.2), 1);
		}

		// Run for every pixel on the surface of an object
        void surf (Input i, inout SurfaceOutput o)
        {
			float4 col = lerp(_GrassColor, _RockColor, i.rockRatio);
			if (i.vertexHeight < _ShoreLevel) {
				col = _SandColor;
			}
			o.Albedo = col;
        }
        ENDCG
    }
    FallBack "Standard"
}
