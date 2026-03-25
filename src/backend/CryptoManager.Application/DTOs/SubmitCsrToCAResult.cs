using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record SubmitCsrToCAResult(CertificateId CertificateId, string EnrollmentId);