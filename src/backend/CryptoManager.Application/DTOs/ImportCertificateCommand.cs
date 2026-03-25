using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record ImportCertificateCommand(KeyId KeyId, byte[] CertDer, byte[][]? ChainDer = null);