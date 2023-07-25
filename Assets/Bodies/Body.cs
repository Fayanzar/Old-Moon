using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Body : MonoBehaviour
{
    public double μ = 1;
    public double mass = 1;
    public double r = 1;
    public Vector3Double position;
    public Vector3Double velocity;
    public Vector3Double acceleration;

    public static Vector3Double GetGravitationalForce(Body body, Body[] bodies)
    {
        var force = new Vector3Double(0, 0, 0);
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != body) {
                Vector3Double rad = bodies[i].position - body.position;
                force += (Constants.G * body.mass * bodies[i].mass / rad.sqrMagnitude) * rad.normalized;
            }
        return force;
    }

    public void OnValidate()
    {
        var centerPosition = FindObjectOfType<MainCamera>().centeredBody.position;
        if (μ != 0) mass = μ / Constants.G;
    }

    public void LateUpdate()
    {
        FindObjectOfType<MainCamera>().CenterBody(this);
        FindObjectOfType<Cone>().SetMaterial();
    }
}
