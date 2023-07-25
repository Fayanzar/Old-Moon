using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class Matrix
{
    private double[,] A;

    public int n { get => A.GetLength(0); }
    public int m { get => A.GetLength(1); }

    public Matrix(double[] vector)
    {
        var n = vector.Length;
        A = new double[n,1];
        for (int i = 0; i < n; i++)
            A[i,1] = vector[i];
    }

    public Matrix(double[,] matrix)
    {
        var n = matrix.GetLength(0);
        var m = matrix.GetLength(1);
        A = new double[n,m];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                A[i,j] = matrix[i,j];
    }

    public Matrix(Vector3 vector)
    {
        A = new double[3,1];
        A[0,0] = vector.x;
        A[1,0] = vector.y;
        A[2,0] = vector.z;
    }

    public Matrix(Vector3Double vector)
    {
        A = new double[3,1];
        A[0,0] = vector.x;
        A[1,0] = vector.y;
        A[2,0] = vector.z;
    }

    public static Matrix RotMatrix(string axis, double angle)
    {
        var c = Math.Cos(angle);
        var s = Math.Sin(angle);
        double[,] R;
        switch (axis.ToUpper()) {
            case "X": R = new double[3,3] { {1, 0,  0},
                                            {0, c, -s},
                                            {0, s,  c} };
            break;
            case "Y": R = new double[3,3] { {c,  0, s},
                                            {0,  1, 0},
                                            {-s, 0, c} };
            break;
            case "Z": R = new double[3,3] { {c, -s, 0},
                                            {s, c,  0},
                                            {0, 0,  1} };
            break;
            default: throw new System.Exception("Wrong axis");
        }
        return new Matrix(R);
    }

    public void SetValue(int i, int j, double v) { A[i,j] = v; }

    public double GetValue(int i, int j) { return A[i,j]; }

    public double this[int i, int j]
    {
        get => GetValue(i, j);
        set => SetValue(i, j, value);
    }

    public static Matrix operator +(Matrix a, Matrix b)
    {
        if (a.n != b.n || a.m != b.m)
            throw new System.Exception("Matrix dimensions do not match");

        double[,] c = new double[a.n, a.m];
        for (int i = 0; i < a.n; i++)
            for (int j = 0; j < a.m; j++)
                c[i,j] = a[i,j] + b[i,j];
        return new Matrix(c);
    }

    public static Matrix operator -(Matrix a)
    {
        double[,] b = new double[a.n, a.m];
        for (int i = 0; i < a.n; i++)
            for (int j = 0; j < a.m; j++)
                b[i,j] = -a[i,j];
        return new Matrix(b);
    }

    public static Matrix operator -(Matrix a, Matrix b) { return a + (-b); }

    public static Matrix operator *(Matrix a, Matrix b)
    {
        if (a.m != b.n)
            throw new System.Exception("Matrix dimensions do not match");

        double[,] c = new double[a.n, b.m];
        for (int i = 0; i < a.n; i++)
            for (int j = 0; j < b.m; j++)
            {
                double s = 0.0;
                for (int k = 0; k < a.m; k++)
                    s += a[i,k] * b[k,j];
                c[i,j] = s;
            }
        return new Matrix(c);
    }

    public static Vector3Double operator *(Matrix a, Vector3Double b)
    {
        var m = a * new Matrix(b);
        return new Vector3Double(m.A[0,0], m.A[1,0], m.A[2,0]);
    }

    public override string ToString()
    {
        string s = "";
        for (int i = 0; i < n; i++)
        {
            s += "(";
            for (int j = 0; j < m; j++)
                s += A[i,j] + ", ";
            s += ")\n";
        }
        return s;
    }

    public double[] ToVector()
    {
        var n = this.n;
        double[] v = new double[n];
        for (int i = 0; i < n; i++)
            v[i] = A[n,1];
        return v;
    }
}
