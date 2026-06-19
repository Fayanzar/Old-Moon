using System;
using UnityEngine;

[Serializable]
class BodyJSON
{
    public string name = "";
    public Vector3Double position;
}

class TimePosition
{
    public double time;
    public BodyJSON[] bodies;
}

public class PEFRL : Solver
{
    const double ξ = 0.1786178958448091;
    const double λ = -0.2123418310626054;
    const double χ = -0.06626458266981849;
    public double time = 0.0;
    private double oldTime = 0.0;

    void Start ()
    {
        oldTime = -1.0;
        time = 0.0;
    }

    // Update is called once per frame
    void Update()
    {
        var mainCamera = FindObjectOfType<MainCamera>();
        var simSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;
        var dt = simSpeed * (double)Time.deltaTime;
        var n = bodies.Length;
        var forces = new Vector3Double[n];

        // var timePos = new TimePosition
        // {
        //     time = time,
        //     bodies = new BodyJSON[n]
        // };
        // for (int i = 0; i < n; i++)
        // {
        //     var body = new BodyJSON
        //     {
        //         name = bodies[i].transform.name,
        //         position = bodies[i].position
        //     };
        //     timePos.bodies[i] = body;
        // }
        // if (oldTime != Math.Floor(time))
        // {
        //     //Debug.Log(JsonUtility.ToJson(timePos));
        //     oldTime = Math.Floor(time);
        // }

        // r1
        for (int i = 0; i < n; i++)
            bodies[i].position += bodies[i].velocity * ξ * dt;
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(bodies[i], bodies);
        // v1, r2
        for (int i = 0; i < n; i++) {
            bodies[i].velocity += (1 - 2 * λ) * dt / (2 * bodies[i].mass) * forces[i];
            bodies[i].position += bodies[i].velocity * χ * dt;
        }
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(bodies[i], bodies);
        // v2, r3
        for (int i = 0; i < n; i++) {
            bodies[i].velocity += λ * dt / bodies[i].mass * forces[i];
            bodies[i].position += bodies[i].velocity * (1 - 2 * (χ + ξ)) * dt;
        }
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(bodies[i], bodies);
        // v3, r4
        for (int i = 0; i < n; i++) {
            bodies[i].velocity += λ * dt / bodies[i].mass * forces[i];
            bodies[i].position += bodies[i].velocity * χ * dt;
        }
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(bodies[i], bodies);
        // v(t + dt), r(t + dt)
        for (int i = 0; i < n; i++) {
            bodies[i].velocity += (1 - 2 * λ) * dt / (2 * bodies[i].mass) * forces[i];
            bodies[i].position += bodies[i].velocity * ξ * dt;
        }
        for (int i = 0; i < n; i++)
            bodies[i].acceleration = Body.GetGravitationalForce(bodies[i], bodies) / bodies[i].mass;

        time += dt / Constants.day;
    }
}
