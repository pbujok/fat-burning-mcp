using System.IO.Compression;
using FatBurner.Abstractions;
using Fit = Dynastream.Fit;

namespace FatBurner.Mcp.Garmin;

/// <summary>Decodes the samples from an activity's original file, as a raw FIT file or a ZIP containing one.</summary>
public static class FitActivityDecoder
{
    private const byte TimestampFieldNumber = 253;
    private const double DegreesPerSemicircle = 180d / 2147483648d;

    /// <returns>The samples, or null when the content is not a FIT file (for example an uploaded GPX or TCX).</returns>
    /// <exception cref="InvalidDataException">The content is a FIT file but is corrupted.</exception>
    public static ActivityStreams? Decode(string activityId, byte[] content)
    {
        using var fit = OpenFitStream(content);
        if (fit is null || fit.Length == 0)
            return null;

        var decoder = new Fit.Decode();
        fit.Position = 0;
        if (!decoder.IsFIT(fit))
            return null;

        fit.Position = 0;
        if (!decoder.CheckIntegrity(fit))
            throw new InvalidDataException($"The FIT file of activity {activityId} is corrupted.");

        var columns = new List<string>();
        var columnIndexes = new Dictionary<string, int>();
        var samples = new List<(DateTime Timestamp, Dictionary<int, double> Values)>();

        decoder.MesgEvent += (_, args) =>
        {
            if (args.mesg.Num != Fit.MesgNum.Record || args.mesg.GetField(TimestampFieldNumber) is null)
                return;

            var values = new Dictionary<int, double>();
            foreach (var field in args.mesg.Fields.Cast<Fit.FieldBase>().Concat(args.mesg.DeveloperFields))
            {
                if (args.mesg.GetField(TimestampFieldNumber) == field || !TryReadValue(field, out var name, out var value))
                    continue;

                if (!columnIndexes.TryGetValue(name, out var index))
                {
                    index = columns.Count;
                    columnIndexes[name] = index;
                    columns.Add(name);
                }

                values[index] = value;
            }

            samples.Add((new Fit.RecordMesg(args.mesg).GetTimestamp().GetDateTime(), values));
        };

        fit.Position = 0;
        decoder.Read(fit);

        var rows = samples
            .Select(sample =>
            {
                var row = new double?[columns.Count];
                foreach (var (index, value) in sample.Values)
                    row[index] = value;
                return row;
            })
            .ToArray();

        return new ActivityStreams(activityId, rows.Length, columns, samples.Select(s => s.Timestamp).ToArray(), rows);
    }

    private static MemoryStream? OpenFitStream(byte[] content)
    {
        if (content is not [(byte)'P', (byte)'K', ..])
            return new MemoryStream(content, writable: false);

        using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".fit", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return null;

        var fit = new MemoryStream();
        using var entryStream = entry.Open();
        entryStream.CopyTo(fit);
        return fit;
    }

    // Keeps single numeric values only: multi-value fields such as speed_1s have no place in one row.
    private static bool TryReadValue(Fit.FieldBase field, out string name, out double value)
    {
        name = field.Name;
        value = 0;

        if (field.GetNumValues() != 1 || field.GetValue() is not (IConvertible and not string) || !field.IsNumeric())
            return false;

        // Records share one definition, so a sample without a reading (such as a GPS dropout) carries
        // the type's "invalid" marker. The SDK returns it as data: latitude would decode as 180 degrees.
        var invalidValue = Fit.Fit.BaseType[field.Type & Fit.Fit.BaseTypeNumMask].invalidValue;
        if (Convert.ToDouble(field.GetRawValue(0)) == Convert.ToDouble(invalidValue))
            return false;

        value = Convert.ToDouble(field.GetValue());
        if (!double.IsFinite(value))
            return false;

        if (field.Units == "semicircles")
        {
            name = name switch
            {
                "PositionLat" => "Latitude",
                "PositionLong" => "Longitude",
                _ => name
            };
            value = Math.Round(value * DegreesPerSemicircle, 6);
            return true;
        }

        // FIT stores most values as floats; rounding drops conversion noise such as 3.2000000476.
        value = Math.Round(value, 3);
        return true;
    }
}
