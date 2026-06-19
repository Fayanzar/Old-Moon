using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Solver : MonoBehaviour
{
    public Body[] bodies;
    public double CurrentTime  { get; protected set; }
    public double CurrentDt    { get; protected set; }
    public double CurrentSimDt { get; protected set; }
    public int StepsPerTick    { get; protected set; }
}
