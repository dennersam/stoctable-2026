namespace Stoctable.Domain.Enums;

/// <summary>
/// Ciclo de vida de uma empresa no control plane. Enquanto não estiver
/// <see cref="Ready"/>, o login é recusado com um código próprio para que o
/// frontend mostre a tela de "preparando seu ambiente" em vez do dashboard.
/// </summary>
public enum CompanyStatus
{
    /// <summary>Cadastro feito; o banco ainda está sendo criado.</summary>
    Provisioning,

    /// <summary>Banco criado, migrations aplicadas, pronto para uso.</summary>
    Ready,

    /// <summary>O provisionamento esgotou as tentativas. Exige intervenção.</summary>
    Failed,

    /// <summary>Acesso bloqueado (inadimplência, pedido do cliente).</summary>
    Suspended
}
