namespace CryptoManager.Application.Abstractions;

public interface IHsmProviderRegistry
{
    /// <summary>Routes to a named provider instance. Used for operations on existing keys.</summary>
    IHsmProvider Resolve(string providerInstanceId);

    /// <summary>Returns the configured default provider. Used when creating new keys.</summary>
    IHsmProvider ResolveDefault();
    
    /// <summary> Returns the first available provider. Used when creating new keys. /// </summary>
    IHsmProvider ResolveFirstAvailable();

    /// <summary>The ID of the default provider, to be stamped into ProviderRef on key creation.</summary>
    string DefaultProviderId { get; }
}