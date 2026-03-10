namespace CryptoManager.Application.DTOs;

public sealed record SignedArtifact(
    string Format,
    string OutputFileName,
    string OutputContentType,
    byte[] Bytes);
