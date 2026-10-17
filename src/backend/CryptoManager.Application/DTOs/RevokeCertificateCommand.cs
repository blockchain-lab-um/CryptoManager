using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record RevokeCertificateCommand(
    CertificateId CertificateId,
    RevocationReason Reason = RevocationReason.Unspecified);