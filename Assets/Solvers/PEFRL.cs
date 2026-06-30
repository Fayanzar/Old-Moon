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

    // Update is called once per frame
    protected override void Update()
    {
        var mainCamera = FindFirstObjectByType<MainCamera>();
        var simSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;
        var dt = simSpeed * (double)Time.deltaTime;
        var n = PhysBodies.Length;
        var forces = new Vector3Double[n];

        // r1
        for (int i = 0; i < n; i++)
            PhysBodies[i].position += PhysBodies[i].velocity * ξ * dt;
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(PhysBodies[i], PhysBodies);
        // v1, r2
        for (int i = 0; i < n; i++) {
            PhysBodies[i].velocity += (1 - 2 * λ) * dt / (2 * PhysBodies[i].mass) * forces[i];
            PhysBodies[i].position += PhysBodies[i].velocity * χ * dt;
        }
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(PhysBodies[i], PhysBodies);
        // v2, r3
        for (int i = 0; i < n; i++) {
            PhysBodies[i].velocity += λ * dt / PhysBodies[i].mass * forces[i];
            PhysBodies[i].position += PhysBodies[i].velocity * (1 - 2 * (χ + ξ)) * dt;
        }
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(PhysBodies[i], PhysBodies);
        // v3, r4
        for (int i = 0; i < n; i++) {
            PhysBodies[i].velocity += λ * dt / PhysBodies[i].mass * forces[i];
            PhysBodies[i].position += PhysBodies[i].velocity * χ * dt;
        }
        for (int i = 0; i < n; i++)
            forces[i] = Body.GetGravitationalForce(PhysBodies[i], PhysBodies);
        // v(t + dt), r(t + dt)
        for (int i = 0; i < n; i++) {
            PhysBodies[i].velocity += (1 - 2 * λ) * dt / (2 * PhysBodies[i].mass) * forces[i];
            PhysBodies[i].position += PhysBodies[i].velocity * ξ * dt;
        }
        for (int i = 0; i < n; i++)
            PhysBodies[i].acceleration = Body.GetGravitationalForce(PhysBodies[i], PhysBodies) / PhysBodies[i].mass;
    }
}
