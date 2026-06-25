using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotation : MonoBehaviour
{
    public float tilt;
    public float period = Constants.day;
    public bool inverseRotation = false;
    Vector3 axis;
    // Start is called before the first frame update

    void OnValidate()
    {
        var axisR = Quaternion.AngleAxis(tilt, Vector3.right);
        axis = axisR * Vector3.up;
        if (inverseRotation) axis *= -1;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, axisR, 180);
    }

    void Start()
    {
        var axisR = Quaternion.AngleAxis(tilt, Vector3.right);
        axis = axisR * Vector3.up;
        if (inverseRotation) axis *= -1;
        var zeroRotation = new Quaternion(0, 0, 0, 1);
        transform.rotation = Quaternion.RotateTowards(zeroRotation, axisR, 180);
    }

    void OnEnable()
    {
        Solver.OnTickPassed += Rotat;
    }

    void OnDisable()
    {
        Solver.OnTickPassed -= Rotat;
    }

    void Rotat(double dt) // yes, it's not a typo
    {
        var angle = 360 * dt / period;
        var R = Quaternion.AngleAxis((float)angle, axis);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, R * transform.rotation, 360);
    }
}
