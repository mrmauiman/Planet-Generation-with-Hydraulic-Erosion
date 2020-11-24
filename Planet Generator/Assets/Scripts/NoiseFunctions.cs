using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Noise Functions Present are a c# adaptation of the Perlin Noise shader functions from ronja-tutorials.com
// Link: https://www.ronja-tutorials.com/2018/09/15/perlin-noise.html#perlin-noise

public static class NoiseFunctions
{
	// helper functions
	// returns value but with all member data sined
	private static Vector3 Vec3Sin(Vector3 value)
    {
		return new Vector3(Mathf.Sin(value.x), Mathf.Sin(value.y), Mathf.Sin(value.z));
    }

	// returns value without it's integer component
	private static float Frac(float value)
    {
		return value - Mathf.Floor(value);
    }

	// returns value without it's integer components
	private static Vector3 Vec3Frac(Vector3 value)
    {
		return new Vector3(Frac(value.x), Frac(value.y), Frac(value.z));
    }

	// returns value without it's fractional components
	private static Vector3 Vec3Floor(Vector3 value)
    {
		return new Vector3(Mathf.Floor(value.x), Mathf.Floor(value.y), Mathf.Floor(value.z));
    }


	// The Purpose of the two functions below is to create random numbers that are the same every time it's called with the same value
	// get a scalar random value from a 3d value
	// value is a position in space, dotDir is a random seed in the form of a vector 3
	// returns a float that is seemingly random but is the same for calls with the same value and dotDir
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

	// The purpose of this function is to get a random directional vector
	// value is a position in space
	// returns a seemingly random directional vector that is the same for calls with the same value
	private static Vector3 Rand3dTo3d(Vector3 value)
	{
		return new Vector3(
			Rand3dTo1d(value, new Vector3(12.989f, 78.233f, 37.719f)),
			Rand3dTo1d(value, new Vector3(39.346f, 11.135f, 83.155f)),
			Rand3dTo1d(value, new Vector3(73.156f, 52.235f, 09.151f))
			);
	}

	// Easing Functions

	// This returns interpolator^2
	private static float EaseIn(float interpolator)
	{
		return interpolator * interpolator;
	}

	// This returns 1 - (1-interpolator)^2
	private static float EaseOut(float interpolator)
	{
		return 1 - EaseIn(1 - interpolator);
	}

	// This returns a float that has lies interpolator distance along an s shaped curve from 0-1
	private static float EaseInOut(float interpolator)
	{
		float easeInValue = EaseIn(interpolator);
		float easeOutValue = EaseOut(interpolator);
		return Mathf.Lerp(easeInValue, easeOutValue, interpolator);
	}

	// Perlin Noise Functions
	// value is a position in space
	// returns the 3D perlin noise value for value
	public static float PerlinNoise(Vector3 value)
    {
		// Get the fractional portions of value to use as an interpolator
		Vector3 fraction = Vec3Frac(value);

		// Create the interpolators by converting the fraction from a linear curve to an s shaped curve
		float interpolatorX = EaseInOut(fraction.x);
		float interpolatorY = EaseInOut(fraction.y);
		float interpolatorZ = EaseInOut(fraction.z);

		// Use loops to get the integer values surounding value
		float[] cellNoiseZ = new float[2];
		for (int z = 0; z <= 1; z++)
        {
			float[] cellNoiseY = new float[2];
			for (int y = 0; y <= 1; y++)
            {
				float[] cellNoiseX = new float[2];
				for (int x = 0; x <= 1; x++)
                {
					// get the surrounding value (cell) we are looking at
					Vector3 cell = Vec3Floor(value) + new Vector3(x, y, z);
					// get the random vector associated with the cell and let it's values be in the ranges of -1 to 1
					Vector3 cellDirection = Rand3dTo3d(cell) * 2f - new Vector3(1f, 1f, 1f);
					// get the vector from the cell to the value
					Vector3 compareVector = fraction - new Vector3(x, y, z);
					// set the noisevalue for that cell (how similar the cell vector is to the vector pointing at value from cell)
					cellNoiseX[x] = Vector3.Dot(cellDirection, compareVector);
                }
				// interpolate along the x-axis cells by interpolator to compact the values to two axis
				cellNoiseY[y] = Mathf.Lerp(cellNoiseX[0], cellNoiseX[1], interpolatorX);
            }
			// interpolate along the y-axis by interpolator to compact the values to one axis
			cellNoiseZ[z] = Mathf.Lerp(cellNoiseY[0], cellNoiseY[1], interpolatorY);
        }
		// interpolate along the z-axis by interpolator to compact the values to a single value
		float noise = Mathf.Lerp(cellNoiseZ[0], cellNoiseZ[1], interpolatorZ);
		return noise;
	}

	// This function layers 4 levels of perlin noise
	// value is a position in space, cellSizes is how large the cells for perlin noise should be, weights is how much of the different cellSizes should be in the final value
	// returns a single noise value of 4 calculations of perlin noise multiplied by their weights added together
	public static float LayeredPerlinNoise(Vector3 value, Vector4 cellSizes, Vector4 weights)
    {
		Vector4 noises = new Vector4(PerlinNoise(value / cellSizes.x), PerlinNoise(value / cellSizes.y), PerlinNoise(value / cellSizes.z), PerlinNoise(value / cellSizes.w));
		// get noises ~0-1
		noises += new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
		float noise = (noises.x * weights.x) + (noises.y * weights.y) + (noises.z * weights.z) + (noises.w * weights.w);
		return noise;
	}
}
