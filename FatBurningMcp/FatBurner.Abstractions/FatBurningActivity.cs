namespace FatBurner.Abstractions;

public record FatBurningActivity(
    string Title,
    string Activity,
    DateTime Date,
    TimeOnly TimeOfActivity,
    decimal Distance,
    string SerializedDetails);