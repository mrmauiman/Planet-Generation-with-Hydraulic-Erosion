using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VertexData
{
    public float longitude;
    public float latitude;
    public bool raining;

    public VertexData(float pLongitude, float pLatitude)
    {
        longitude = pLongitude;
        latitude = pLatitude;
        raining = false;
    }

    public override string ToString()
    {
        return "(" + longitude + " E, " + latitude + " N)";
    }

    public Vector2 GetCoords()
    {
        return new Vector2(longitude, latitude);
    }
}
