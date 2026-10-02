namespace Hygeia;

public sealed class FanPoint
{
    public int Temp { get; set; }
    public int Duty { get; set; }
}

public static class FanCurves
{
    public static List<FanPoint> Default() =>
    [
        new() { Temp = 40, Duty = 35 },
        new() { Temp = 55, Duty = 50 },
        new() { Temp = 70, Duty = 70 },
        new() { Temp = 80, Duty = 85 },
        new() { Temp = 90, Duty = 100 }
    ];

    public static List<FanPoint> Normalize(IEnumerable<FanPoint> points)
    {
        var ordered = points
            .Select(point => new FanPoint
            {
                Temp = Math.Clamp(point.Temp, 0, 120),
                Duty = Math.Clamp(point.Duty, 0, 100)
            })
            .GroupBy(point => point.Temp)
            .Select(group => group.Last())
            .OrderBy(point => point.Temp)
            .ToList();
        return ordered.Count >= 2 ? ordered : Default();
    }

    public static bool Same(IReadOnlyList<FanPoint> left, IReadOnlyList<FanPoint> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Temp != right[i].Temp || left[i].Duty != right[i].Duty)
            {
                return false;
            }
        }

        return true;
    }

    public static int Interpolate(int temp, IReadOnlyList<FanPoint> points)
    {
        if (points.Count == 0)
        {
            return 40;
        }

        if (temp <= points[0].Temp)
        {
            return points[0].Duty;
        }

        for (var i = 1; i < points.Count; i++)
        {
            var left = points[i - 1];
            var right = points[i];
            if (temp > right.Temp)
            {
                continue;
            }

            var span = right.Temp - left.Temp;
            if (span <= 0)
            {
                return right.Duty;
            }

            var weight = (temp - left.Temp) / (double)span;
            return (int)Math.Round(left.Duty + (right.Duty - left.Duty) * weight);
        }

        return points[^1].Duty;
    }
}