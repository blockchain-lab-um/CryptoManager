namespace CryptoManager.Infrastructure.Identity.WebAuthn;

public sealed class WebAuthnOptions
{
    public const string SectionName = "WebAuthn";
    public string ServerDomain { get; set; } = "localhost";
    public string ServerName { get; set; } = "CryptoManager";
    /// <summary>One or more allowed origins (scheme+host+port, no trailing slash). Must exactly match what the browser sends in clientDataJSON.origin.</summary>
    public string[] Origins { get; set; } = Array.Empty<string>();
    public int TimeoutMs { get; set; } = 60_000;
    public int ChallengeTtlSeconds { get; set; } = 300;
}
