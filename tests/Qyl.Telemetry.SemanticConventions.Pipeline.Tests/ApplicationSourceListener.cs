using System.Diagnostics;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// Every test class that listens to the application scope (<c>Qyl.Spans.Source</c>) takes this
/// resource lock, so the classes run one after the other and a "no listener" test never sees another
/// class's listener.
/// </summary>
internal static class ApplicationSourceListeners
{
    public const string Name = "Qyl.Telemetry.Application listeners";
}

/// <summary>Enables activities on the application scope while it is alive.</summary>
internal sealed class ApplicationSourceListener : IDisposable
{
    private readonly ActivityListener _listener;
    public ApplicationSourceListener()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "Qyl.Telemetry.Application",
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();
}
