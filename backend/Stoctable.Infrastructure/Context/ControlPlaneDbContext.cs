using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Entities.ControlPlane;
using Stoctable.Domain.Enums;

namespace Stoctable.Infrastructure.Context;

/// <summary>
/// Banco de controle do SaaS: empresas, filiais, contas de login e o estado do
/// provisionamento. É separado do <see cref="StoctableDbContext"/> por dois
/// motivos que se reforçam:
///
/// 1. Ele precisa ser legível ANTES de existir um tenant — o login acontece
///    antes de saber qual banco de empresa abrir.
/// 2. Ele guarda as connection strings de todos os tenants, então não pode ser
///    ele próprio um tenant.
///
/// O histórico de migrations é próprio (<c>__EFMigrationsHistory_ControlPlane</c>)
/// para que os dois contextos jamais se confundam caso alguém aponte ambos para
/// o mesmo banco por engano.
///
/// Note que o AuditSaveChangesInterceptor NÃO é anexado aqui: ele escreve na
/// tabela <c>audit_logs</c>, que só existe no banco do tenant.
/// </summary>
public class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountBranch> AccountBranches => Set<AccountBranch>();
    public DbSet<ProvisioningJob> ProvisioningJobs => Set<ProvisioningJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(c =>
        {
            c.ToTable("companies");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).HasColumnName("id");
            c.Property(x => x.Cnpj).HasColumnName("cnpj").HasMaxLength(14).IsRequired();
            c.Property(x => x.RazaoSocial).HasColumnName("razao_social").HasMaxLength(200).IsRequired();
            c.Property(x => x.NomeFantasia).HasColumnName("nome_fantasia").HasMaxLength(200);
            c.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
            c.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
            c.Property(x => x.Status).HasColumnName("status")
                .HasConversion(s => s.ToString().ToLowerInvariant(), s => Enum.Parse<CompanyStatus>(s, true))
                .HasMaxLength(20);
            c.Property(x => x.DatabaseName).HasColumnName("database_name").HasMaxLength(63);
            c.Property(x => x.DatabaseProvider).HasColumnName("database_provider").HasMaxLength(20);
            c.Property(x => x.ConnectionStringEncrypted).HasColumnName("connection_string_encrypted");
            c.Property(x => x.ProvisionedAt).HasColumnName("provisioned_at");
            c.Property(x => x.CreatedAt).HasColumnName("created_at");
            c.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            c.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            c.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            c.HasIndex(x => x.Cnpj).IsUnique();
        });

        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("branches");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.CompanyId).HasColumnName("company_id");
            b.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            b.Property(x => x.Cnpj).HasColumnName("cnpj").HasMaxLength(14);
            b.Property(x => x.RazaoSocial).HasColumnName("razao_social").HasMaxLength(200).IsRequired();
            b.Property(x => x.NomeFantasia).HasColumnName("nome_fantasia").HasMaxLength(200);
            b.Property(x => x.IsHeadquarters).HasColumnName("is_headquarters");
            b.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
            b.Property(x => x.Neighborhood).HasColumnName("neighborhood").HasMaxLength(100);
            b.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
            b.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
            b.Property(x => x.ZipCode).HasColumnName("zip_code").HasMaxLength(9);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
            b.Property(x => x.IsActive).HasColumnName("is_active");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            b.Ignore(x => x.DisplayName);

            b.HasOne(x => x.Company).WithMany(x => x.Branches)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);

            // O código entra no prefixo dos documentos (ORC-PENHA-2026090001),
            // então precisa ser único dentro da empresa — duas filiais com o
            // mesmo código colidiriam na sequência de numeração.
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<Account>(a =>
        {
            a.ToTable("accounts");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).HasColumnName("id");
            a.Property(x => x.CompanyId).HasColumnName("company_id");
            a.Property(x => x.Email).HasColumnName("email").HasMaxLength(150).IsRequired();
            a.Property(x => x.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
            a.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            a.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            a.Property(x => x.Role).HasColumnName("role")
                .HasConversion(r => r.ToString().ToLowerInvariant(), s => Enum.Parse<UserRole>(s, true))
                .HasMaxLength(20);
            a.Property(x => x.IsActive).HasColumnName("is_active");
            a.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            a.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            a.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(128);
            a.Property(x => x.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");
            a.Property(x => x.PasswordResetTokenHash).HasColumnName("password_reset_token_hash").HasMaxLength(128);
            a.Property(x => x.PasswordResetTokenExpiresAt).HasColumnName("password_reset_token_expires_at");
            a.Property(x => x.CreatedAt).HasColumnName("created_at");
            a.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            a.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            a.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            a.HasOne(x => x.Company).WithMany(x => x.Accounts)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);

            // E-mail é a identidade de login: único no SaaS inteiro.
            a.HasIndex(x => x.Email).IsUnique();
            // Username é só apelido de exibição: único dentro da empresa, para
            // que duas empresas possam ter um "admin" cada.
            a.HasIndex(x => new { x.CompanyId, x.Username }).IsUnique();
            a.HasIndex(x => x.RefreshTokenHash);
        });

        modelBuilder.Entity<AccountBranch>(ab =>
        {
            ab.ToTable("account_branches");
            ab.HasKey(x => new { x.AccountId, x.BranchId });
            ab.Property(x => x.AccountId).HasColumnName("account_id");
            ab.Property(x => x.BranchId).HasColumnName("branch_id");
            ab.Property(x => x.CreatedAt).HasColumnName("created_at");

            ab.HasOne(x => x.Account).WithMany(x => x.Branches)
                .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            ab.HasOne(x => x.Branch).WithMany()
                .HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProvisioningJob>(j =>
        {
            j.ToTable("provisioning_jobs");
            j.HasKey(x => x.Id);
            j.Property(x => x.Id).HasColumnName("id");
            j.Property(x => x.CompanyId).HasColumnName("company_id");
            j.Property(x => x.Step).HasColumnName("step")
                .HasConversion(s => s.ToString().ToLowerInvariant(), s => Enum.Parse<ProvisioningStep>(s, true))
                .HasMaxLength(30);
            j.Property(x => x.State).HasColumnName("state")
                .HasConversion(s => s.ToString().ToLowerInvariant(), s => Enum.Parse<ProvisioningState>(s, true))
                .HasMaxLength(20);
            j.Property(x => x.Attempts).HasColumnName("attempts");
            j.Property(x => x.LastError).HasColumnName("last_error");
            j.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            j.Property(x => x.StatusToken).HasColumnName("status_token").HasMaxLength(64).IsRequired();
            j.Property(x => x.CreatedAt).HasColumnName("created_at");
            j.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
            j.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            j.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);

            j.HasOne(x => x.Company).WithMany()
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);

            j.HasIndex(x => x.StatusToken).IsUnique();
            // O poller varre por (state, next_attempt_at) procurando job travado.
            j.HasIndex(x => new { x.State, x.NextAttemptAt });
        });
    }
}
