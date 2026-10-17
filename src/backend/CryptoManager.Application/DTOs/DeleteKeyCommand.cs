using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record DeleteKeyCommand(
    KeyId KeyId
);