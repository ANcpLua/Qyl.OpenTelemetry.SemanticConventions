namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

[TestClass]
public sealed class CapturedFieldPolicyTests
{
    [TestMethod]
    [DataRow("http.request.header", true)]
    [DataRow("http.response.header", true)]
    [DataRow("rpc.request.metadata", false)]
    [DataRow("rpc.response.metadata", false)]
    public void Captured_families_use_the_finite_name_rule(string root, bool http)
    {
        string[] accepted = ["x-request-id", "x_request_id", "x.request.id", "x-message-id", "content-type",
            "x-ratelimit-remaining", "x-tenant-id", "x-demo-res-md", "x-auth", "key"];
        string[] denied = ["authorization", "proxy-authorization", "cookie", "set-cookie", "x-api-key",
            "www-authenticate", "proxy-authenticate", "x-auth-token", "x-token-count", "client-secret",
            "x-password", "x-passwd", "x-apikey", "x-api_key", "x-credential", "x-private_key",
            "x-private-key", "x-privatekey", "x-client_secret", "x-jwt", "", "bad name", "bad:name",
            "bad/name", "bad\r\nname", "naïve"];
        foreach (var name in accepted)
        {
            Check(root + "." + name, true);
            Check((root + "." + name).ToUpperInvariant(), true);
        }
        foreach (var name in denied)
        {
            Check(root + "." + name, false);
            Check((root + "." + name).ToUpperInvariant(), false);
        }
        Check(root, false);
        Check(root.ToUpperInvariant(), false);
        Check(root + ".x+feature", http);
        Check(root + ".!#$%&'*+^`|~", http);
    }

    [TestMethod]
    public void Nearby_and_unrelated_names_keep_the_generic_policy()
    {
        Assert.IsFalse(RegistryPolicy.IsPersisted("custom.message"));
        Assert.IsFalse(RegistryPolicy.IsPersisted("http.request.headers.x-message-id"));
        Assert.IsTrue(RegistryPolicy.IsPersisted("http.request.headers.x-request-id"));
        Assert.IsFalse(RegistryPolicy.TryGetCapturedFieldPersistence("http.request.headers.x-request-id", out _));
        Assert.IsFalse(RegistryPolicy.TryGetCapturedFieldPersistence("custom.http.request.header.x-message-id", out _));
        Assert.IsFalse(RegistryPolicy.IsPersisted("user.http.request.header.x-request-id"));
        Assert.IsTrue(RegistryPolicy.IsPersisted("exception.message"));
        Assert.IsFalse(RegistryPolicy.IsPersisted("Exception.Message"));
    }

    private static void Check(string key, bool expected)
    {
        Assert.IsTrue(RegistryPolicy.TryGetCapturedFieldPersistence(key, out var actual), key);
        Assert.AreEqual(expected, actual, key);
        Assert.AreEqual(expected, RegistryPolicy.IsPersisted(key), key);
    }
}
