using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Stoctable.Domain.Entities.Base;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure.Interceptors;

/// <summary>
/// Carimba <c>branch_id</c> em toda entidade nova que seja de escopo de filial.
///
/// Existe para que nenhum serviço precise lembrar de preencher esse campo.
/// Serviço que preenche filial na mão é exatamente como se grava venda na loja
/// errada: basta um caminho de código esquecido, e o erro é silencioso — a
/// linha existe, só que invisível para quem deveria vê-la e visível para quem
/// não deveria.
///
/// Entidades já existentes não são tocadas: mover uma venda de filial não é
/// uma operação que exista, e um UPDATE mudando branch_id seria bug.
/// </summary>
public class BranchScopeSaveChangesInterceptor(BranchContext branchContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<IBranchScoped>())
        {
            if (entry.State != EntityState.Added) continue;

            // Um valor já preenchido é respeitado: o backfill e os testes
            // precisam poder gravar em nome de outra filial explicitamente.
            if (entry.Entity.BranchId == Guid.Empty)
                entry.Entity.BranchId = branchContext.BranchId;
        }
    }
}
