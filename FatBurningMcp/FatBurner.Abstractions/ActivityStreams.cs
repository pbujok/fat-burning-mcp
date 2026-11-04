namespace FatBurner.Abstractions;

/// <summary>
/// Per-sample activity data in columnar form: <c>Rows[i][j]</c> is the value of <c>Columns[j]</c> at <c>Timestamps[i]</c>,
/// or null when the device recorded no value for it in that sample.
/// </summary>
public record ActivityStreams(
    string ActivityId,
    int TotalSamples,
    IReadOnlyList<string> Columns,
    IReadOnlyList<DateTime> Timestamps,
    IReadOnlyList<double?[]> Rows)
{
    /// <summary>Keeps evenly spaced samples, always including the first and last. Values are picked, not averaged.</summary>
    public ActivityStreams Downsample(int maxPoints)
    {
        var points = Math.Max(maxPoints, 2);
        if (Rows.Count <= points)
            return this;

        var indexes = Enumerable.Range(0, points)
            .Select(i => (int)((long)i * (Rows.Count - 1) / (points - 1)))
            .ToArray();

        return this with
        {
            Timestamps = indexes.Select(i => Timestamps[i]).ToArray(),
            Rows = indexes.Select(i => Rows[i]).ToArray()
        };
    }
}
