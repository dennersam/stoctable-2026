namespace Stoctable.Migration;

// ─── SIC 6 raw models ────────────────────────────────────────────────────────
// Field names match exactly the SQL Server columns from SICNET_069255

public record SicSupplier(
    int Controle,
    string? Codigo,
    string? Empresa,
    string? Contato,
    string? Endereco,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? Cep,
    string? Telefone,
    string? Cgc,         // CNPJ
    string? Email,
    string? Obs
);

public record SicProduct(
    int Controle,
    string? Codigo,      // SKU legado
    string? Produto,     // nome
    string? Fabricante,  // texto livre → vira FK manufacturer
    int? LkFornec,       // FK TABFOR.controle
    decimal? PrecoCusto,
    decimal? PrecoVenda,
    double? Quantidade,
    double? EstMinimo,
    string? Unidade,
    bool? Inativo,
    string? Obs,
    string? Cean,        // EAN/barcode
    double? Ipi,
    double? Icms,
    string? Cst
);

public record SicCustomer(
    int Controle,
    string? Nome,
    string? Cgc,         // CPF ou CNPJ
    bool? TagFisica,     // true = pessoa física (CPF), false = jurídica (CNPJ)
    string? Endereco,
    string? EndNumero,
    string? EndComplemento,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? Cep,
    string? Telefone,
    string? Celular,
    string? Email,
    decimal? LimiteCred,
    bool? AtendBloq,     // true = bloqueado = inativo
    string? Obs,
    string? Contato
);

public record SicUser(
    int Controle,
    string? Nome,
    string? Senha,       // plain-text, max 7 chars → BCrypt na migração
    int? Nivel,          // 1 = admin, demais = atendente
    bool? Inativo
);
