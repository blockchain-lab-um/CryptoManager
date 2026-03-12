using CryptoManager.API.DTOs.General;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace CryptoManager.API.Middleware
{
    public sealed class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ErrorHandlingMiddleware(
            RequestDelegate next,
            ILogger<ErrorHandlingMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var traceId = context.TraceIdentifier;

            var (statusCode, message) = ex switch
            {
                // Application / Domain
                NotFoundException nf => (HttpStatusCode.NotFound, nf.Message),
                ForbiddenException fe => (HttpStatusCode.Forbidden, fe.Message),
                DomainException de => (HttpStatusCode.BadRequest, de.Message),
                HsmUnavailableException hu => (HttpStatusCode.ServiceUnavailable, hu.Message),

                // Explicit cancellation (client disconnected / timeout)
                //OperationCanceledException =>
                //    (HttpStatusCode.ClientClosedRequest, "Request was cancelled."),

                // Fallback
                _ => (HttpStatusCode.InternalServerError,
                      _env.IsDevelopment()
                          ? ex.Message
                          : "An unexpected error occurred.")
            };

            if ((int)statusCode >= 500)
            {
                _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);
            }
            else
            {
                _logger.LogWarning(ex, "Request failed. TraceId={TraceId}", traceId);
            }

            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse(
                Error: message,
                TraceId: traceId
            );

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}
