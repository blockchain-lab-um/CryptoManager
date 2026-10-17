using CryptoManager.API.DTOs.Certificates;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers;

/// <summary>
/// Certificate lifecycle management for a key.
/// The CA used for enrollment is determined by the backend configuration — clients never choose it.
/// </summary>
[Route("api/keys/{keyId:guid}/certificates")]
[ApiController]
[Authorize(Policy = "CanOperate")]
public sealed class CertificatesController : ControllerBase
{
    private readonly GenerateCsrUseCase _generateCsr;
    private readonly SubmitCsrToCAUseCase _submitCsr;
    private readonly CompleteCertificateEnrollmentUseCase _completeEnrollment;
    private readonly ImportCertificateUseCase _importCertificate;
    private readonly RevokeCertificateUseCase _revokeCertificate;
    private readonly GetActiveCertificateUseCase _getActiveCertificate;

    public CertificatesController(
        GenerateCsrUseCase generateCsr,
        SubmitCsrToCAUseCase submitCsr,
        CompleteCertificateEnrollmentUseCase completeEnrollment,
        ImportCertificateUseCase importCertificate,
        RevokeCertificateUseCase revokeCertificate,
        GetActiveCertificateUseCase getActiveCertificate)
    {
        _generateCsr         = generateCsr;
        _submitCsr           = submitCsr;
        _completeEnrollment  = completeEnrollment;
        _importCertificate   = importCertificate;
        _revokeCertificate   = revokeCertificate;
        _getActiveCertificate = getActiveCertificate;
    }

    /// <summary>
    /// Generates a CSR for the key's primary version and submits it to the configured CA.
    /// Returns an enrollment ID that can be polled via the /complete endpoint.
    /// For the soft (dev) CA, the certificate is issued synchronously and /complete
    /// will always return immediately with IsComplete=true.
    /// </summary>
    // POST /api/keys/{keyId}/certificates/enroll
    [HttpPost("enroll")]
    [ProducesResponseType(typeof(EnrollResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EnrollResponseDto>> Enroll(
        Guid keyId,
        [FromBody] EnrollRequestDto request,
        CancellationToken ct)
    {
        var kid     = new KeyId(keyId);
        var subject = SubjectDN.From(request.CommonName, request.Organization, request.OrganizationalUnit, request.Country);

        var csrResult = await _generateCsr.ExecuteAsync(new GenerateCsrCommand(kid, subject), ct);
        var caResult  = await _submitCsr.ExecuteAsync(new SubmitCsrToCACommand(kid, csrResult.CsrDer), ct);

        return Ok(new EnrollResponseDto(
            CertificateId: caResult.CertificateId.Value.ToString(),
            EnrollmentId:  caResult.EnrollmentId));
    }

    /// <summary>
    /// Polls the CA for the enrollment result and activates the certificate when ready.
    /// For the soft CA this always completes immediately.
    /// </summary>
    // POST /api/keys/{keyId}/certificates/enrollments/{enrollmentId}/complete
    [HttpPost("enrollments/{enrollmentId}/complete")]
    [ProducesResponseType(typeof(CompleteEnrollmentResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompleteEnrollmentResponseDto>> CompleteEnrollment(
        Guid keyId,
        string enrollmentId,
        CancellationToken ct)
    {
        var result = await _completeEnrollment.ExecuteAsync(
            new CompleteCertificateEnrollmentCommand(enrollmentId), ct);

        return Ok(new CompleteEnrollmentResponseDto(
            IsComplete:    result.IsComplete,
            CertificateId: result.CertificateId?.Value.ToString(),
            Thumbprint:    result.Thumbprint));
    }

    /// <summary>
    /// Imports an externally-issued certificate (DER, base64) for the key's primary version.
    /// The certificate's public key must match the key version's public key.
    /// </summary>
    // POST /api/keys/{keyId}/certificates/import
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportCertificateResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportCertificateResponseDto>> Import(
        Guid keyId,
        [FromBody] ImportCertificateRequestDto request,
        CancellationToken ct)
    {
        byte[] certDer;
        byte[][]? chainDer = null;

        try
        {
            certDer = Convert.FromBase64String(request.CertificateDerBase64);
            if (request.ChainDerBase64 is { Length: > 0 })
                chainDer = request.ChainDerBase64.Select(Convert.FromBase64String).ToArray();
        }
        catch (FormatException)
        {
            throw new DomainException("CertificateDerBase64 or ChainDerBase64 contains invalid base64.");
        }

        var result = await _importCertificate.ExecuteAsync(
            new ImportCertificateCommand(new KeyId(keyId), certDer, chainDer), ct);

        return Ok(new ImportCertificateResponseDto(
            CertificateId: result.CertificateId.Value.ToString(),
            Thumbprint:    result.Thumbprint));
    }

    /// <summary>Revokes the specified certificate.</summary>
    // POST /api/keys/{keyId}/certificates/{certId}/revoke
    [HttpPost("{certId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(
        Guid keyId,
        Guid certId,
        [FromBody] RevokeRequestDto request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<RevocationReason>(request.Reason, ignoreCase: true, out var reason))
            throw new DomainException($"Unknown revocation reason '{request.Reason}'.");

        await _revokeCertificate.ExecuteAsync(
            new RevokeCertificateCommand(new CertificateId(certId), reason), ct);

        return NoContent();
    }

    /// <summary>Returns the active certificate for the key's primary version, or 404 if none.</summary>
    // GET /api/keys/{keyId}/certificates/active
    [HttpGet("active")]
    [ProducesResponseType(typeof(CertificateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificateResponseDto>> GetActive(
        Guid keyId,
        CancellationToken ct)
    {
        var cert = await _getActiveCertificate.ExecuteAsync(new KeyId(keyId), ct);

        if (cert is null)
            return NotFound();

        return Ok(new CertificateResponseDto(
            Id:           cert.Id.Value.ToString(),
            KeyVersionId: cert.KeyVersionId.Value.ToString(),
            Status:       cert.Status.ToString(),
            Source:       cert.Source.ToString(),
            SerialNumber: cert.SerialNumber,
            Thumbprint:   cert.Thumbprint,
            SubjectDN:    cert.SubjectDN,
            IssuerDN:     cert.IssuerDN,
            NotBefore:    cert.NotBefore,
            NotAfter:     cert.NotAfter,
            EnrollmentId: cert.EnrollmentId,
            CreatedAt:    cert.CreatedAt,
            CreatedBy:    cert.CreatedBy));
    }
}