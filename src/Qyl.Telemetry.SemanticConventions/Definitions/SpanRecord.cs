using System.Diagnostics;
using System.Globalization;

namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A finished span as a trace reader received it: identity, name, kind, timing, status, scope,
/// attributes, resource, events and links. The generated span views (namespaces <c>…Views</c>)
/// read it, and <c>Qyl.Telemetry.Testing</c>'s receiver produces it.
/// </summary>
/// <remarks>
/// Attribute values are what OTLP carries: <see cref="string"/>, <see cref="long"/>,
/// <see cref="double"/>, <see cref="bool"/>, or a list of those. The typed getters convert
/// between the integer and floating representations and return <see langword="null"/> for an
/// absent key or a value of another type.
/// </remarks>
public sealed class SpanRecord
{
    private const string AppPrefix = "qyl.app.";

    /// <summary>Initializes a span record.</summary>
    /// <param name="traceId">The trace id, 32 lowercase hex characters.</param>
    /// <param name="spanId">The span id, 16 lowercase hex characters.</param>
    /// <param name="parentSpanId">The parent span id, or <see langword="null"/> for a root span.</param>
    /// <param name="name">The span name.</param>
    /// <param name="kind">The span kind.</param>
    /// <param name="startTime">When the span started.</param>
    /// <param name="endTime">When the span ended.</param>
    /// <param name="status">The status code.</param>
    /// <param name="statusDescription">The status description, or <see langword="null"/>.</param>
    /// <param name="scopeName">The instrumentation scope name.</param>
    /// <param name="scopeVersion">The instrumentation scope version, or <see langword="null"/>.</param>
    /// <param name="schemaUrl">The scope's schema URL, or <see langword="null"/>.</param>
    /// <param name="attributes">The span attributes.</param>
    /// <param name="resource">The resource attributes.</param>
    /// <param name="events">The span events.</param>
    /// <param name="links">The span links.</param>
    public SpanRecord(
        string traceId,
        string spanId,
        string? parentSpanId,
        string name,
        ActivityKind kind,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        ActivityStatusCode status,
        string? statusDescription,
        string scopeName,
        string? scopeVersion,
        string? schemaUrl,
        IReadOnlyDictionary<string, object?> attributes,
        IReadOnlyDictionary<string, object?> resource,
        IReadOnlyList<SpanEventRecord> events,
        IReadOnlyList<SpanLinkRecord> links)
    {
        TraceId = traceId ?? throw new ArgumentNullException(nameof(traceId));
        SpanId = spanId ?? throw new ArgumentNullException(nameof(spanId));
        ParentSpanId = parentSpanId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        StartTime = startTime;
        EndTime = endTime;
        Status = status;
        StatusDescription = statusDescription;
        ScopeName = scopeName ?? throw new ArgumentNullException(nameof(scopeName));
        ScopeVersion = scopeVersion;
        SchemaUrl = schemaUrl;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Links = links ?? throw new ArgumentNullException(nameof(links));
    }

    /// <summary>The trace id, 32 lowercase hex characters.</summary>
    public string TraceId { get; }

    /// <summary>The span id, 16 lowercase hex characters.</summary>
    public string SpanId { get; }

    /// <summary>The parent span id, or <see langword="null"/> for a root span.</summary>
    public string? ParentSpanId { get; }

    /// <summary>The span name.</summary>
    public string Name { get; }

    /// <summary>The span kind.</summary>
    public ActivityKind Kind { get; }

    /// <summary>When the span started.</summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>When the span ended.</summary>
    public DateTimeOffset EndTime { get; }

    /// <summary>The span's duration.</summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>The status code.</summary>
    public ActivityStatusCode Status { get; }

    /// <summary>The status description, or <see langword="null"/>.</summary>
    public string? StatusDescription { get; }

    /// <summary>The instrumentation scope name.</summary>
    public string ScopeName { get; }

    /// <summary>The instrumentation scope version, or <see langword="null"/>.</summary>
    public string? ScopeVersion { get; }

    /// <summary>The scope's schema URL, or <see langword="null"/>.</summary>
    public string? SchemaUrl { get; }

    /// <summary>The span attributes.</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; }

    /// <summary>The resource attributes.</summary>
    public IReadOnlyDictionary<string, object?> Resource { get; }

    /// <summary>The span events.</summary>
    public IReadOnlyList<SpanEventRecord> Events { get; }

    /// <summary>The span links.</summary>
    public IReadOnlyList<SpanLinkRecord> Links { get; }

    /// <summary>The resource's <c>service.name</c>, or <see langword="null"/>.</summary>
    public string? Service => Resource.TryGetValue("service.name", out var value) ? value as string : null;

    /// <summary>The <c>qyl.app.*</c> business attributes, keyed without the prefix, values as invariant strings.</summary>
    public IReadOnlyDictionary<string, string> App
    {
        get
        {
            var app = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in Attributes)
            {
                if (attribute.Key.StartsWith(AppPrefix, StringComparison.Ordinal) && attribute.Value is not null)
                {
                    app[attribute.Key.Substring(AppPrefix.Length)] = Invariant(attribute.Value);
                }
            }

            return app;
        }
    }

    /// <summary>The attribute as a string, or <see langword="null"/> when absent or not a string.</summary>
    /// <param name="key">The attribute key.</param>
    /// <returns>The value.</returns>
    public string? GetString(string key) => Attributes.TryGetValue(key, out var value) ? value as string : null;

    /// <summary>The attribute as a 64-bit integer, or <see langword="null"/> when absent or not an integer.</summary>
    /// <param name="key">The attribute key.</param>
    /// <returns>The value.</returns>
    public long? GetInt64(string key) =>
        Attributes.TryGetValue(key, out var value)
            ? value switch
            {
                long l => l,
                int i => i,
                short s => s,
                byte b => b,
                _ => null,
            }
            : null;

    /// <summary>The attribute as a double, or <see langword="null"/> when absent or not a number.</summary>
    /// <param name="key">The attribute key.</param>
    /// <returns>The value.</returns>
    public double? GetDouble(string key) =>
        Attributes.TryGetValue(key, out var value)
            ? value switch
            {
                double d => d,
                float f => f,
                long l => l,
                int i => i,
                _ => null,
            }
            : null;

    /// <summary>The attribute as a boolean, or <see langword="null"/> when absent or not a boolean.</summary>
    /// <param name="key">The attribute key.</param>
    /// <returns>The value.</returns>
    public bool? GetBoolean(string key) => Attributes.TryGetValue(key, out var value) && value is bool b ? b : null;

    /// <summary>The attribute as a list of strings, or <see langword="null"/> when absent or not a list of strings.</summary>
    /// <param name="key">The attribute key.</param>
    /// <returns>The value.</returns>
    public IReadOnlyList<string>? GetStrings(string key)
    {
        if (!Attributes.TryGetValue(key, out var value) || value is string || value is not System.Collections.IEnumerable items)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in items)
        {
            if (item is not string text)
            {
                return null;
            }

            list.Add(text);
        }

        return list;
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    private static string Invariant(object value) =>
        value switch
        {
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
}

/// <summary>An event of a <see cref="SpanRecord"/>.</summary>
public sealed class SpanEventRecord
{
    /// <summary>Initializes a span event record.</summary>
    /// <param name="name">The event name (<c>exception</c> for a recorded exception).</param>
    /// <param name="timestamp">When the event happened.</param>
    /// <param name="attributes">The event attributes.</param>
    public SpanEventRecord(string name, DateTimeOffset timestamp, IReadOnlyDictionary<string, object?> attributes)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Timestamp = timestamp;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    /// <summary>The event name.</summary>
    public string Name { get; }

    /// <summary>When the event happened.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>The event attributes.</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; }
}

/// <summary>A link of a <see cref="SpanRecord"/>.</summary>
public sealed class SpanLinkRecord
{
    /// <summary>Initializes a span link record.</summary>
    /// <param name="traceId">The linked trace id.</param>
    /// <param name="spanId">The linked span id.</param>
    /// <param name="attributes">The link attributes.</param>
    public SpanLinkRecord(string traceId, string spanId, IReadOnlyDictionary<string, object?> attributes)
    {
        TraceId = traceId ?? throw new ArgumentNullException(nameof(traceId));
        SpanId = spanId ?? throw new ArgumentNullException(nameof(spanId));
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    /// <summary>The linked trace id.</summary>
    public string TraceId { get; }

    /// <summary>The linked span id.</summary>
    public string SpanId { get; }

    /// <summary>The link attributes.</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; }
}
