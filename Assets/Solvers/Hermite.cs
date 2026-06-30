using UnityEngine;
using System;
using Unity.Collections;

/// 4th-order Hermite integrator with adaptive timestep.
///
/// Algorithm outline per step:
///   1. Save current state (pos, vel, acc, jerk) for all bodies.
///   2. Predict pos/vel at t+dt using the Hermite predictor (3rd-order in pos).
///   3. Evaluate new acc/jerk at the predicted state.
///   4. Correct pos/vel using the Hermite corrector (4th-order).
///   5. Estimate the local truncation error and adjust dt for the next step.
///
/// Timestep control uses the classical Aarseth criterion:
///   dt_new = eta * sqrt( |a||a''| + |j|^2 ) / ( |j||j'| + |a'''|^2 )^(1/2)  (see below)
/// A simpler but robust surrogate is used here:
///   dt_new = eta * ( |a| / |j| )^(1/2)
/// combined with a step-rejection scheme based on the corrector–predictor difference.

public class Hermite : Solver
{
        // -------------------------------------------------------------------------
    // Inspector-tunable parameters
    // -------------------------------------------------------------------------

    [Tooltip("Dimensionless accuracy parameter (0.01–0.04 is typical).")]
    public double eta = 0.02;

    [Tooltip("Initial timestep in seconds.")]
    public double initialDt = 1.0;

    [Tooltip("Hard minimum timestep (prevents infinite loops).")]
    public double minDt = 1e-6;

    [Tooltip("Hard maximum timestep.")]
    public double maxDt = 1e2;

    [Tooltip("Maximum factor by which dt may grow in one step.")]
    public double dtGrowthLimit = 2.0;

    [Tooltip("Safety factor applied to the Aarseth timestep estimate.")]
    public double safetyFactor = 0.9;

    [Tooltip("Error tolerance for step rejection (relative).")]
    public double errorTolerance = 1e-3;

    [Tooltip("Maximum individual steps per FixedUpdate (prevents frame freezes).")]
    public int maxStepsPerFrame = 1000;

    // Saved state at beginning of step
    private NativeArray<Vector3Double> pos0, vel0, acc0, jerk0;
    // Predicted state
    private NativeArray<Vector3Double> posPred, velPred;
    // New derivatives at predicted state
    private NativeArray<Vector3Double> accNew, jerkNew;

    //Parallel front buffer arrays
    private NativeArray<Vector3Double> posPrev;
    private NativeArray<double> mass;

    // The simulation time at which each body last took a step
    private NativeArray<double> lastStepTime;
    // Each body's current individual timestep
    private NativeArray<double> dt;
    // Absolute time at which each body is next due to step
    private NativeArray<double> nextStepTime;

    // Min-heap for efficiently finding the next body to step
    // (index into bodies[])
    private IndexedMinHeap heap;


    private int n;
    private int recenterTicks;

    protected override void Start()
    {
        base.Start();
        recenterTicks = 0;
        n = PhysBodies.Length;

        CurrentDt    = initialDt;
        CurrentSimDt = initialDt;
        CurrentTime = 0.0;
        StepsPerTick = 1;
        AllocateScratch();
        InitialDerivatives();
        InitialTimesteps();
        BuildHeap();
    }

    protected override void FixedUpdate()
    {
        var mainCamera = FindFirstObjectByType<MainCamera>();
        var simulationSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;

        double targetSpeed = simulationSpeed * Time.fixedDeltaTime;
        double simTimeTarget = CurrentTime + targetSpeed;

        // Safety cap: never run more than this many steps per frame
        // (prevents a freeze if something drives dt very small)

        int steps = 0;
        var prevTime = CurrentTime;

        while (CurrentTime < simTimeTarget && steps < maxStepsPerFrame)
        {
            Step();
            steps++;
        }

        StepsPerTick = steps;
        CurrentSimDt = CurrentTime - prevTime;

        for (int i = 0; i < n; i++)
        {
            PhysBodies[i].previousPosition = posPrev[i];
            PhysBodies[i].position = pos0[i];
        }

        foreach (Body body in NonPhysBodies) {
            body.previousPosition = body.position;
            body.Move(CurrentSimDt);
        }

        NativeArray<Vector3Double>.Copy(pos0, posPrev, n);
        base.FixedUpdate();

        if (recenterTicks / 1000 > 1)
        {
            recenterTicks -= 100;
            var barycenter = FindFirstObjectByType<Barycenter>();
            if (barycenter != null && barycenter.isStatic)
            {
                var delta = barycenter.startPosition - barycenter.position;
                ShiftBodies(delta);
            }
        }

        recenterTicks++;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// Advance all bodies by one adaptive Hermite step.
    public override void Step()
    {
        // --- Pick the body with the smallest nextStepTime -------------------
        int idx = heap.PeekMin();
        double tTarget = nextStepTime[idx];   // time we are stepping body idx TO
        bool accepted = false;

        while (!accepted)
        {
            double dti = dt[idx];

            // --- 1. Predict (3rd-order Taylor) ----------------------------
            PredictAll(CurrentTime);

            // --- 2. Evaluate derivatives at predicted positions -----------
            EvaluateDerivativesForOne(idx);

            // --- 3. Correct (4th-order Hermite) ---------------------------
            CorrectOne(idx, dti);

            // --- 4. Estimate error & decide accept / reject ---------------
            double error = EstimateErrorOne(idx, dti);

            if (error <= errorTolerance || dti <= minDt)
            {
                // Accept: commit corrected state into bodies[idx]
                CommitOne(idx, tTarget);

                // Update dt for next step
                double dtNext = NewTimestep(idx, dti);
                dt[idx]           = dtNext;
                lastStepTime[idx] = tTarget;
                nextStepTime[idx] = tTarget + dtNext;

                // Update heap and system time
                heap.UpdateMin(idx, nextStepTime[idx]);
                CurrentTime = nextStepTime[heap.PeekMin()];

                base.Step();
                CurrentDt = dtNext;
                accepted = true;
            }
            else
            {
                // Reject: shrink dt and retry (don't move tTarget, just reduce dti)
                double dtNext = dti * Math.Max(0.5,
                    safetyFactor * Math.Pow(errorTolerance / error, 0.2));
                dtNext = Math.Max(dtNext, minDt);
                dt[idx]          = dtNext;
                nextStepTime[idx] = lastStepTime[idx] + dtNext;
                tTarget          = nextStepTime[idx];

                // Rebuild heap entry since priority changed
                heap.UpdateMin(idx, nextStepTime[idx]);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------
    private void AllocateScratch()
    {
        pos0 = new(n,  Allocator.Persistent);
        vel0 = new(n,  Allocator.Persistent);
        acc0 = new(n,  Allocator.Persistent);
        jerk0 = new(n,  Allocator.Persistent);

        posPred  = new(n,  Allocator.Persistent);
        velPred  = new(n,  Allocator.Persistent);
        accNew   = new(n,  Allocator.Persistent);
        jerkNew  = new(n,  Allocator.Persistent);

        posPrev = new(n,  Allocator.Persistent);
        mass = new(n,  Allocator.Persistent);

        lastStepTime = new(n,  Allocator.Persistent);
        nextStepTime = new(n,  Allocator.Persistent);
        dt = new(n, Allocator.Persistent);

        for (int i = 0; i < n; i++)
        {
            pos0[i] = posPrev[i] = PhysBodies[i].position;
            vel0[i] = PhysBodies[i].velocity;
            acc0[i] = PhysBodies[i].acceleration;
            jerk0[i] = PhysBodies[i].jerk;
            mass[i] = PhysBodies[i].mass;
        }
    }

    private void InitialTimesteps()
    {
        for (int i = 0; i < n; i++)
        {
            double aMag = acc0[i].magnitude;
            double jMag = jerk0[i].magnitude;

            double dtEst = (jMag > 1e-30)
                ? eta * Math.Sqrt(aMag / jMag)
                : initialDt;

            dt[i]           = Math.Clamp(dtEst * safetyFactor, minDt, maxDt);
            lastStepTime[i] = 0.0;
            nextStepTime[i] = dt[i];
        }
    }

    private void BuildHeap()
    {
        heap = new IndexedMinHeap(n);
        for (int i = 0; i < n; i++)
            heap.Insert(i, nextStepTime[i]);
    }

    /// Compute initial accelerations and jerks so the first step has
    /// valid derivatives.  Call once before the first Step().
    public override void InitialDerivatives()
    {
        base.InitialDerivatives();
        // Zero accumulators
        for (int i = 0; i < n; i++)
        {
            acc0[i]  = Vector3Double.zero;
            jerk0[i] = Vector3Double.zero;
        }

        // Evaluate all pairs
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                Vector3Double rij = pos0[j] - pos0[i];
                Vector3Double vij = vel0[j] - vel0[i];

                double r2    = rij.sqrMagnitude;
                double r     = Math.Sqrt(r2);
                double r3    = r2 * r;
                double r5    = r3 * r2;
                double rdotv = Vector3Double.dot(rij, vij);

                Vector3Double aij  = rij * (Constants.G * mass[j] / r3);
                Vector3Double aji  = rij * (Constants.G * mass[i] / r3);
                acc0[i] += aij;
                acc0[j] -= aji;

                Vector3Double jij = (vij / r3 - rij * (3.0 * rdotv / r5)) * (Constants.G * mass[j]);
                Vector3Double jji = (vij / r3 - rij * (3.0 * rdotv / r5)) * (Constants.G * mass[i]);
                jerk0[i] += jij;
                jerk0[j] -= jji;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Hermite core
    // -------------------------------------------------------------------------

    /// Hermite predictor — 3rd-order Taylor expansion:
    ///   r_p = r0 + v0*dt + (1/2)*a0*dt^2 + (1/6)*j0*dt^3
    ///   v_p = v0 + a0*dt + (1/2)*j0*dt^2
    private void PredictAll(double tTarget)
    {
        for (int j = 0; j < n; j++)
        {
            double s  = tTarget - lastStepTime[j];   // time since j's last step
            double s2 = s  * s;
            double s3 = s2 * s;

            posPred[j] = pos0[j]
                       + vel0[j]  * s
                       + acc0[j]  * (0.5  * s2)
                       + jerk0[j] * (s3 / 6.0);

            velPred[j] = vel0[j]
                       + acc0[j]  * s
                       + jerk0[j] * (0.5 * s2);
        }
    }

    /// Hermite corrector — uses old and new acc/jerk to achieve 4th order:
    ///   v_c = v_p + (1/2)*(a0 + a1)*dt  -  (1/12)*(j1 - j0)*dt^2
    ///   r_c = r_p + (1/2)*(v0 + v_c)*dt -  (1/12)*(a1 - a0)*dt^2
    ///
    /// (Makino & Aarseth 1992 formulation)
    private void CorrectOne(int i, double dti)
    {
        double dt2 = dti * dti;

        Vector3Double velCorr =
              velPred[i]
            + (acc0[i]  + accNew[i])   * (0.5  * dti)
            - (jerkNew[i] - jerk0[i])  * (dt2 / 12.0);

        Vector3Double posCorr =
              posPred[i]
            + (vel0[i]  + velCorr)     * (0.5  * dti)
            - (accNew[i]  - acc0[i])   * (dt2 / 12.0);

        posPred[i] = posCorr;
        velPred[i] = velCorr;
    }

    /// Copy the corrected state (stored in posPred / velPred / accNew / jerkNew
    /// after Correct() runs) into the bodies array.
    private void CommitOne(int i, double tTarget)
    {
        pos0[i]  = posPred[i];
        vel0[i]  = velPred[i];
        acc0[i]  = accNew[i];
        jerk0[i] = jerkNew[i];
    }

    // -------------------------------------------------------------------------
    // Derivative evaluation
    // -------------------------------------------------------------------------

    /// Fills acc[] and jerk[] for a snapshot of positions/velocities.
    ///
    /// Jerk (da/dt) is computed analytically from the gravitational pair forces:
    ///   j_i = G * sum_{j≠i} m_j * [ (v_ij / r_ij^3)
    ///                                - 3*(r_ij · v_ij) / r_ij^5 * r_ij ]
    /// where r_ij = r_j - r_i,  v_ij = v_j - v_i.
    ///
    /// This avoids a finite-difference jerk estimate and keeps the full
    /// 4th-order accuracy of the corrector.
    private void EvaluateDerivativesForOne(int i)
    {
        accNew[i] = Vector3Double.zero;
        jerkNew[i] = Vector3Double.zero;

        for (int j = 0; j < n; j++)
        {
            if (j == i) continue;

            Vector3Double rij = posPred[j] - posPred[i];
            Vector3Double vij = velPred[j] - velPred[i];

            double r2    = rij.sqrMagnitude;
            double r     = Math.Sqrt(r2);
            double r3    = r2 * r;
            double r5    = r3 * r2;
            double rdotv = Vector3Double.dot(rij, vij);

            // Acceleration: a_i += G*m_j * r_ij / r^3
            accNew[i] += rij * (Constants.G * mass[j] / r3);

            // Jerk: da/dt_i += G*m_j * [ v_ij/r^3 - 3*(r·v)*r_ij/r^5 ]
            jerkNew[i] += (vij / r3 - rij * (3.0 * rdotv / r5)) * (Constants.G * mass[j]);
        }
    }

    // -------------------------------------------------------------------------
    // Adaptive timestep
    // -------------------------------------------------------------------------

    /// Aarseth (1985) individual timestep criterion:
    ///   dt = eta * sqrt( |a| / |j| )
    private double NewTimestep(int i, double dtOld)
    {
        double aMag = accNew[i].magnitude;
        double jMag = jerkNew[i].magnitude;

        double dtNew = (jMag > 1e-30)
            ? eta * Math.Sqrt(aMag / jMag)
            : dtOld * dtGrowthLimit;

        dtNew *= safetyFactor;
        dtNew  = Math.Min(dtNew, dtOld * dtGrowthLimit);
        return Math.Clamp(dtNew, minDt, maxDt);
    }

    /// Error estimate based on the 5th-order term of the Hermite corrector.
    /// The leading-order error in position after correction is proportional to:
    ///   delta = (1/120) * |a^(3)| * dt^5
    /// A practical surrogate is the predictor–corrector difference scaled by
    /// the corrected magnitude (relative error per step).
    private double EstimateErrorOne(int i, double dti)
    {
        // Compare corrected position against the raw predictor output.
        // The difference is O(dt^4) and serves as a local truncation error proxy.
        Vector3Double predicted =
              pos0[i]
            + vel0[i]  * dti
            + acc0[i]  * (0.5  * dti * dti)
            + jerk0[i] * (dti * dti * dti / 6.0);

        double delta = (posPred[i] - predicted).magnitude;
        double scale = Math.Max(posPred[i].magnitude, 1e-30);
        return delta / scale;
    }

    void OnDestroy()
    {
        mass.Dispose();

        pos0.Dispose();
        vel0.Dispose();
        acc0.Dispose();
        jerk0.Dispose();

        posPrev.Dispose();

        posPred.Dispose();
        velPred.Dispose();
        accNew.Dispose();
        jerkNew.Dispose();

        lastStepTime.Dispose();
        nextStepTime.Dispose();
        dt.Dispose();
    }

    public override void ShiftBodies(Vector3Double delta)
    {
        base.ShiftBodies(delta);
        for (int i = 0; i < n; i++) {
            posPrev[i] += delta;
            pos0[i] += delta;
        }
    }
}

// =============================================================================
// Indexed min-heap
// Stores (bodyIndex, priority=nextStepTime) pairs.
// Supports O(log n) insert, peek-min, and update-by-index.
// =============================================================================
internal class IndexedMinHeap
{
    private NativeArray<int>    heapToBody;   // heap position -> body index
    private NativeArray<int>    bodyToHeap;   // body index    -> heap position
    private NativeArray<double> priority;     // body index    -> priority value
    private int size;

    public IndexedMinHeap(int capacity)
    {
        heapToBody = new(capacity,  Allocator.Persistent);
        bodyToHeap = new(capacity,  Allocator.Persistent);
        priority   = new(capacity,  Allocator.Persistent);
        size       = 0;

        for (int i = 0; i < capacity; i++) bodyToHeap[i] = -1;
    }

    ~IndexedMinHeap()
    {
        heapToBody.Dispose();
        bodyToHeap.Dispose();
        priority.Dispose();
    }

    public void Insert(int bodyIdx, double prio)
    {
        int pos        = size++;
        heapToBody[pos] = bodyIdx;
        bodyToHeap[bodyIdx] = pos;
        priority[bodyIdx]   = prio;
        BubbleUp(pos);
    }

    /// <summary>Returns the body index with the smallest nextStepTime.</summary>
    public int PeekMin() => heapToBody[0];

    /// <summary>Update the priority of a body already in the heap.</summary>
    public void UpdateMin(int bodyIdx, double newPrio)
    {
        priority[bodyIdx] = newPrio;
        int pos = bodyToHeap[bodyIdx];
        BubbleUp(pos);
        BubbleDown(pos);
    }

    private void BubbleUp(int pos)
    {
        while (pos > 0)
        {
            int parent = (pos - 1) / 2;
            if (priority[heapToBody[parent]] <= priority[heapToBody[pos]]) break;
            Swap(pos, parent);
            pos = parent;
        }
    }

    private void BubbleDown(int pos)
    {
        while (true)
        {
            int left  = 2 * pos + 1;
            int right = 2 * pos + 2;
            int smallest = pos;

            if (left  < size && priority[heapToBody[left]]  < priority[heapToBody[smallest]])
                smallest = left;
            if (right < size && priority[heapToBody[right]] < priority[heapToBody[smallest]])
                smallest = right;

            if (smallest == pos) break;
            Swap(pos, smallest);
            pos = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        int ba = heapToBody[a], bb = heapToBody[b];
        heapToBody[a] = bb; heapToBody[b] = ba;
        bodyToHeap[ba] = b; bodyToHeap[bb] = a;
    }
}
