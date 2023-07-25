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
    [ExecuteInEditMode]
    void OnValidate()
    {
        var axisR = Quaternion.AngleAxis(tilt, Vector3.right);
        axis = axisR * Vector3.up;
        if (inverseRotation) axis *= -1;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, axisR, 180);
    }

    // Update is called once per frame
    void Update()
    {
        var mainCamera = FindObjectOfType<MainCamera>();
        var simSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;
        var dt = simSpeed * (double)Time.deltaTime;
        var angle = 360 * dt / period;
        var R = Quaternion.AngleAxis((float)angle, axis);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, R * transform.rotation, 360);
    }
}
