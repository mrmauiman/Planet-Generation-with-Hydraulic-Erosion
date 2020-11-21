Shader "Custom/TerrainShader"
{
    Properties
    {
		_CellSize("Cell Size", Vector) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct appdata members worldPos)
#pragma exclude_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
			#include "WhiteNoise.cginc"
			#include "Easing.cginc"

			struct Input
			{
				float3 worldPos;
			};

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

			float4 _CellSize;

			float PerlinNoise(float3 value) {
				float3 fraction = frac(value);
				float interpolatorX = easeInOut(fraction.x);
				float interpolatorY = easeInOut(fraction.y);
				float interpolatorZ = easeInOut(fraction.z);

				float cellNoiseZ[2];
				[unroll]
				for (int z = 0; z < 2; z++) {
					float cellNoiseY[2];
					[unroll]
					for (int y = 0; y < 2; y++) {
						float cellNoiseX[2];
						[unroll]
						for (int x = 0; x < 2; x++) {
							float3 cell = floor(value) + float3(x, y, z);
							float3 cellDirection = rand3dTo3d(cell) * 2 - 1;
							float3 compareVector = fraction - float3(x, y, z);
							cellNoiseX[x] = dot(cellDirection, compareVector);
						}
						cellNoiseY[y] = lerp(cellNoiseX[0], cellNoiseX[1], interpolatorX);
					}
					cellNoiseZ[z] = lerp(cellNoiseY[0], cellNoiseY[1], interpolatorY);
				}
				float noise = lerp(cellNoiseZ[0], cellNoiseZ[1], interpolatorZ);
				return noise;
			}

            v2f vert (appdata v, Input i)
            {
                v2f o;
				float3 value = i.worldPos / _CellSize;
				float noise = PerlinNoise(value);
				o.vertex = normalize(v.vertex) * noise;
				o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
