using System.IO.Compression;
using Fit = Dynastream.Fit;

namespace FatBurner.Garmin.Tests;

/// <summary>Builds real FIT files with the SDK encoder, so tests exercise the actual decoder.</summary>
internal static class FitTestFiles
{
    public static readonly DateTime Start = new(2024, 8, 10, 6, 7, 8, DateTimeKind.Utc);

    private const double SemicirclesPerDegree = 2147483648d / 180d;

    /// <summary>A full first sample, and a second one with only a timestamp and heart rate.</summary>
    public static byte[] TwoSamples()
    {
        var full = new Fit.RecordMesg();
        full.SetTimestamp(new Fit.DateTime(Start));
        full.SetPositionLat((int)Math.Round(52.229676 * SemicirclesPerDegree));
        full.SetPositionLong((int)Math.Round(21.012229 * SemicirclesPerDegree));
        full.SetHeartRate(150);
        full.SetCadence(80);
        full.SetEnhancedSpeed(3.2f);

        var partial = new Fit.RecordMesg();
        partial.SetTimestamp(new Fit.DateTime(Start.AddSeconds(1)));
        partial.SetHeartRate(151);

        return Encode(full, partial);
    }

    public static byte[] WithDeveloperField(string name, byte value)
    {
        var developer = new Fit.DeveloperDataIdMesg();
        developer.SetDeveloperDataIndex(0);

        var description = new Fit.FieldDescriptionMesg();
        description.SetDeveloperDataIndex(0);
        description.SetFieldDefinitionNumber(0);
        description.SetFitBaseTypeId(Fit.FitBaseType.Uint8);
        description.SetFieldName(0, name);

        var record = new Fit.RecordMesg();
        record.SetTimestamp(new Fit.DateTime(Start));
        record.SetHeartRate(150);
        record.SetDeveloperField(new Fit.DeveloperField(description, developer) { });
        foreach (var field in record.DeveloperFields)
            field.SetValue(value);

        return Encode(developer, description, record);
    }

    public static byte[] Zip(byte[] content, string entryName = "42_ACTIVITY.fit")
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = zip.CreateEntry(entryName).Open();
            entry.Write(content);
        }

        return output.ToArray();
    }

    private static byte[] Encode(params Fit.Mesg[] mesgs)
    {
        var fileId = new Fit.FileIdMesg();
        fileId.SetType(Fit.File.Activity);
        fileId.SetManufacturer(Fit.Manufacturer.Garmin);
        fileId.SetTimeCreated(new Fit.DateTime(Start));

        using var output = new MemoryStream();
        var encoder = new Fit.Encode(output, Fit.ProtocolVersion.V20);
        encoder.Write(fileId);
        encoder.Write(mesgs);
        encoder.Close();
        return output.ToArray();
    }
}
