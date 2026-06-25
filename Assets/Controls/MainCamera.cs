using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MainCamera : MonoBehaviour
{
    public double scale = 1e-7;
    public double speed = 1;
    public Constants.TimeUnit timeUnit;
    public Body centeredBody;

    [Header("Movement")]
    [Tooltip("Starting speed in units/second when a key is first pressed.")]
    public float baseSpeed = 5.0f;

    [Tooltip("Maximum speed the acceleration can reach.")]
    public float maxSpeed = 200.0f;

    [Tooltip("How quickly speed ramps up the longer a direction is held (units/sec^2 multiplier).")]
    public float acceleration = 8.0f;

    [Tooltip("How quickly speed resets back to base once movement stops.")]
    public float deceleration = 12.0f;

    [Tooltip("Multiplier applied while holding Shift.")]
    public float shiftMultiplier = 4.0f;

    [Header("Drift (coasting after release)")]
    [Tooltip("How long the camera keeps drifting after all movement keys are released, in seconds. " +
             "0 = stops instantly (no drift).")]
    public float driftTime = 0.4f;

    [Tooltip("Curve describing how velocity decays over driftTime. " +
             "X axis = normalized time (0 = moment of release, 1 = driftTime elapsed), " +
             "Y axis = velocity multiplier (should start at 1 and ease to 0).")]
    public AnimationCurve driftFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Look")]
    [Tooltip("Mouse look sensitivity.")]
    public float lookSensitivity = 2.0f;

    [Tooltip("Require holding the right mouse button to look around (Scene-view style). " +
             "If false, the cursor is locked and look is always active (FPS style).")]
    public bool requireRightMouseButton = true;


    private double lastFixedTime;

    private double targetScale = 1e-7;
    private float scaleVelocity = 0f;

    private float currentSpeed;
    private float heldTime;        // how long movement keys have been continuously held
    private float yaw;
    private float pitch;
    //private bool  isLooking;

    // Drift / coasting state
    private Vector3 lastMoveDirWorld;  // last normalized world-space move direction
    private float   lastMoveSpeed;     // speed at the instant of release (incl. shift mult)
    private float   driftElapsed;      // time since movement stopped
    private bool    wasMovingLastFrame;

    public float Yaw { get => yaw; set => yaw = value; }
    public float Pitch { get => pitch; set => pitch = value; }

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw   = angles.y;
        pitch = angles.x;

        currentSpeed = baseSpeed;
    }

    void OnValidate()
    {
        CenterBodies();
    }

    void FixedUpdate()
    {
        lastFixedTime = Time.timeAsDouble;
    }

    // Update is called once per frame
    void Update()
    {
        HandleLook();
        HandleMovement();

        float mouseScale = Input.GetAxis("Mouse ScrollWheel");
        targetScale *= 1 + mouseScale;
        scale = Mathf.SmoothDamp((float)scale, (float)targetScale, ref scaleVelocity, 0.15f);
    }

    private void HandleLook()
    {
        bool lookActive = !requireRightMouseButton || Input.GetMouseButton(1);

        // Manage cursor lock/visibility when using RMB-to-look (Scene-view style)
        if (requireRightMouseButton)
        {
            if (Input.GetMouseButtonDown(1))
            {
                //isLooking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                //isLooking = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else
        {
            // FPS-style: always locked
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            //isLooking = true;
        }

        if (!lookActive) return;

        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        yaw   += mouseX;
        pitch -= mouseY;
        pitch  = Mathf.Clamp(pitch, -89.9f, 89.9f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector3 inputDir = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    inputDir += Vector3.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  inputDir += Vector3.back;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  inputDir += Vector3.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputDir += Vector3.right;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))      inputDir += Vector3.up;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) inputDir += Vector3.down;

        bool isMoving = inputDir.sqrMagnitude > 0.0001f;

        // --- Shift boost --------------------------------------------------------
        float speedMultiplier = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            ? shiftMultiplier
            : 1.0f;

        // --- Acceleration ramp (only while actively holding a key) -------------
        if (isMoving)
        {
            heldTime += Time.deltaTime;

            // Speed grows the longer the key has been held, capped at maxSpeed.
            float targetSpeed = Mathf.Lerp(baseSpeed, maxSpeed,
                1.0f - Mathf.Exp(-acceleration * heldTime * 0.1f));

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed,
                acceleration * baseSpeed * Time.deltaTime);

            // Remember this direction/speed in case keys are released next frame
            Vector3 worldDir = transform.TransformDirection(inputDir.normalized);
            lastMoveDirWorld = worldDir;
            lastMoveSpeed    = currentSpeed * speedMultiplier;
            driftElapsed     = 0.0f;

            transform.position += worldDir * currentSpeed * speedMultiplier * Time.deltaTime;
        }
        else
        {
            // No key held this frame: reset the acceleration ramp so the next
            // press starts fresh, but DON'T touch transform.position yet —
            // that's handled by the drift coroutine below using the velocity
            // captured at the moment of release.
            heldTime = 0.0f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, baseSpeed,
                deceleration * baseSpeed * Time.deltaTime);

            ApplyDrift();
        }

        wasMovingLastFrame = isMoving;
    }

    private void ApplyDrift()
    {
        if (driftTime <= 0.0f) return;          // drift disabled
        if (lastMoveDirWorld.sqrMagnitude < 0.0001f) return; // never moved yet
        if (driftElapsed >= driftTime) return;  // drift already finished

        float t = driftElapsed / driftTime;
        float falloff = driftFalloff.Evaluate(Mathf.Clamp01(t));

        if (falloff <= 0.0001f)
        {
            driftElapsed = driftTime; // mark finished, stop evaluating further
            return;
        }

        transform.position += falloff * lastMoveSpeed * Time.deltaTime * lastMoveDirWorld;
        driftElapsed += Time.deltaTime;
    }


    public void CenterBody(Body body)
    {
        double alpha = (Time.timeAsDouble - lastFixedTime) / Time.fixedDeltaTime;
        alpha = Math.Clamp(alpha, 0.0, 1.0);

        var centerPosition = Vector3Double.Lerp(centeredBody.previousPosition, centeredBody.position, alpha);
        var bodyPosition = Vector3Double.Lerp(body.previousPosition, body.position, alpha);
        body.transform.position = (Vector3)((bodyPosition - centerPosition) * scale);
        body.transform.localScale = new Vector3(1, 1, 1) * (float)(body.r * 2 * scale);
    }

    public void CenterBodies()
    {
        var bodies = FindObjectsOfType<Body>();
        var centerPosition = centeredBody.position;
        for (int i = 0; i < bodies.Length; i++) {
            bodies[i].transform.position = (Vector3)((bodies[i].position - centerPosition) * scale);
            bodies[i].transform.localScale = new Vector3(1, 1, 1) * (float)(bodies[i].r * 2 * scale);
        }
    }
}
