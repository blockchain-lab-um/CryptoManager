using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.UseCases;

public sealed class GetAuditLogsUseCase
{
    private readonly IAuditRepository _auditRepository;
    private readonly ICurrentUser _currentUser;

    public GetAuditLogsUseCase(IAuditRepository auditRepository, ICurrentUser currentUser)
    {
        _auditRepository = auditRepository;
        _currentUser = currentUser;
    }

    public async Task<AuditLogPage> ExecuteAsync(GetAuditLogsCommand command, CancellationToken ct = default)
    {
        var isAdmin = _currentUser.IsInRole("Admin");

        var query = new AuditLogQuery(
            // Non-admin callers are scoped to their own rows only.
            // This is set here and cannot be overridden by the API caller.
            ScopeToActorId: isAdmin ? null : _currentUser.UserId,
            // Actor filter is silently ignored for non-admins: the UI does not send it for
            // regular users, but we strip it here regardless so the contract is enforced
            // server-side and cannot be bypassed by a crafted request.
            Actor: isAdmin ? command.Actor : null,
            Action: command.Action,
            KeyId: command.KeyId,
            From: command.From,
            To: command.To,
            Page: command.Page,
            PageSize: Math.Clamp(command.PageSize, 1, 100));

        var page = await _auditRepository.QueryAsync(query, ct);

        // Operators see their own logs but not raw error/exception messages.
        // Admins see the full Error field.
        if (!isAdmin)
        {
            var sanitised = page.Items
                .Select(e => e with { Error = null })
                .ToList();
            return page with { Items = sanitised };
        }

        return page;
    }
}