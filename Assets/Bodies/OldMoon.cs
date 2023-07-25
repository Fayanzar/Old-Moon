using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class OldMoon : MonoBehaviour
{
    public double angularDiameter = 0.009;
    public double earthDistance = 100000;
    public Body earth;
    public double deviationAmplitude;
    public double period = 1;
    public Barycenter barycenter;
    public double time = 0;

    void Start()
    {
        time = 0;
    }

    [ExecuteInEditMode]
    void OnValidate()
    {
        var dir = (earth.position - barycenter.position).normalized;
        GetComponent<Body>().position = dir * earthDistance + earth.position;
        var r = (earthDistance - earth.r) * Math.Sqrt((1 - Math.Cos(angularDiameter)) / 2);
        GetComponent<Body>().r = r;
    }

    // Update is called once per frame
    void Update()
    {
        var dir = (earth.position - barycenter.position).normalized;
        GetComponent<Body>().position = dir * earthDistance + earth.position;
        var deviationDir = Vector3Double.cross(dir, new Vector3Double(0, 1, 0)).normalized;
        GetComponent<Body>().position += deviationDir * Math.Sin(time / period * 2 * Math.PI) * deviationAmplitude;

        var mainCamera = FindObjectOfType<MainCamera>();
        var simSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;
        var dt = simSpeed * (double)Time.deltaTime;
        time += dt;
        time -= time > period ? period : 0;
    }
}
