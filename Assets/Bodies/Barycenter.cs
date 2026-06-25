using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Barycenter : Body
{
    public Body[] bodies;
    public bool isStatic = true;

    public void OnEnable()
    {
        Solver.OnTickPassed += Center;
    }

    public void OnDisable()
    {
        Solver.OnTickPassed -= Center;
    }

    public override void Move(double dt)
    {
        var massSum = bodies.Aggregate(0.0, (s, b) => s + b.mass);
        position = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.position * b.mass) / massSum;
        velocity = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.velocity * b.mass) / massSum;
        acceleration = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.acceleration * b.mass) / massSum;
    }

    private void Center(double dt)
    {
        if (isStatic) {
            var delta = previousPosition - position;
            FindObjectOfType<Solver>().ShiftBodies(delta);
        }
    }

    new void OnValidate()
    {
        var massSum = bodies.Aggregate(0.0, (s, b) => s + b.mass);
        mass = massSum;
        position = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.position * b.mass) / massSum;
        velocity = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.velocity * b.mass) / massSum;
        acceleration = bodies.Aggregate(new Vector3Double(0, 0, 0), (s, b) => s + b.acceleration * b.mass) / massSum;
    }

    protected override void Start()
    {
        base.Start();
        if (isStatic)
            for (int i = 0; i < bodies.Length; i++)
                bodies[i].velocity -= velocity;
    }
}
