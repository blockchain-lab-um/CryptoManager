namespace CryptoManager.Application.DTOs;

public sealed record VerifySignedFileCommand(
    string FileName,
    string? ContentType,
    byte[] FileBytes
);
