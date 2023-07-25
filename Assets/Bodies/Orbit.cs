using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Body))]
public class Orbit : MonoBehaviour
{
    public Body orbitalParent;
    public double eccentricity;
    public double period;
    public double majorSemiaxis;
    public double orbitYRotation;
    public double inclinationX;
    public double inclinationZ;
    public double meanAnomaly;
    public bool inverseDirection = false;

    // Update is called once per frame
    void OnValidate()
    {
        Func<double, double> KeplerFunc = E => E - eccentricity * Math.Sin(E) - meanAnomaly;
        Func<double, double> KeplerFuncD = E => 1 - eccentricity * Math.Cos(E);
        var E = Newton.Solve(KeplerFunc, KeplerFuncD, meanAnomaly);

        var M = orbitalParent.mass;
        var m = GetComponent<Body>().mass;
        var G = Constants.G;
        var μ = G * (M + m);

        majorSemiaxis = Math.Pow(period * period * μ / (4 * Math.PI * Math.PI), 1.0 / 3);
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
}
