using System;

public static class Newton
{
    private static double EPS = 0.00001;
    public static double Solve(Func<double, double> f, Func<double, double> ff,
                               double x)
    {
        double x1, x0;
        x0 = x1 = x;
        do
        {
            x0 = x1;
            x1 = x0 - f(x0) / ff(x0);
        } while (Math.Abs(x1 - x0) > EPS || Math.Abs(f(x1) - f(x0)) > EPS);
        return x1;
    }
}
