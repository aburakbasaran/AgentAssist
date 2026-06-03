namespace AgentAssist.Evaluation.Tests.DevCloud;

internal static class Layer2DistributionStatistics
{
    public static DistributionSummary Summarize(IReadOnlyList<double> values)
    {
        if (values.Count is 0)
        {
            return new DistributionSummary(0, 0, 0, 0, 0, null, null);
        }

        var min = values.Min();
        var max = values.Max();
        var mean = values.Average();
        var variance = values.Select(v => (v - mean) * (v - mean)).Average();
        var std = Math.Sqrt(variance);
        double? ciLower = null;
        double? ciUpper = null;
        if (values.Count > 1)
        {
            var halfWidth = StudentT975(values.Count) * (std / Math.Sqrt(values.Count));
            ciLower = mean - halfWidth;
            ciUpper = mean + halfWidth;
        }

        return new DistributionSummary(values.Count, min, max, mean, std, ciLower, ciUpper);
    }

    /// <summary>Two-sided 95% Student-t multiplier for small n (Layer 2 uses N=3).</summary>
    private static double StudentT975(int n) => n switch
    {
        2 => 12.706,
        3 => 4.303,
        4 => 3.182,
        5 => 2.776,
        _ => 2.776
    };
}

internal sealed record DistributionSummary(
    int Count,
    double Min,
    double Max,
    double Mean,
    double Std,
    double? Confidence95Lower,
    double? Confidence95Upper);
