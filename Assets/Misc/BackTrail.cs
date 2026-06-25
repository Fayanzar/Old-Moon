using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BackTrail : Trail
{
    public Trail mainTrail;

    void OnEnable()
    {
        Solver.OnTimePassed += Sample;
    }

    void OnDisable()
    {
        Solver.OnTimePassed -= Sample;
    }

    protected override void Update()
    {
        if (Body != null && mainTrail != null)
        {
            AgePoints();
            Draw();
        }
    }

    protected override void Sample()
    {
        if (mainTrail != null && mainTrail.Count != 0 && mainTrail.Count == mainTrail.capacity)
        {
            var pos = mainTrail.TrailPoints[mainTrail.Tail];
            trailPoints[head] = pos;

            var time = mainTrail.CpuPoints[mainTrail.Tail].w;

            var centeredPos = mainCamera.centeredBody.position;
            var samplePos = (Vector3)((pos - centeredPos) * mainCamera.scale);
            cpuPoints[head] = new Vector4(samplePos.x, samplePos.y, samplePos.z, time);

            head  = (head + 1) % capacity;
            count = Mathf.Min(count + 1, capacity);
        }
    }

    protected override void AgePoints()
    {
        var pos = mainTrail.TrailPoints[mainTrail.Tail];
        trailPoints[(head - 1 + capacity) % capacity] = pos;
        base.AgePoints();
    }
}
