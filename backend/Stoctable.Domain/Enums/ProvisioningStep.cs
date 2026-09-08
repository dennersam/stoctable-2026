namespace Stoctable.Domain.Enums;

/// <summary>
/// Passos do provisionamento, em ordem. Cada um é idempotente, e o valor
/// gravado em <c>provisioning_jobs.step</c> é o último passo CONCLUÍDO — assim
/// um processo que morre no meio retoma do ponto seguinte sem repetir efeito.
/// </summary>
public enum ProvisioningStep
{
    /// <summary>Nada feito ainda.</summary>
    Pending,

    /// <summary>Banco criado no provedor (Neon hoje, Azure depois).</summary>
    DatabaseCreated,

    /// <summary>Connection string cifrada e gravada na empresa.</summary>
    ConnectionStored,

    /// <summary>Migrations do tenant aplicadas.</summary>
    MigrationsApplied,

    /// <summary>Extensões, função de normalização e índices GIN da busca criados.</summary>
    SearchSchemaApplied,

    /// <summary>Formas de pagamento e tipos de cliente semeados.</summary>
    ReferenceDataSeeded,

    /// <summary>Filiais e usuário administrador projetados no banco do tenant.</summary>
    CompanyDataSeeded,

    /// <summary>Empresa marcada como Ready e cache de conexão invalidado.</summary>
    Completed
}

/// <summary>Estado de execução do job, ortogonal ao passo em que ele está.</summary>
public enum ProvisioningState
{
    Queued,
    Running,
    Succeeded,
    Failed
}
