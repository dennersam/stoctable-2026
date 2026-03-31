using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Interceptors;

public class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditLogs(DbContext? context)
    {
        if (context is null) return;

        var httpContext = httpContextAccessor.HttpContext;
        var username = httpContext?.User?.Identity?.Name;
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

        Guid? userId = null;
        if (Guid.TryParse(httpContext?.User?.FindFirst("sub")?.Value, out var parsed))
            userId = parsed;

        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && e.Entity is not AuditLog)
            .ToList();

        foreach (var entry in entries)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Create
            };

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue
                ?.ToString() ?? string.Empty;

            string? oldValues = null;
            string? newValues = null;

            if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified)
                    .ToList();

                oldValues = JsonSerializer.Serialize(
                    changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue),
                    JsonOptions);

                newValues = JsonSerializer.Serialize(
                    changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue),
                    JsonOptions);
            }
            else if (entry.State == EntityState.Added)
            {
                newValues = JsonSerializer.Serialize(
                    entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue),
                    JsonOptions);
            }
            else if (entry.State == EntityState.Deleted)
            {
                oldValues = JsonSerializer.Serialize(
                    entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue),
                    JsonOptions);
            }

            var audit = new AuditLog
            {
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                Action = action,
                UserId = userId,
                Username = username,
                IpAddress = ipAddress,
                OldValues = oldValues,
                NewValues = newValues,
                OccurredAt = DateTimeOffset.UtcNow
            };

            context.Set<AuditLog>().Add(audit);
        }
    }
}
