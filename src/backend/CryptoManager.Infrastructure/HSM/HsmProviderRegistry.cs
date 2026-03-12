using CryptoManager.Application.Abstractions;
using CryptoManager.Application.Exceptions;

namespace CryptoManager.Infrastructure.HSM;

public sealed class HsmProviderRegistry : IHsmProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IHsmProvider> _providers;

    public string DefaultProviderId { get; }

    public HsmProviderRegistry(IReadOnlyDictionary<string, IHsmProvider> providers, string defaultProviderId)
    {
        if (providers == null || providers.Count == 0)
            throw new ArgumentException("At least one HSM provider must be registered.", nameof(providers));
        if (!providers.ContainsKey(defaultProviderId))
            throw new ArgumentException($"Default provider '{defaultProviderId}' is not registered.", nameof(defaultProviderId));

        _providers = providers;
        DefaultProviderId = defaultProviderId;
    }

    public IHsmProvider Resolve(string providerInstanceId)
    {
        // Fall back to default for rows that pre-date multi-provider support (ProviderInstanceId was "").
        var id = string.IsNullOrEmpty(providerInstanceId) ? DefaultProviderId : providerInstanceId;

        if (!_providers.TryGetValue(id, out var provider))
            throw new HsmUnavailableException($"HSM provider '{id}' is not registered.");

        return provider;
    }

    public IHsmProvider ResolveDefault() => _providers[DefaultProviderId];

    public IHsmProvider ResolveFirstAvailable()
    {
        var defaultProvider = _providers[DefaultProviderId];
        if (defaultProvider.IsAvailable())
            return defaultProvider;

        var fallback = _providers
            .Where(kv => kv.Key != DefaultProviderId && kv.Value.IsAvailable())
            .Select(kv => kv.Value)
            .FirstOrDefault();

        return fallback
            ?? throw new HsmUnavailableException("No HSM provider is currently available.");
    }
}