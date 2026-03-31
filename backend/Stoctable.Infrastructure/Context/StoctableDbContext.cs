using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context.Configurations;

namespace Stoctable.Infrastructure.Context;

public class StoctableDbContext(DbContextOptions<StoctableDbContext> options) : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as IEntityTypeConfiguration<T> deste assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoctableDbContext).Assembly);

        // Mapeamentos simples sem configuração separada
        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("branches");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            b.Property(x => x.Cnpj).HasColumnName("cnpj").HasMaxLength(18);
            b.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
            b.Property(x => x.IsActive).HasColumnName("is_active");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
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
            p.Property(x => x.CreatedAt).HasColumnName("created_at");
            p.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            p.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            p.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
            p.HasOne(x => x.PaymentMethod).WithMany(pm => pm.Payments).HasForeignKey(x => x.PaymentMethodId);
        });
    }
}
