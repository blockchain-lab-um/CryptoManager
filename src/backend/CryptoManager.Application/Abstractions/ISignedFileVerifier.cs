using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

public interface ISignedFileVerifier
{
    Task<VerifySignedFileResult> VerifyAsync(VerifySignedFileCommand command, CancellationToken ct = default);
}
