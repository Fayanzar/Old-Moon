using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[RequireComponent(typeof(Body))]
public class OrbitKepler : MonoBehaviour
{
    public Body orbitalParent;
    public double eccentricity;
    public double period;
    public double majorSemiaxis;
    public double orbitYRotation;
    public double inclinationX;
    public double inclinationZ;
    public double meanAnomalyInit;
    public bool inverseDirection = false;
    private double time;

    void Start()
    {
        time = 0;
    }

    [ExecuteInEditMode]
    void OnValidate()
    {
        time = 0;
        Func<double, double> KeplerFunc = E => E - eccentricity * Math.Sin(E) - meanAnomalyInit;
        Func<double, double> KeplerFuncD = E => 1 - eccentricity * Math.Cos(E);
        var E = Newton.Solve(KeplerFunc, KeplerFuncD, meanAnomalyInit);

        var minorSemiaxis = Math.Sqrt(1 - eccentricity * eccentricity) * majorSemiaxis;

        var x = (Math.Cos(E) - eccentricity) * majorSemiaxis;
        var z = minorSemiaxis * Math.Sin(E) * (inverseDirection ? -1 : 1);

        var initialVelocity = 2 * majorSemiaxis * Math.PI / (period * (1 - eccentricity));
        var initVelZ = Math.Sqrt(1 - eccentricity * eccentricity) * Math.Cos(E) * initialVelocity * (inverseDirection ? -1 : 1);
        var initVelX = -Math.Sin(E) * initialVelocity;

        var RMat = Matrix.RotMatrix("Z", inclinationZ) * Matrix.RotMatrix("X", inclinationX) * Matrix.RotMatrix("Y", orbitYRotation);

        GetComponent<Body>().velocity = orbitalParent.velocity + RMat * new Vector3Double(initVelX, 0, initVelZ);
        GetComponent<Body>().position = orbitalParent.position + RMat * new Vector3Double(x, 0, z);
    }

    // Update is called once per frame
    void Update()
    {
        var mainCamera = FindObjectOfType<MainCamera>();
        var simSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;
        var dt = simSpeed * (double)Time.deltaTime;
        time += dt;
        time -= (time > period) ? period : 0;

        var meanAnomaly = 2 * Math.PI / period * time;
        Func<double, double> KeplerFunc = E => E - eccentricity * Math.Sin(E) - meanAnomaly;
        Func<double, double> KeplerFuncD = E => 1 - eccentricity * Math.Cos(E);
        var E = Newton.Solve(KeplerFunc, KeplerFuncD, meanAnomaly);

        var minorSemiaxis = Math.Sqrt(1 - eccentricity * eccentricity) * majorSemiaxis;

        var x = (Math.Cos(E) - eccentricity) * majorSemiaxis;
        var z = minorSemiaxis * Math.Sin(E) * (inverseDirection ? -1 : 1);

        var V = 2 * majorSemiaxis * Math.PI / (period * (1 - eccentricity));
        var VZ = Math.Sqrt(1 - eccentricity * eccentricity) * Math.Cos(E) * V * (inverseDirection ? -1 : 1);
        var VX = -Math.Sin(E) * V;

        var RMat = Matrix.RotMatrix("Z", inclinationZ) * Matrix.RotMatrix("X", inclinationX) * Matrix.RotMatrix("Y", orbitYRotation);

        GetComponent<Body>().velocity = orbitalParent.velocity + RMat * new Vector3Double(VX, 0, VZ);
        GetComponent<Body>().position = orbitalParent.position + RMat * new Vector3Double(x, 0, z);
    }
}
