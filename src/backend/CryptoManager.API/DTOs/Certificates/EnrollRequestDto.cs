using System.ComponentModel.DataAnnotations;

namespace CryptoManager.API.DTOs.Certificates;

public sealed record EnrollRequestDto
{
    /// <summary>Certificate subject common name (required).</summary>
    [Required, MinLength(1)]
    public string CommonName { get; init; } = default!;

    public string? Organization { get; init; }
    public string? OrganizationalUnit { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "US".</summary>
    public string? Country { get; init; }
}