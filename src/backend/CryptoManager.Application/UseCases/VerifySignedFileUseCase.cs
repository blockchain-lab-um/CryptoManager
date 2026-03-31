using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.UseCases;

public sealed class VerifySignedFileUseCase
{
    private readonly ISignedFileVerifier _signedFileVerifier;

    public VerifySignedFileUseCase(ISignedFileVerifier signedFileVerifier)
    {
        _signedFileVerifier = signedFileVerifier;
    }

    public Task<VerifySignedFileResult> ExecuteAsync(VerifySignedFileCommand command, CancellationToken ct = default) =>
        _signedFileVerifier.VerifyAsync(command, ct);
}
