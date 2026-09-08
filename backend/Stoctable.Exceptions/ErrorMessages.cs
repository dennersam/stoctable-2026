namespace Stoctable.Exceptions;

public static class ErrorMessages
{
    public static class Auth
    {
        public const string InvalidCredentials = "Usuário ou senha inválidos.";
        public const string InactiveUser = "Usuário inativo. Contate o administrador.";
        public const string InvalidRefreshToken = "Token de atualização inválido ou expirado.";
        public const string Unauthorized = "Acesso não autorizado.";
        public const string InvalidResetToken = "Link inválido ou expirado. Solicite um novo.";
        public const string WeakPassword = "A senha deve ter pelo menos 6 caracteres.";
        public const string BranchNotAllowed = "Você não tem acesso a esta filial.";
        public const string NoBranchAssigned = "Sua conta não está vinculada a nenhuma filial. Contate o administrador.";
        public const string BranchNotSelected = "Selecione uma filial para continuar.";
        public const string CompanyProvisioning = "Estamos preparando o ambiente da sua empresa. Tente novamente em instantes.";
        public const string CompanySuspended = "O acesso da sua empresa está suspenso. Contate o suporte.";
        public const string CompanyUnavailable = "O ambiente da sua empresa está indisponível. Contate o suporte.";
    }

    public static class User
    {
        public const string NotFound = "Usuário não encontrado.";
        public const string UsernameAlreadyExists = "Nome de usuário já está em uso.";
        public const string EmailAlreadyExists = "E-mail já está em uso.";
        public const string IncorrectPassword = "Senha atual incorreta.";
    }

    public static class Product
    {
        public const string NotFound = "Produto não encontrado.";
        public const string SkuAlreadyExists = "Código SKU já está em uso.";
        public const string BarcodeAlreadyExists = "Código de barras já está em uso.";
        public const string InsufficientStock = "Estoque insuficiente para o produto '{0}'.";
    }

    public static class Customer
    {
        public const string NotFound = "Cliente não encontrado.";
        public const string DocumentAlreadyExists = "CPF/CNPJ já cadastrado.";
    }

    public static class Quotation
    {
        public const string NotFound = "Orçamento não encontrado.";
        public const string CannotModify = "Orçamento não pode ser modificado no status atual: {0}.";
        public const string CannotFinalize = "Orçamento não pode ser finalizado. Verifique os itens e o estoque.";
        public const string CannotCancel = "Orçamento não pode ser cancelado no status atual: {0}.";
        public const string AlreadyConverted = "Orçamento já foi convertido em venda.";
        public const string EmptyItems = "Orçamento deve ter pelo menos um item.";
        public const string ConcurrentStockChange = "O estoque de um dos produtos foi alterado por outra operação. Recarregue o orçamento e tente novamente.";
    }

    public static class Sale
    {
        public const string NotFound = "Venda não encontrada.";
        public const string AlreadyPaid = "Venda já foi paga.";
        public const string PaymentExceedsTotal = "Valor do pagamento excede o total da venda.";
        public const string AlreadyCancelled = "Venda já foi cancelada.";
        public const string CancellationReasonRequired = "Informe o motivo do cancelamento.";
    }

    public static class Manufacturer
    {
        public const string NotFound = "Fabricante não encontrado.";
        public const string NameAlreadyExists = "Já existe um fabricante com este nome.";
    }

    public static class Supplier
    {
        public const string NotFound = "Fornecedor não encontrado.";
        public const string CnpjAlreadyExists = "CNPJ já cadastrado.";
    }

    public static class Inventory
    {
        public const string InsufficientStock = "Estoque insuficiente para o produto.";
    }
}
