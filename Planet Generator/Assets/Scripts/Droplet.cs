using System.Collections;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

public class Droplet
{
    public const int maxSediment = 10;
    public float sediment;
    public Vector2 velocity;
    public Vector2 position;

    public Droplet(Vector2 pos)
    {
        sediment = 0;
        velocity = Vector2.zero;
        position = pos;
    }

    public bool AtCapacity()
    {
        return sediment > maxSediment;
    }
}
