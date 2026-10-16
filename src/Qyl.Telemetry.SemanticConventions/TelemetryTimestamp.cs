namespace Qyl.Telemetry.SemanticConventions;

internal static class TelemetryTimestamp
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static DateTimeOffset ToDateTimeOffset(ulong nanoseconds) =>
        Epoch.AddTicks((long)(nanoseconds / 100));
}
