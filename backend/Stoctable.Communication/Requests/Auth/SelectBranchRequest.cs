namespace Stoctable.Communication.Requests.Auth;

/// <summary>
/// Troca o token de pré-filial (ou o de uma sessão em andamento) por um token
/// amarrado à filial escolhida. É neste ponto — e só nele — que se verifica se
/// a conta tem acesso à loja pedida; depois disso a filial viaja assinada
/// dentro do JWT e nenhuma requisição precisa consultar o banco para saber.
/// </summary>
public record SelectBranchRequest(Guid BranchId);
