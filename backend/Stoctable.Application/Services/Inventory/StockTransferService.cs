using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Inventory;
using Stoctable.Communication.Responses.Inventory;
using Stoctable.Domain.Contracts;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Contracts.Services;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Inventory;

/// <summary>
/// Transferência de mercadoria entre filiais, em duas etapas.
///
/// A regra que sustenta o desenho: <b>cada etapa roda na sessão da própria
/// filial</b>. O envio exige estar logado na origem, o recebimento exige estar
/// logado no destino, e nenhuma das duas escreve na linha de estoque da outra.
/// O filtro de dupla visibilidade deixa as duas lojas VEREM o documento; quem
/// separa o que cada uma pode FAZER com ele são as checagens deste serviço.
/// </summary>
public class StockTransferService(
    IStockTransferRepository transferRepository,
    IProductRepository productRepository,
    IProductStockRepository stockRepository,
    IInventoryRepository inventoryRepository,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<IEnumerable<StockTransferResponse>>> GetAsync(
        TransferDirection direction, StockTransferStatus? status, CancellationToken ct = default)
    {
        var transfers = await transferRepository.GetAsync(direction, status, ct);
        return Result<IEnumerable<StockTransferResponse>>.Success(transfers.Select(MapToResponse));
    }

    public async Task<Result<StockTransferResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var transfer = await transferRepository.GetWithItemsAsync(id, ct);
        if (transfer is null)
            return Result<StockTransferResponse>.NotFound(ErrorMessages.StockTransfer.NotFound);

        return Result<StockTransferResponse>.Success(MapToResponse(transfer));
    }

    /// <summary>
    /// Cria o rascunho. Nenhum estoque é tocado — a mercadoria só sai no envio.
    /// </summary>
    public async Task<Result<StockTransferResponse>> CreateAsync(
        CreateStockTransferRequest request, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.EmptyItems);

        if (request.DestinationBranchId == currentTenant.BranchId)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.SameBranch);

        // O destino vem do corpo da requisição, então é entrada não confiável: se
        // aceito cru, viraria um jeito de despejar mercadoria numa filial de outra
        // empresa. As claims são assinadas, e é contra elas que se confere.
        if (!currentTenant.AllowedBranchIds.Contains(request.DestinationBranchId))
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.UnknownDestination);

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.EmptyItems);

            if (await productRepository.GetByIdNoTrackingAsync(item.ProductId, ct) is null)
                return Result<StockTransferResponse>.NotFound(ErrorMessages.Product.NotFound);
        }

        var transfer = new StockTransfer
        {
            // BranchId (origem) fica por conta do interceptor.
            DestinationBranchId = request.DestinationBranchId,
            TransferNumber = await transferRepository.GenerateNextNumberAsync(ct),
            Status = StockTransferStatus.Pending,
            Notes = request.Notes,
            Items = request.Items.Select(i => new StockTransferItem
            {
                ProductId = i.ProductId,
                QuantitySent = i.Quantity
            }).ToList()
        };

        await transferRepository.AddAsync(transfer, ct);

        var created = await transferRepository.GetWithItemsAsync(transfer.Id, ct);
        return Result<StockTransferResponse>.Success(MapToResponse(created!), 201);
    }

    /// <summary>
    /// Envia: baixa o estoque DA ORIGEM e coloca a carga em trânsito.
    ///
    /// Entre este passo e o recebimento a mercadoria não aparece no saldo de
    /// nenhuma das duas lojas, o que é fiel à realidade física — ela está no
    /// caminho. A tela de rede mostra isso como "em trânsito".
    /// </summary>
    public async Task<Result<StockTransferResponse>> ShipAsync(
        Guid id, string username, CancellationToken ct = default)
    {
        var transfer = await transferRepository.GetWithItemsAsync(id, ct);
        if (transfer is null)
            return Result<StockTransferResponse>.NotFound(ErrorMessages.StockTransfer.NotFound);

        // O filtro deixa o destino VER esta transferência; enviar é da origem.
        if (transfer.BranchId != currentTenant.BranchId)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.OnlyOriginCanShip);

        if (transfer.Status != StockTransferStatus.Pending)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.NotPending);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                foreach (var item in transfer.Items)
                {
                    var product = await productRepository.GetByIdNoTrackingAsync(item.ProductId, innerCt);

                    // Filial ativa = origem, então este é o caminho normal de
                    // baixa. Nada aqui sabe que existe uma segunda filial.
                    var baixa = await stockRepository.TryDecrementAsync(item.ProductId, item.QuantitySent, innerCt);
                    if (!baixa.Success)
                        throw new InsufficientStockException(
                            string.Format(ErrorMessages.Product.InsufficientStock, product?.Name ?? "?"));

                    await inventoryRepository.AddMovementAsync(new InventoryMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = MovementType.TransferOut,
                        Quantity = -item.QuantitySent,
                        QuantityBefore = baixa.QuantityAfter + item.QuantitySent,
                        QuantityAfter = baixa.QuantityAfter,
                        ReferenceType = "transfer",
                        ReferenceId = transfer.Id,
                        CreatedBy = username
                    }, innerCt);
                }

                transfer.Status = StockTransferStatus.InTransit;
                transfer.ShippedAt = DateTimeOffset.UtcNow;
                transfer.ShippedBy = username;
                await transferRepository.UpdateAsync(transfer, innerCt);

                return Result<StockTransferResponse>.Success(MapToResponse(transfer));
            }, ct);
        }
        catch (InsufficientStockException ex)
        {
            return Result<StockTransferResponse>.Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Recebe: confere e dá entrada no estoque DO DESTINO.
    ///
    /// Quem chama está logado no destino, então o BranchContext já aponta para a
    /// filial certa e a linha de estoque dela é criada na primeira entrada.
    /// Item não informado na conferência assume que chegou tudo o que saiu.
    /// </summary>
    public async Task<Result<StockTransferResponse>> ReceiveAsync(
        Guid id, ReceiveStockTransferRequest request, string username, CancellationToken ct = default)
    {
        var transfer = await transferRepository.GetWithItemsAsync(id, ct);
        if (transfer is null)
            return Result<StockTransferResponse>.NotFound(ErrorMessages.StockTransfer.NotFound);

        if (transfer.DestinationBranchId != currentTenant.BranchId)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.OnlyDestinationCanReceive);

        if (transfer.Status != StockTransferStatus.InTransit)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.NotInTransit);

        var conferido = request.Items?.ToDictionary(i => i.ProductId, i => i.QuantityReceived)
                        ?? [];

        foreach (var item in transfer.Items)
        {
            if (!conferido.TryGetValue(item.ProductId, out var recebido)) continue;
            if (recebido < 0 || recebido > item.QuantitySent)
                return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.ReceivedMoreThanSent);
        }

        return await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var item in transfer.Items)
            {
                var recebido = conferido.TryGetValue(item.ProductId, out var q) ? q : item.QuantitySent;
                item.QuantityReceived = recebido;

                if (recebido != item.QuantitySent)
                    transfer.HasDivergence = true;

                if (recebido <= 0) continue;

                var entrada = await stockRepository.IncrementAsync(item.ProductId, recebido, innerCt);

                await inventoryRepository.AddMovementAsync(new InventoryMovement
                {
                    ProductId = item.ProductId,
                    MovementType = MovementType.TransferIn,
                    Quantity = recebido,
                    QuantityBefore = entrada.QuantityAfter - recebido,
                    QuantityAfter = entrada.QuantityAfter,
                    ReferenceType = "transfer",
                    // Mesmo ReferenceId das duas pernas: é o que liga a saída de
                    // uma loja à entrada da outra na auditoria.
                    ReferenceId = transfer.Id,
                    CreatedBy = username
                }, innerCt);
            }

            transfer.Status = StockTransferStatus.Received;
            transfer.ReceivedAt = DateTimeOffset.UtcNow;
            transfer.ReceivedBy = username;
            await transferRepository.UpdateAsync(transfer, innerCt);

            return Result<StockTransferResponse>.Success(MapToResponse(transfer));
        }, ct);
    }

    /// <summary>
    /// Cancela — só antes do envio. Ver a justificativa em <see cref="StockTransfer"/>.
    /// </summary>
    public async Task<Result<StockTransferResponse>> CancelAsync(
        Guid id, string reason, CancellationToken ct = default)
    {
        var transfer = await transferRepository.GetWithItemsAsync(id, ct);
        if (transfer is null)
            return Result<StockTransferResponse>.NotFound(ErrorMessages.StockTransfer.NotFound);

        if (transfer.BranchId != currentTenant.BranchId)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.OnlyOriginCanShip);

        if (transfer.Status == StockTransferStatus.InTransit)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.CannotCancelInTransit);

        if (transfer.Status != StockTransferStatus.Pending)
            return Result<StockTransferResponse>.Failure(ErrorMessages.StockTransfer.NotPending);

        transfer.Status = StockTransferStatus.Cancelled;
        transfer.CancelledAt = DateTimeOffset.UtcNow;
        transfer.CancellationReason = reason;
        await transferRepository.UpdateAsync(transfer, ct);

        return Result<StockTransferResponse>.Success(MapToResponse(transfer));
    }

    private static StockTransferResponse MapToResponse(StockTransfer t) => new(
        Id: t.Id,
        TransferNumber: t.TransferNumber,
        OriginBranchId: t.BranchId,
        DestinationBranchId: t.DestinationBranchId,
        Status: t.Status.ToString(),
        HasDivergence: t.HasDivergence,
        ShippedAt: t.ShippedAt,
        ShippedBy: t.ShippedBy,
        ReceivedAt: t.ReceivedAt,
        ReceivedBy: t.ReceivedBy,
        CancelledAt: t.CancelledAt,
        CancellationReason: t.CancellationReason,
        Notes: t.Notes,
        CreatedAt: t.CreatedAt,
        Items: t.Items.Select(i => new StockTransferItemResponse(
            ProductId: i.ProductId,
            ProductName: i.Product?.Name,
            Sku: i.Product?.Sku,
            QuantitySent: i.QuantitySent,
            QuantityReceived: i.QuantityReceived)).ToList());
}
