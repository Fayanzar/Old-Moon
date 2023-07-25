using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Barycenter : Body
{
    public Body[] bodies;
    public bool isStatic = true;

    new void OnValidate()
    {
        var massSum = bodies.Aggregate(0.0, (s, b) => s + b.mass);
        mass = massSum;
        position = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.position * b.mass) / massSum;
        velocity = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.velocity * b.mass) / massSum;
        acceleration = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.acceleration * b.mass) / massSum;
    }

    public void Start()
    {
        if (isStatic)
            for (int i = 0; i < bodies.Length; i++)
                bodies[i].velocity -= velocity;
    }

    // Update is called once per frame
    void Update()
    {
        var massSum = bodies.Aggregate(0.0, (s, b) => s + b.mass);
        position = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.position * b.mass) / massSum;
        velocity = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.velocity * b.mass) / massSum;
        acceleration = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.acceleration * b.mass) / massSum;
    }
}
