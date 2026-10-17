using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record GenerateCsrCommand(KeyId KeyId, SubjectDN Subject);