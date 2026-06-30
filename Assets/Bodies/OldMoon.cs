using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class OldMoon : Body
{
    public double angularDiameter = 0.009;
    public double earthDistance = 100000;
    public Body earth;
    public double deviationAmplitude;
    public double period = 1;
    public Barycenter barycenter;
    public double time = 0;

    protected override void Start()
    {
        base.Start();
        time = 0;
    }

    public override void OnValidate()
    {
        base.OnValidate();
        var dir = (earth.position - barycenter.position).normalized;
        position = dir * earthDistance + earth.position;
        var r = (earthDistance - earth.r) * Math.Sqrt((1 - Math.Cos(angularDiameter)) / 2);
        this.r = r;
    }

    public override void Move(double dt)
    {
        var dir = (earth.position - barycenter.position).normalized;
        position = dir * earthDistance + earth.position;
        var deviationDir = Vector3Double.cross(dir, new Vector3Double(0, 1, 0)).normalized;
        position += deviationDir * Math.Sin(time / period * 2 * Math.PI) * deviationAmplitude;

        time += dt;
        time -= time > period ? period : 0;

        FindFirstObjectByType<Cone>().SetMaterial();
    }
}
