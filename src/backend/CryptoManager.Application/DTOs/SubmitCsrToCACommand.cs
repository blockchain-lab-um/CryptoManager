using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record SubmitCsrToCACommand(KeyId KeyId, byte[] CsrDer);