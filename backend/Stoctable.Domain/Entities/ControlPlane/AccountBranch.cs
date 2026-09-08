namespace Stoctable.Domain.Entities.ControlPlane;

/// <summary>
/// Quais filiais uma conta pode acessar. É contra esta tabela que a escolha de
/// filial é validada no momento de emitir o token — depois disso a filial vive
/// dentro do JWT assinado e nenhuma requisição precisa consultar o banco para
/// saber se o acesso é legítimo.
///
/// Não herda BaseEntity: é uma tabela de junção com chave composta, sem
/// identidade própria.
/// </summary>
public class AccountBranch
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
