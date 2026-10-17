namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// The one identity of a data store a database span talks to: <c>db.system.name</c>,
/// <c>server.address</c>, <c>server.port</c> and <c>db.namespace</c>. Tests and the service map
/// compare stores by this value, so "the same database" means the same thing everywhere.
/// </summary>
[JetBrains.Annotations.PublicAPI]
public readonly struct StoreIdentity : IEquatable<StoreIdentity>
{
    /// <summary>Initializes a store identity.</summary>
    /// <param name="system">The <c>db.system.name</c> value.</param>
    /// <param name="serverAddress">The <c>server.address</c> value.</param>
    /// <param name="serverPort">The <c>server.port</c> value.</param>
    /// <param name="namespace">The <c>db.namespace</c> value.</param>
    public StoreIdentity(string? system, string? serverAddress, long? serverPort, string? @namespace)
    {
        System = system;
        ServerAddress = serverAddress;
        ServerPort = serverPort;
        Namespace = @namespace;
    }

    /// <summary>The <c>db.system.name</c> value.</summary>
    public string? System { get; }

    /// <summary>The <c>server.address</c> value.</summary>
    public string? ServerAddress { get; }

    /// <summary>The <c>server.port</c> value.</summary>
    public long? ServerPort { get; }

    /// <summary>The <c>db.namespace</c> value.</summary>
    public string? Namespace { get; }

    /// <inheritdoc/>
    public bool Equals(StoreIdentity other) =>
        string.Equals(System, other.System, StringComparison.Ordinal)
        && string.Equals(ServerAddress, other.ServerAddress, StringComparison.Ordinal)
        && ServerPort == other.ServerPort
        && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is StoreIdentity other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = System is null ? 0 : StringComparer.Ordinal.GetHashCode(System);
            hash = (hash * 397) ^ (ServerAddress is null ? 0 : StringComparer.Ordinal.GetHashCode(ServerAddress));
            hash = (hash * 397) ^ ServerPort.GetHashCode();
            return (hash * 397) ^ (Namespace is null ? 0 : StringComparer.Ordinal.GetHashCode(Namespace));
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{System}://{ServerAddress}{(ServerPort is { } port ? ":" + port.ToString(global::System.Globalization.CultureInfo.InvariantCulture) : string.Empty)}/{Namespace}";

    /// <summary>Whether two store identities are equal.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns>Whether they are equal.</returns>
    public static bool operator ==(StoreIdentity left, StoreIdentity right) => left.Equals(right);

    /// <summary>Whether two store identities differ.</summary>
    /// <param name="left">The first identity.</param>
    /// <param name="right">The second identity.</param>
    /// <returns>Whether they differ.</returns>
    public static bool operator !=(StoreIdentity left, StoreIdentity right) => !left.Equals(right);
}
