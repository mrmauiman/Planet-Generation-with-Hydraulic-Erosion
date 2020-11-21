using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NoiseFunctions
{
	// helper functions
	private static Vector3 Vec3Sin(Vector3 value)
    {
		return new Vector3(Mathf.Sin(value.x), Mathf.Sin(value.y), Mathf.Sin(value.z));
    }

	private static float Frac(float value)
    {
		return value - Mathf.Floor(value);
    }

	private static Vector3 Vec3Frac(Vector3 value)
    {
		return new Vector3(Frac(value.x), Frac(value.y), Frac(value.z));
    }

	private static Vector3 Vec3Floor(Vector3 value)
    {
		return new Vector3(Mathf.Floor(value.x), Mathf.Floor(value.y), Mathf.Floor(value.z));
    }


	//The Purpose of the two functions below is to create random numbers that are the same with every time it's called with the same value
	//get a scalar random value from a 3d value
	private static float Rand3dTo1d(Vector3 value, Vector3 dotDir)
	{
		//make value smaller to avoid artefacts
		Vector3 smallValue = Vec3Sin(value);
		//get scalar value from 3d vector
		float random = Vector3.Dot(smallValue, dotDir);
		//make value more random by making it bigger and then taking the factional part
		random = Frac(Mathf.Sin(random) * 143758.5453f);
		return random;
	}

	//to 3d functions

	private static Vector3 Rand3dTo3d(Vector3 value)
	{
		return new Vector3(
			Rand3dTo1d(value, new Vector3(12.989f, 78.233f, 37.719f)),
			Rand3dTo1d(value, new Vector3(39.346f, 11.135f, 83.155f)),
			Rand3dTo1d(value, new Vector3(73.156f, 52.235f, 09.151f))
			);
	}

	// Easing Functions

	private static float EaseIn(float interpolator)
	{
		return interpolator * interpolator;
	}

	private static float EaseOut(float interpolator)
	{
		return 1 - EaseIn(1 - interpolator);
	}

	private static float EaseInOut(float interpolator)
	{
		float easeInValue = EaseIn(interpolator);
		float easeOutValue = EaseOut(interpolator);
		return Mathf.Lerp(easeInValue, easeOutValue, interpolator);
	}

	// Perlin Noise Functions
	public static float PerlinNoise(Vector3 value)
    {
		Vector3 fraction = Vec3Frac(value);

		float interpolatorX = EaseInOut(fraction.x);
		float interpolatorY = EaseInOut(fraction.y);
		float interpolatorZ = EaseInOut(fraction.z);

		float[] cellNoiseZ = new float[2];
		for (int z = 0; z <= 1; z++)
        {
			float[] cellNoiseY = new float[2];
			for (int y = 0; y <= 1; y++)
            {
				float[] cellNoiseX = new float[2];
				for (int x = 0; x <= 1; x++)
                {
					Vector3 cell = Vec3Floor(value) + new Vector3(x, y, z);
					Vector3 cellDirection = Rand3dTo3d(cell) * 2f - new Vector3(1f, 1f, 1f);
					Vector3 compareVector = fraction - new Vector3(x, y, z);
					cellNoiseX[x] = Vector3.Dot(cellDirection, compareVector);
                }
				cellNoiseY[y] = Mathf.Lerp(cellNoiseX[0], cellNoiseX[1], interpolatorX);
            }
			cellNoiseZ[z] = Mathf.Lerp(cellNoiseY[0], cellNoiseY[1], interpolatorY);
        }
		float noise = Mathf.Lerp(cellNoiseZ[0], cellNoiseZ[1], interpolatorZ);
		return noise;
	}

	public static float LayeredPerlinNoise(Vector3 value, Vector4 cellSizes, Vector4 weights)
    {
		Vector4 noises = new Vector4(PerlinNoise(value / cellSizes.x), PerlinNoise(value / cellSizes.y), PerlinNoise(value / cellSizes.z), PerlinNoise(value / cellSizes.w));
		// get noises ~0-1
		noises += new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
		float noise = (noises.x * weights.x) + (noises.y * weights.y) + (noises.z * weights.z) + (noises.w * weights.w);
		return noise;
	}
}
