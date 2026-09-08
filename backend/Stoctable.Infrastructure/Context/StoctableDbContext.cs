using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context.Configurations;
using Stoctable.Infrastructure.Search;
using Stoctable.Infrastructure.Tenancy;

namespace Stoctable.Infrastructure.Context;

public class StoctableDbContext : DbContext
{
    /// <summary>
    /// Filial ativa. É CAMPO DE INSTÂNCIA de propósito, e os filtros globais
    /// mais abaixo leem através dele.
    ///
    /// ⚠️ Nunca copie este valor para uma variável local dentro de
    /// OnModelCreating para usar no filtro. O modelo do EF é construído UMA VEZ
    /// e cacheado por tipo de contexto: um valor copiado congela a filial da
    /// primeira requisição do processo, e todas as seguintes passam a enxergar
    /// os dados dela. A falha é silenciosa e passa numa suíte de teste de uma
    /// filial só — por isso existe o BranchIsolationTests, que roda duas
    /// filiais no mesmo processo.
    /// </summary>
    private readonly BranchContext _branch;

    public StoctableDbContext(DbContextOptions<StoctableDbContext> options, BranchContext? branchContext = null)
        : base(options)
    {
        // O parâmetro é opcional para o factory de design-time e para os testes,
        // que constroem o contexto na mão. Em execução real o DI injeta o
        // BranchContext com escopo de requisição.
        _branch = branchContext ?? new BranchContext();
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    public DbSet<CustomerType> CustomerTypes => Set<CustomerType>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerCrmNote> CustomerCrmNotes => Set<CustomerCrmNote>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as IEntityTypeConfiguration<T> deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoctableDbContext).Assembly);

        // Função de normalização usada pela busca textual (ver SearchSchema).
        // Não usamos EF.Functions.Unaccent do Npgsql porque ela mapeia para o
        // unaccent() não-imutável, que não consegue usar os índices GIN.
        modelBuilder
            .HasDbFunction(typeof(DbSearchFunctions).GetMethod(nameof(DbSearchFunctions.Normalize))!)
            .HasName(SearchSchema.FunctionName);

        // Mapeamentos simples sem configuração separada.
        //
        // A entidade Branch saiu daqui: filial agora é conceito do control plane
        // (Domain/Entities/ControlPlane/Branch.cs). A tabela `branches` do tenant
        // nunca foi lida ou escrita por nada — a resolução de filial sempre usou
        // o nome do segredo no Key Vault, jamais esta tabela.

        // ─── Isolamento por filial ──────────────────────────────────────────
        // O banco é da empresa; estas tabelas são de cada loja. O filtro lê
        // _branch (campo de instância) e o EF o reavalia a cada consulta, com o
        // contexto da requisição — ver o aviso na declaração do campo.
        modelBuilder.Entity<Sale>().HasQueryFilter(x => x.BranchId == _branch.BranchId);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => x.BranchId == _branch.BranchId);
        modelBuilder.Entity<Quotation>().HasQueryFilter(x => x.BranchId == _branch.BranchId);
        modelBuilder.Entity<InventoryMovement>().HasQueryFilter(x => x.BranchId == _branch.BranchId);
        modelBuilder.Entity<StockReservation>().HasQueryFilter(x => x.BranchId == _branch.BranchId);
        modelBuilder.Entity<ProductStock>().HasQueryFilter(x => x.BranchId == _branch.BranchId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(x => x.BranchId == _branch.BranchId);

        // Transferência é o único registro que DUAS filiais enxergam, e por isso
        // seu filtro tem duas pernas: a origem vê as que emitiu, o destino vê as
        // que vêm para ele. Continua sendo um filtro — ninguém mais alcança a
        // linha, e uma terceira loja não vê nada. Quem separa o que cada lado
        // PODE FAZER é a regra de negócio no serviço, não este filtro.
        modelBuilder.Entity<StockTransfer>().HasQueryFilter(
            x => x.BranchId == _branch.BranchId || x.DestinationBranchId == _branch.BranchId);

        // SaleItem e QuotationItem NÃO têm filtro nem branch_id: só são
        // alcançáveis pelo pai, que já é filtrado. Uma coluna redundante criaria
        // um invariante de consistência que ninguém manteria. O preço disso é
        // que consultar context.SaleItems direto não filtra nada — por isso
        // esses DbSet não devem sair do repositório do agregado.

        modelBuilder.Entity<ProductStock>(ps =>
        {
            ps.ToTable("product_stocks");
            ps.HasKey(x => x.Id);
            ps.Property(x => x.Id).HasColumnName("id");
            ps.Property(x => x.BranchId).HasColumnName("branch_id");
            ps.Property(x => x.ProductId).HasColumnName("product_id");
            ps.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(10, 3);
            ps.Property(x => x.Reserved).HasColumnName("reserved").HasPrecision(10, 3);
            ps.Property(x => x.Minimum).HasColumnName("minimum").HasPrecision(10, 3);
            ps.Property(x => x.CreatedAt).HasColumnName("created_at");
            ps.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            ps.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            ps.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            ps.Ignore(x => x.Available);

            ps.HasOne(x => x.Product).WithMany(p => p.Stocks)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);

            // Único por (filial, produto): é o alvo do ON CONFLICT nos UPDATEs
            // atômicos de estoque. Sem ele o upsert não teria como funcionar.
            ps.HasIndex(x => new { x.BranchId, x.ProductId }).IsUnique();
        });

        modelBuilder.Entity<StockTransfer>(t =>
        {
            t.ToTable("stock_transfers");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id");
            t.Property(x => x.BranchId).HasColumnName("branch_id");
            t.Property(x => x.DestinationBranchId).HasColumnName("destination_branch_id");
            t.Property(x => x.TransferNumber).HasColumnName("transfer_number").HasMaxLength(40).IsRequired();
            t.Property(x => x.Status).HasColumnName("status")
                .HasConversion<string>(
                    s => s.ToString().ToLowerInvariant(),
                    s => Enum.Parse<Domain.Enums.StockTransferStatus>(s, true))
                .HasMaxLength(20);
            t.Property(x => x.ShippedAt).HasColumnName("shipped_at");
            t.Property(x => x.ShippedBy).HasColumnName("shipped_by").HasMaxLength(100);
            t.Property(x => x.ReceivedAt).HasColumnName("received_at");
            t.Property(x => x.ReceivedBy).HasColumnName("received_by").HasMaxLength(100);
            t.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            t.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(255);
            t.Property(x => x.HasDivergence).HasColumnName("has_divergence");
            t.Property(x => x.Notes).HasColumnName("notes");
            t.Property(x => x.CreatedAt).HasColumnName("created_at");
            t.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            t.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            t.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            // A numeração é por filial de origem, então o par é que é único.
            t.HasIndex(x => new { x.BranchId, x.TransferNumber }).IsUnique();
            // A caixa de entrada do destino é uma consulta de rotina.
            t.HasIndex(x => new { x.DestinationBranchId, x.Status });

            t.HasMany(x => x.Items).WithOne(i => i.Transfer)
                .HasForeignKey(i => i.TransferId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockTransferItem>(i =>
        {
            i.ToTable("stock_transfer_items");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).HasColumnName("id");
            i.Property(x => x.TransferId).HasColumnName("transfer_id");
            i.Property(x => x.ProductId).HasColumnName("product_id");
            i.Property(x => x.QuantitySent).HasColumnName("quantity_sent").HasPrecision(10, 3);
            i.Property(x => x.QuantityReceived).HasColumnName("quantity_received").HasPrecision(10, 3);
            i.Property(x => x.CreatedAt).HasColumnName("created_at");
            i.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            i.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            i.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            i.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<Supplier>(s =>
        {
            s.ToTable("suppliers");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).HasColumnName("id");
            s.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(150).IsRequired();
            s.Property(x => x.TradeName).HasColumnName("trade_name").HasMaxLength(150);
            s.Property(x => x.Cnpj).HasColumnName("cnpj").HasMaxLength(18);
            s.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
            s.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
            s.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
            s.Property(x => x.ContactPerson).HasColumnName("contact_person").HasMaxLength(100);
            s.Property(x => x.IsActive).HasColumnName("is_active");
            s.Property(x => x.Notes).HasColumnName("notes");
            s.Property(x => x.CreatedAt).HasColumnName("created_at");
            s.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            s.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            s.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            s.HasIndex(x => x.Cnpj).IsUnique();
        });

        modelBuilder.Entity<ProductCategory>(pc =>
        {
            pc.ToTable("product_categories");
            pc.HasKey(x => x.Id);
            pc.Property(x => x.Id).HasColumnName("id");
            pc.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            pc.Property(x => x.ParentId).HasColumnName("parent_id");
            pc.Property(x => x.IsActive).HasColumnName("is_active");
            pc.Property(x => x.CreatedAt).HasColumnName("created_at");
            pc.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            pc.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            pc.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            pc.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
        });

        modelBuilder.Entity<CustomerType>(ct =>
        {
            ct.ToTable("customer_types");
            ct.HasKey(x => x.Id);
            ct.Property(x => x.Id).HasColumnName("id");
            ct.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            ct.Property(x => x.DiscountPct).HasColumnName("discount_pct").HasPrecision(5, 2);
            ct.Property(x => x.CreditLimit).HasColumnName("credit_limit").HasPrecision(12, 2);
            ct.Property(x => x.IsActive).HasColumnName("is_active");
            ct.Property(x => x.CreatedAt).HasColumnName("created_at");
            ct.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            ct.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            ct.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        });

        modelBuilder.Entity<CustomerCrmNote>(n =>
        {
            n.ToTable("customer_crm_notes");
            n.HasKey(x => x.Id);
            n.Property(x => x.Id).HasColumnName("id");
            n.Property(x => x.CustomerId).HasColumnName("customer_id");
            n.Property(x => x.Note).HasColumnName("note").IsRequired();
            n.Property(x => x.NoteType).HasColumnName("note_type").HasMaxLength(20);
            n.Property(x => x.CreatedAt).HasColumnName("created_at");
            n.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
        });

        modelBuilder.Entity<PaymentMethod>(pm =>
        {
            pm.ToTable("payment_methods");
            pm.HasKey(x => x.Id);
            pm.Property(x => x.Id).HasColumnName("id");
            pm.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            pm.Property(x => x.RequiresInstallments).HasColumnName("requires_installments");
            pm.Property(x => x.MaxInstallments).HasColumnName("max_installments");
            pm.Property(x => x.IsActive).HasColumnName("is_active");
            pm.Property(x => x.CreatedAt).HasColumnName("created_at");
            pm.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            pm.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            pm.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
        });

        modelBuilder.Entity<QuotationItem>(qi =>
        {
            qi.ToTable("quotation_items");
            qi.HasKey(x => x.Id);
            qi.Property(x => x.Id).HasColumnName("id");
            qi.Property(x => x.QuotationId).HasColumnName("quotation_id");
            qi.Property(x => x.ProductId).HasColumnName("product_id");
            qi.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(10, 3);
            qi.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
            qi.Property(x => x.DiscountPct).HasColumnName("discount_pct").HasPrecision(5, 2);
            qi.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(12, 2);
            qi.Property(x => x.CreatedAt).HasColumnName("created_at");
            qi.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            qi.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            qi.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            qi.HasOne(x => x.Product).WithMany(p => p.QuotationItems).HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<SaleItem>(si =>
        {
            si.ToTable("sale_items");
            si.HasKey(x => x.Id);
            si.Property(x => x.Id).HasColumnName("id");
            si.Property(x => x.SaleId).HasColumnName("sale_id");
            si.Property(x => x.ProductId).HasColumnName("product_id");
            si.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(10, 3);
            si.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
            si.Property(x => x.DiscountPct).HasColumnName("discount_pct").HasPrecision(5, 2);
            si.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(12, 2);
            si.Property(x => x.CreatedAt).HasColumnName("created_at");
            si.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            si.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            si.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            si.HasOne(x => x.Product).WithMany(p => p.SaleItems).HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<Payment>(p =>
        {
            p.ToTable("payments");
            p.HasKey(x => x.Id);
            p.Property(x => x.Id).HasColumnName("id");
            p.Property(x => x.BranchId).HasColumnName("branch_id");
            p.HasIndex(x => new { x.BranchId, x.PaidAt });
            p.Property(x => x.SaleId).HasColumnName("sale_id");
            p.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id");
            p.Property(x => x.Amount).HasColumnName("amount").HasPrecision(12, 2);
            p.Property(x => x.Installments).HasColumnName("installments");
            p.Property(x => x.Status).HasColumnName("status")
                .HasConversion<string>(
                    s => s.ToString().ToLowerInvariant(),
                    s => Enum.Parse<Domain.Enums.PaymentStatus>(s, true));
            p.Property(x => x.TransactionRef).HasColumnName("transaction_ref").HasMaxLength(100);
            p.Property(x => x.PaidAt).HasColumnName("paid_at");
            p.Property(x => x.RefundedAt).HasColumnName("refunded_at");
            p.Property(x => x.CreatedAt).HasColumnName("created_at");
            p.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            p.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            p.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            p.HasOne(x => x.PaymentMethod).WithMany(pm => pm.Payments).HasForeignKey(x => x.PaymentMethodId);
        });
    }
}
