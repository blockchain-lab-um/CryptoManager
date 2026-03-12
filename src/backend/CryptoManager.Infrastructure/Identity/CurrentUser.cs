using CryptoManager.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CryptoManager.Infrastructure.Identity;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor accessor) => _http = accessor;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public string Actor =>
        User?.Identity?.Name ?? "anonymous";

    public string UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) =>
        User?.IsInRole(role) == true;
}