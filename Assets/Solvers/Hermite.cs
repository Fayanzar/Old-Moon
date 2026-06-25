using UnityEngine;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

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

    // Saved state at beginning of step
    private NativeArray<Vector3Double> pos0, vel0, acc0, jerk0;
    // Predicted state
    private NativeArray<Vector3Double> posPred, velPred;
    // New derivatives at predicted state
    private NativeArray<Vector3Double> accNew, jerkNew;
    private double targetSpeed;

    //Parallel front buffer arrays
    private NativeArray<Vector3Double> posPrev, pos1, vel1, acc1, jerk1;
    private NativeArray<double> mass;

    private int n;

    protected override void Start()
    {
        base.Start();
        n = PhysBodies.Length;

        // foreach (Body body in AllBodies)
        //     body.previousPosition = body.position;

        CurrentDt    = initialDt;
        CurrentSimDt = initialDt;
        CurrentTime = 0.0;
        StepsPerTick = 1;
        AllocateScratch();
        InitialDerivatives();
    }

    protected override void FixedUpdate()
    {
        var mainCamera = FindObjectOfType<MainCamera>();
        var simulationSpeed = Constants.constDict[mainCamera.timeUnit] * mainCamera.speed;
        double simTimeToAdvance = simulationSpeed * Time.fixedDeltaTime;
        targetSpeed = simTimeToAdvance;
        double simTimeCovered   = 0.0;
        // Safety cap: never run more than this many steps per frame
        // (prevents a freeze if something drives dt very small)
        const int maxStepsPerFrame = 1000;
        int steps = 0;
        var prevTime = CurrentTime;

        while (simTimeCovered < simTimeToAdvance && steps < maxStepsPerFrame)
        {
            // Clamp the last sub-step so we don't overshoot
            double remaining = simTimeToAdvance - simTimeCovered;
            if (CurrentDt > remaining)
                CurrentDt = remaining;

            Step();
            simTimeCovered += CurrentDt; // CurrentDt was the dt actually used
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
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// Advance all bodies by one adaptive Hermite step.
    public override void Step()
    {
        bool accepted = false;

        while (!accepted)
        {
            double dt = CurrentDt;

            // --- 1. Predict (3rd-order Taylor) ----------------------------
            Predict(dt);

            // --- 2. Evaluate derivatives at predicted positions -----------
            EvaluateDerivatives(posPred, velPred, accNew, jerkNew);

            // --- 3. Correct (4th-order Hermite) ---------------------------
            Correct(dt);

            // --- 4. Estimate error & decide accept / reject ---------------
            double error = EstimateError(dt);

            if (error <= errorTolerance || dt <= minDt)
            {
                // Accept step: commit corrected state, advance time
                ApplyCorrected();
                CurrentTime += dt;
                accepted = true;

                // Compute next dt suggestion from Aarseth criterion
                double dtSuggested = AarsethTimestep();
                double dtNext = dtSuggested * safetyFactor;

                // Clamp growth and absolute bounds
                dtNext = Math.Min(dtNext, dt * dtGrowthLimit);
                dtNext = Math.Clamp(dtNext, minDt, Math.Min(maxDt, targetSpeed));

                base.Step();
                CurrentDt = dtNext;
            }
            else
            {
                // Reject step: shrink dt
                double dtNext = dt * Math.Max(0.5,
                    safetyFactor * Math.Pow(errorTolerance / error, 0.2));
                CurrentDt = Math.Max(dtNext, minDt);
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

        pos1 = new(n,  Allocator.Persistent);
        vel1 = new(n,  Allocator.Persistent);
        acc1 = new(n,  Allocator.Persistent);
        jerk1 = new(n,  Allocator.Persistent);

        posPrev = new(n,  Allocator.Persistent);
        mass = new(n,  Allocator.Persistent);

        for (int i = 0; i < n; i++)
        {
            pos0[i] = pos1[i] = posPrev[i] = PhysBodies[i].position;
            vel0[i] = vel1[i] = PhysBodies[i].velocity;
            acc0[i] = acc1[i] = PhysBodies[i].acceleration;
            jerk0[i] = jerk1[i] = PhysBodies[i].jerk;
            mass[i] = PhysBodies[i].mass;
        }
    }

    /// Compute initial accelerations and jerks so the first step has
    /// valid derivatives.  Call once before the first Step().
    public override void InitialDerivatives()
    {
        base.InitialDerivatives();
        EvaluateDerivatives(pos1, vel1, acc1, jerk1);
    }

    // -------------------------------------------------------------------------
    // Hermite core
    // -------------------------------------------------------------------------

    /// Hermite predictor — 3rd-order Taylor expansion:
    ///   r_p = r0 + v0*dt + (1/2)*a0*dt^2 + (1/6)*j0*dt^3
    ///   v_p = v0 + a0*dt + (1/2)*j0*dt^2
    private void Predict(double dt)
    {
        double dt2 = dt  * dt;
        double dt3 = dt2 * dt;

        for (int i = 0; i < n; i++)
        {
            posPred[i] = pos0[i]
                       + vel0[i]  * dt
                       + acc0[i]  * (0.5  * dt2)
                       + jerk0[i] * (dt3 / 6.0);

            velPred[i] = vel0[i]
                       + acc0[i]  * dt
                       + jerk0[i] * (0.5 * dt2);
        }
    }

    /// Hermite corrector — uses old and new acc/jerk to achieve 4th order:
    ///   v_c = v_p + (1/2)*(a0 + a1)*dt  -  (1/12)*(j1 - j0)*dt^2
    ///   r_c = r_p + (1/2)*(v0 + v_c)*dt -  (1/12)*(a1 - a0)*dt^2
    ///
    /// (Makino & Aarseth 1992 formulation)
    private void Correct(double dt)
    {
        double dt2 = dt * dt;

        for (int i = 0; i < n; i++)
        {
            // Corrected velocity
            Vector3Double velCorr =
                  velPred[i]
                + (acc0[i] + accNew[i])  * (0.5 * dt)
                - (jerkNew[i] - jerk0[i]) * (dt2 / 12.0);

            // Corrected position (use average of old and corrected velocity)
            Vector3Double posCorr =
                  posPred[i]
                + (vel0[i] + velCorr)    * (0.5 * dt)
                - (accNew[i] - acc0[i])  * (dt2 / 12.0);

            // Write back into scratch arrays (don't touch bodies[] yet so
            // EstimateError can compare against the predicted values)
            posPred[i] = posCorr;
            velPred[i] = velCorr;
        }
        // accNew / jerkNew already hold the new derivatives.
    }

    /// Copy the corrected state (stored in posPred / velPred / accNew / jerkNew
    /// after Correct() runs) into the bodies array.
    private void ApplyCorrected()
    {
        for (int i = 0; i < n; i++)
        {
            pos1[i]     = posPred[i];
            vel1[i]     = velPred[i];
            acc1[i]     = accNew[i];
            jerk1[i]    = jerkNew[i];
        }

        (pos0, pos1)   = (pos1, pos0);
        (vel0, vel1)   = (vel1, vel0);
        (acc0, acc1)   = (acc1, acc0);
        (jerk0, jerk1) = (jerk1, jerk0);
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

    [BurstCompile]
    struct DerivativeEvaluationJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3Double> position, velocity;

        [ReadOnly]
        public NativeArray<double> mass;

        public NativeArray<Vector3Double> acceleration, jerk;

        public int length;
        public void Execute(int i)
        {
            acceleration[i] = Body.GetGravForceArrays(position[i], mass[i], position, mass, i) / mass[i];
            for (int j = i + 1; j < length; j++)
            {
                Vector3Double rij = position[j]  - position[i];   // r_j - r_i
                Vector3Double vij = velocity[j]  - velocity[i];   // v_j - v_i

                double r2    = rij.sqrMagnitude;
                double r     = Math.Sqrt(r2);
                double r3    = r2 * r;
                double r5    = r3 * r2;

                double rdotv = Vector3Double.dot(rij, vij);

                // --- Jerk contribution (analytic time-derivative of acc) --
                // da/dt_i += G*m_j * [ v_ij/r^3 - 3*(r·v/r^5)*r_ij ]
                Vector3Double jij = (vij / r3) - (rij * (3.0 * rdotv / r5));
                jij *= Constants.G * mass[j];

                Vector3Double jji = (vij / r3) - (rij * (3.0 * rdotv / r5));
                jji *= Constants.G * mass[i];

                jerk[i]  += jij;
                jerk[j]  -= jji;
            }
        }
    }

    private void EvaluateDerivatives(
        NativeArray<Vector3Double> pos,
        NativeArray<Vector3Double> vel,
        NativeArray<Vector3Double> acc,
        NativeArray<Vector3Double> jerk)
    {
        for (int i = 0; i < n; i++)
        {
            jerk[i] = Vector3Double.zero;
        }
        var job = new DerivativeEvaluationJob()
        {
            position = pos,
            velocity = vel,
            acceleration = acc,
            jerk = jerk,

            length = n,
            mass = mass
        };
        JobHandle dependencyJobHandle = default;
        JobHandle derivativeJobHandle = job.ScheduleByRef(n, 32, dependencyJobHandle);
        derivativeJobHandle.Complete();
    }

    // -------------------------------------------------------------------------
    // Adaptive timestep
    // -------------------------------------------------------------------------

    /// Aarseth (1985) individual timestep criterion:
    ///   dt = eta * sqrt( |a| / |j| )
    ///
    /// Takes the minimum across all bodies (global shared timestep variant).
    private double AarsethTimestep()
    {
        double dtMin = Math.Min(targetSpeed, maxDt);

        for (int i = 0; i < n; i++)
        {
            double aMag = acc0[i].magnitude;
            double jMag = jerk0[i].magnitude;

            if (jMag < 1e-30) continue;   // body in free-fall with no tidal force

            double dtEst = eta * Math.Sqrt(aMag / jMag);
            if (dtEst < dtMin) dtMin = dtEst;
        }

        return Math.Clamp(dtMin, minDt, Math.Min(targetSpeed, maxDt));
    }

    /// Error estimate based on the 5th-order term of the Hermite corrector.
    /// The leading-order error in position after correction is proportional to:
    ///   delta = (1/120) * |a^(3)| * dt^5
    /// A practical surrogate is the predictor–corrector difference scaled by
    /// the corrected magnitude (relative error per step).
    private double EstimateError(double dt)
    {
        double maxErr = 0.0;

        for (int i = 0; i < n; i++)
        {
            // posPred[i] now holds the *corrected* position (after Correct()).
            // pos0[i] holds the saved position.  The truncation error of the
            // predictor (3rd order) vs corrector (4th order) gives a local
            // error estimate for the step.
            Vector3Double deltaPos = posPred[i] - (
                  pos0[i]
                + vel0[i]  * dt
                + acc0[i]  * (0.5  * dt * dt)
                + jerk0[i] * (dt * dt * dt / 6.0));

            double scale = Math.Max(posPred[i].magnitude, 1e-30);
            double err   = deltaPos.magnitude / scale;

            if (err > maxErr) maxErr = err;
        }

        return maxErr;
    }

    void OnDestroy()
    {
        mass.Dispose();

        pos0.Dispose();
        vel0.Dispose();
        acc0.Dispose();
        jerk0.Dispose();

        pos1.Dispose();
        vel1.Dispose();
        acc1.Dispose();
        jerk1.Dispose();

        posPrev.Dispose();

        posPred.Dispose();
        velPred.Dispose();
        accNew.Dispose();
        jerkNew.Dispose();
    }

    public override void ShiftBodies(Vector3Double delta)
    {
        base.ShiftBodies(delta);
        for (int i = 0; i < n; i++)
            pos0[i] += delta;
    }

}
