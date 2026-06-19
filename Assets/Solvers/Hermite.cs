using UnityEngine;
using System;

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
    private Vector3Double[] pos0, vel0, acc0, jerk0;
    // Predicted state
    private Vector3Double[] posPred, velPred;
    // New derivatives at predicted state
    private Vector3Double[] accNew, jerkNew;
    private double targetSpeed;

    void Start()
    {
        foreach (Body body in bodies)
            body.previousPosition = body.position;
        CurrentDt    = initialDt;
        CurrentSimDt = initialDt;
        CurrentTime = 0.0;
        StepsPerTick = 1;
        AllocateScratch();
        InitialDerivatives();
    }

    private void FixedUpdate()
    {
        FindObjectOfType<Cone>().SetMaterial();
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
        foreach (Body body in bodies)
            body.previousPosition = body.position;

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
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// Advance all bodies by one adaptive Hermite step.
    public void Step()
    {
        bool accepted = false;

        while (!accepted)
        {
            double dt = CurrentDt;

            // --- 1. Save state -------------------------------------------
            SaveState();

            // --- 2. Predict (3rd-order Taylor) ----------------------------
            Predict(dt);

            // --- 3. Evaluate derivatives at predicted positions -----------
            EvaluateDerivatives(posPred, velPred, accNew, jerkNew);

            // --- 4. Correct (4th-order Hermite) ---------------------------
            Correct(dt);

            // --- 5. Estimate error & decide accept / reject ---------------
            double error = EstimateError(dt);

            if (error <= errorTolerance || dt <= minDt)
            {
                // Accept step: commit corrected state, advance time
                ApplyCorrected(dt);
                CurrentTime += dt;
                accepted = true;

                // Compute next dt suggestion from Aarseth criterion
                double dtSuggested = AarsethTimestep();
                double dtNext = dtSuggested * safetyFactor;

                // Clamp growth and absolute bounds
                dtNext = Math.Min(dtNext, dt * dtGrowthLimit);
                dtNext = Math.Clamp(dtNext, minDt, Math.Min(maxDt, targetSpeed));
                CurrentDt = dtNext;
            }
            else
            {
                // Reject step: restore state and shrink dt
                RestoreState();
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
        int n = bodies.Length;
        pos0     = new Vector3Double[n]; vel0     = new Vector3Double[n];
        acc0     = new Vector3Double[n]; jerk0    = new Vector3Double[n];
        posPred  = new Vector3Double[n]; velPred  = new Vector3Double[n];
        accNew   = new Vector3Double[n]; jerkNew  = new Vector3Double[n];
    }

    /// Compute initial accelerations and jerks so the first step has
    /// valid derivatives.  Call once before the first Step().
    private void InitialDerivatives()
    {
        int n = bodies.Length;

        // Gather current positions/velocities into temp arrays for the helper
        Vector3Double[] pos  = new Vector3Double[n];
        Vector3Double[] vel  = new Vector3Double[n];
        Vector3Double[] acc  = new Vector3Double[n];
        Vector3Double[] jerk = new Vector3Double[n];

        for (int i = 0; i < n; i++)
        {
            pos[i]  = bodies[i].position;
            vel[i]  = bodies[i].velocity;
        }

        EvaluateDerivatives(pos, vel, acc, jerk);

        for (int i = 0; i < n; i++)
        {
            bodies[i].acceleration = acc[i];
            bodies[i].jerk         = jerk[i];
        }
    }

    // -------------------------------------------------------------------------
    // Hermite core
    // -------------------------------------------------------------------------

    private void SaveState()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            pos0[i]  = bodies[i].position;
            vel0[i]  = bodies[i].velocity;
            acc0[i]  = bodies[i].acceleration;
            jerk0[i] = bodies[i].jerk;
        }
    }

    private void RestoreState()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].position     = pos0[i];
            bodies[i].velocity     = vel0[i];
            bodies[i].acceleration = acc0[i];
            bodies[i].jerk         = jerk0[i];
        }
    }

    /// Hermite predictor — 3rd-order Taylor expansion:
    ///   r_p = r0 + v0*dt + (1/2)*a0*dt^2 + (1/6)*j0*dt^3
    ///   v_p = v0 + a0*dt + (1/2)*j0*dt^2
    private void Predict(double dt)
    {
        double dt2 = dt  * dt;
        double dt3 = dt2 * dt;

        for (int i = 0; i < bodies.Length; i++)
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

        for (int i = 0; i < bodies.Length; i++)
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
    private void ApplyCorrected(double dt)
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].position     = posPred[i];
            bodies[i].velocity     = velPred[i];
            bodies[i].acceleration = accNew[i];
            bodies[i].jerk         = jerkNew[i];
        }
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
    private void EvaluateDerivatives(
        Vector3Double[] pos,
        Vector3Double[] vel,
        Vector3Double[] acc,
        Vector3Double[] jerk)
    {
        int n = bodies.Length;

        for (int i = 0; i < n; i++)
        {
            acc[i]  = Vector3Double.zero;
            jerk[i] = Vector3Double.zero;
        }

        for (int i = 0; i < n; i++)
        {
            acc[i] = Body.GetGravitationalForce(bodies[i], bodies) / bodies[i].mass;
            for (int j = i + 1; j < n; j++)
            {
                Vector3Double rij = pos[j]  - pos[i];   // r_j - r_i
                Vector3Double vij = vel[j]  - vel[i];   // v_j - v_i

                double r2    = rij.sqrMagnitude;
                double r     = Math.Sqrt(r2);
                double r3    = r2 * r;
                double r5    = r3 * r2;

                double rdotv = Vector3Double.dot(rij, vij);

                // --- Jerk contribution (analytic time-derivative of acc) --
                // da/dt_i += G*m_j * [ v_ij/r^3 - 3*(r·v/r^5)*r_ij ]
                Vector3Double jij = (vij / r3) - (rij * (3.0 * rdotv / r5));
                jij *= Constants.G * bodies[j].mass;

                Vector3Double jji = (vij / r3) - (rij * (3.0 * rdotv / r5));
                jji *= Constants.G * bodies[i].mass;

                jerk[i]  += jij;
                jerk[j]  -= jji;
            }
        }
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

        foreach (Body b in bodies)
        {
            double aMag = b.acceleration.magnitude;
            double jMag = b.jerk.magnitude;

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

        for (int i = 0; i < bodies.Length; i++)
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

}
