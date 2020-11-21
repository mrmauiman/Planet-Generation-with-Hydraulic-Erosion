using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Cloud
{
    public Vector2 globalCoords;
    public float radius;
    public float altitude;
    public float life;
    public Vector2 velocity;

    const float windspeed = 0.05f;
    const float maxspeed = 1;
    const float variationFactor = 0.2f;

    public Cloud(Vector2 pCoords, float pRadius, float pAltitude, float pLife)
    {
        globalCoords = pCoords;
        radius = pRadius;
        altitude = pAltitude;
        life = pLife;
        velocity = Vector2.zero;
    }

    public void Absorb(Cloud other)
    {
        globalCoords = (globalCoords + other.globalCoords) / 2;
        radius += other.radius;
        altitude = (altitude + other.altitude) / 2;
        life += other.life;
        velocity += other.velocity;
    }

    // Gets the position in 3d space based on altitude and globalCoords
    public Vector3 Position()
    {
        Vector3 rv = Vector3.forward * altitude;
        // Rotate Longitude
        Quaternion rotation = Quaternion.AngleAxis(globalCoords.x, Vector3.up);
        rv = rotation * rv;
        // Rotate Latitude
        rotation = Quaternion.AngleAxis(-globalCoords.y, rotation * Vector3.right);
        rv = rotation * rv;
        return rv;
    }

    public void ApplyWindForce(Vector2 direction)
    {
        Vector2 variation = new Vector2(UnityEngine.Random.value * variationFactor, UnityEngine.Random.value * variationFactor);
        direction += variation;
        velocity += direction * windspeed;
        velocity = velocity.normalized * Mathf.Min(velocity.magnitude, maxspeed);
    }

    public void Move()
    {
        globalCoords += velocity;
        if (globalCoords.x > 180)
        {
            globalCoords.x = -180 + (globalCoords.x - 180);
        }
        else if (globalCoords.x < -180)
        {
            globalCoords.x = 180 + (globalCoords.x + 180);
        }
        if (globalCoords.y > 90)
        {
            globalCoords.y = -90 + (globalCoords.y - 90);
        }
        else if (globalCoords.y < -90)
        {
            globalCoords.y = 90 + (globalCoords.y + 90);
        }
    }
}
