using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class Cone : MonoBehaviour
{
    public Body[] suns;
    public Body occluder;
    public Body occludee;

    void OnValidate()
    {
        SetMaterial();
    }

    public void SetMaterial()
    {
        double outside = 0;
        foreach (Body sun in suns)
            outside += OutsideFraction(sun);
        occludee.GetComponent<Renderer>().sharedMaterial.SetFloat("_Outside", (float)outside);
    }

    double OutsideFraction(Body sun)
    {
        var r1 = occluder.r;
        var r2 = sun.r;
        var d = sun.position - occluder.position;
        var apex = occluder.position - d * r1 / (r2 - r1);
        var sina = (r2 - r1) / d.magnitude;
        var a2 = sina * sina / (1 - sina * sina);

        var dir = d.normalized;
        var φ = Math.Atan2(dir.y, dir.x);
        var pr = Math.Sqrt(dir.x * dir.x + dir.y * dir.y);
        var θ = Math.Atan2(pr, dir.z);

        var Rz = Matrix.RotMatrix("Z", -φ);
        var Ry = Matrix.RotMatrix("Y", -θ);
        var oc = Ry * Rz * (occludee.position - apex);

        var φ1 = Math.Atan(oc.y / oc.x);
        var R1z = Matrix.RotMatrix("Z", -φ1);
        oc = R1z * oc;

        var a = Math.Sqrt(a2);

        var n1 = new Vector3Double(1 / a, 0, -1).normalized;
        var n2 = new Vector3Double(-1 / a, 0, -1).normalized;

        var p1 = oc + n1 * occludee.r;
        var p2 = oc + n2 * occludee.r;

        var l1 = new Vector3Double(1, 0, 1 / a);
        var l2 = new Vector3Double(-1, 0, 1 / a);
        var l3 = new Vector3Double(-1, 0, -1 / a);
        var l4 = new Vector3Double(1, 0, -1 / a);

        double outside1 = 0.0, outside2 = 0.0;
        if (p1.x * l1.z - p1.z * l1.x > 0 && p1.x * l4.z - p1.z * l4.x < 0)
            outside1 = Math.Abs(p1.x / a - p1.z) / Math.Sqrt(1 + 1 / a2) / occludee.r;
        if (p2.x * l2.z - p2.z * l2.x < 0 && p2.x * l3.z - p2.z * l3.x > 0)
            outside2 = Math.Abs(p2.x / a + p2.z) / Math.Sqrt(1 + 1 / a2) / occludee.r;
        return outside1 + outside2;
    }
}
