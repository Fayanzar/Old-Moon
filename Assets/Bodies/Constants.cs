using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    public enum TimeUnit {
        Second,
        Minute,
        Hour,
        Day,
        Month,
        Year
    }

    public static Dictionary<TimeUnit, double> constDict = new Dictionary<TimeUnit, double>()
    {
        {TimeUnit.Second, 1},
        {TimeUnit.Minute, 60},
        {TimeUnit.Hour, 3600},
        {TimeUnit.Day, day},
        {TimeUnit.Month, month},
        {TimeUnit.Year, year}
    };
    public const double G = 6.674301515151515e-11;
    public const double mSun = 1.98847e30;
    public const double mEarth = 5.972168e24;
    public const double mMoon = 7.342e22;

    public const double AU = 1.495978707e11;

    public const double year = 31558149.7635;
    public const double month = 2360591.5;
    public const int day = 86400;
}
