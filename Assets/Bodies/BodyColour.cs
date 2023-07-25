using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[ExecuteInEditMode]
public class BodyColour : MonoBehaviour
{
    public int temperature;
    public float intensity = 1f;

    void OnValidate()
    {
        var colour = TempToColour(temperature);
        GetComponent<Renderer>().sharedMaterial.SetColor("_Color", colour * intensity);
    }

    Color TempToColour(int T)
    {
        double x = 1, y = 1;

        if (1667 <= T && T < 4000)
            x = -0.2661239 * 1e9 * Math.Pow(T, -3) - 0.2343589 * 1e6 * Math.Pow(T, -2) + 0.8776956 * 1000 / T + 0.17991;
        else if (4000 <= T && T <= 25000)
            x = -3.0258469 * 1e9 * Math.Pow(T, -3) + 2.1070379 * 1e6 * Math.Pow(T, -2) + 0.2226347 * 1000 / T + 0.24039;

        if (1667 <= T && T < 2222)
            y = -1.1063814 * Math.Pow(x, 3) - 1.34811020 * Math.Pow(x, 2) + 2.18555832 * x - 0.20219683;
        else if (2222 <= T && T < 4000)
            y = -0.9549476 * Math.Pow(x, 3) - 1.37418593 * Math.Pow(x, 2) + 2.09137015 * x - 0.16748867;
        else if (4000 <= T && T <= 25000)
            y = 3.081758 * Math.Pow(x, 3) - 5.8733867 * Math.Pow(x, 2) + 3.75112997 * x - 0.37001483;

        double Y = 0.32902; // Illuminant D65 luminance
        double X = Y / Y * x;
        double Z = Y / Y * (1 - x - y);

        var rLinear = 3.2404542 * X - 1.5371385 * Y - 0.4985314 * Z;
        var gLinear = -0.9692660 * X + 1.8760108 * Y + 0.0415560 * Z;
        var bLinear = 0.0556434 * X - 0.2040259 * Y + 1.0572252 * Z;

        float r, g, b;
        if (rLinear <= 0.0031308) r = (float)(12.92 * rLinear);
        else r = (float)(1.055 * Math.Pow(rLinear, 1 / 2.4) - 0.055);

        if (gLinear <= 0.0031308) g = (float)(12.92 * gLinear);
        else g = (float)(1.055 * Math.Pow(gLinear, 1 / 2.4) - 0.055);

        if (bLinear <= 0.0031308) b = (float)(12.92 * bLinear);
        else b = (float)(1.055 * Math.Pow(bLinear, 1 / 2.4) - 0.055);

        return new Color(r, g, b, 1);
    }
}
