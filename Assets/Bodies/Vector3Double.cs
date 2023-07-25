using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class Vector3Double
{
    public double x;
    public double y;
    public double z;
    public Vector3Double(double a, double b, double c)
    {
        x = a;
        y = b;
        z = c;
    }

    public Vector3Double(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public double sqrMagnitude
    {
        get => x * x + y * y + z * z;
    }

    public double magnitude
    {
        get => Math.Sqrt(sqrMagnitude);
    }

    public Vector3Double normalized
    {
        get { if (sqrMagnitude == 0) return this; else return this / magnitude; }
    }

    public static Vector3Double operator -(Vector3Double v)
    {
        return new Vector3Double(-v.x, -v.y, -v.z);
    }

    public static Vector3Double operator +(Vector3Double v1, Vector3Double v2)
    {
        return new Vector3Double(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
    }

    public static Vector3Double operator -(Vector3Double v1, Vector3Double v2)
    {
        return v1 + (-v2);
    }

    public static Vector3Double operator *(Vector3Double v, double a)
    {
        return new Vector3Double(v.x * a, v.y * a, v.z * a);
    }

    public static Vector3Double operator *(double a, Vector3Double v) { return v * a; }

    public static Vector3Double operator /(Vector3Double v, double a) { return v * (1 / a); }

    public static explicit operator Vector3(Vector3Double v) {
        return new Vector3((float)v.x, (float)v.y, (float)v.z);
    }

    public static Vector3Double cross(Vector3Double a, Vector3Double b)
    {
        double x = a.y * b.z - a.z * b.y;
        double y = a.z * b.x - a.x * b.z;
        double z = a.x * b.y - a.y * b.x;
        return new Vector3Double(x, y, z);
    }

    public override string ToString()
    {
        return $"Vector3Double({x}, {y}, {z})";
    }
}
