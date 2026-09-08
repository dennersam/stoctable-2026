namespace Stoctable.Communication.Requests.Auth;

/// <summary>
/// <paramref name="BranchId"/> preserva a filial ativa ao renovar a sessão —
/// sem ele, uma conta com várias lojas seria devolvida à tela de escolha a cada
/// renovação de token. A filial pedida continua sendo validada contra as
/// permissões atuais da conta, relidas do banco.
/// </summary>
public record RefreshTokenRequest(string RefreshToken, Guid? BranchId = null);
