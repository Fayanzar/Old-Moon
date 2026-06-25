using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public struct BodyStruct
{
    public Body body;
    public bool isPhysical;
}

public class Solver : MonoBehaviour
{
    public BodyStruct[] bodies;
    public double CurrentTime  { get; protected set; }
    public double CurrentDt    { get; protected set; }
    public double CurrentSimDt { get; protected set; }
    public int StepsPerTick    { get; protected set; }

    private Body[] _allBodies;
    private Body[] _physBodies;
    private Body[] _nonPhysBodies;

    public Body[] AllBodies => _allBodies;
    protected Body[] PhysBodies => _physBodies;
    protected Body[] NonPhysBodies => _nonPhysBodies;

    private int trailTickCounter;

    public static event Action OnTimePassed;
    public static event Action<double> OnTickPassed;
    public static event Action<double> OnStepPassed;

    protected virtual void FixedUpdate()
    {
        OnTickPassed?.Invoke(CurrentSimDt);
    }

    void OnEnable()
    {
        _allBodies = bodies.Select(b => b.body).ToArray();
        _physBodies = bodies.Where(b => b.isPhysical).Select(b => b.body).ToArray();
        _nonPhysBodies = bodies.Where(b => !b.isPhysical).Select(b => b.body).ToArray();
    }

    protected virtual void Start()
    {
        trailTickCounter = -1;
    }

    protected virtual void Update()
    {
        if ((int)CurrentTime / 43200 > trailTickCounter)
        {
            trailTickCounter = (int)CurrentTime / 43200;
            OnTimePassed?.Invoke();
        }
    }

    public virtual void InitialDerivatives()
    {

    }

    public virtual void Step()
    {
        OnStepPassed?.Invoke(CurrentDt);
    }

    public virtual void ShiftBodies(Vector3Double delta)
    {
        foreach (var body in bodies)
            body.body.position += delta;
    }
}
