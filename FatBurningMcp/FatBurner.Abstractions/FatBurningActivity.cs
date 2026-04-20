namespace FatBurner.Abstractions;

public record FatBurningActivity(
    string ActivityId,
    string Title,
    string Activity,
    DateTime Date,
    TimeOnly TimeOfActivity,
    decimal Distance,
    string SerializedDetails);