namespace CryptoManager.API.DTOs.General
{
    public sealed record ErrorResponse(
        string Error,
        string TraceId
    );
}
