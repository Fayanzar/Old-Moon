using UnityEngine;

public class Verlet : MonoBehaviour
{
    public Body[] bodies;

    // Update is called once per frame
    void Update()
    {
        var n = bodies.Length;
        var dt = Time.deltaTime * 100000;
        Vector3Double[] newPositions = new Vector3Double[n];
        Vector3Double[] newVelocities = new Vector3Double[n];
        Vector3Double[] newAccelerations = new Vector3Double[n];
        for (int i = 0; i < n; i++)
        {
            newPositions[i] = bodies[i].position + dt * bodies[i].velocity + 0.5 * dt * dt * bodies[i].acceleration;
            newAccelerations[i] = Body.GetGravitationalForce(bodies[i], bodies) / bodies[i].mass;
            newVelocities[i] = bodies[i].velocity + (bodies[i].acceleration + newAccelerations[i]) * dt * 0.5;
        }
        for (int i = 0; i < n; i++)
        {
            bodies[i].position = newPositions[i];
            bodies[i].velocity = newVelocities[i];
            bodies[i].acceleration = newAccelerations[i];
        }
    }
}
