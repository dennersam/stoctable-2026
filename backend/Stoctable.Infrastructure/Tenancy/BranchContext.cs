namespace Stoctable.Infrastructure.Tenancy;

/// <summary>
/// Filial ativa da requisição. Serviço com escopo de requisição.
///
/// É separado do <see cref="TenantContext"/> de propósito: aquele resolve QUAL
/// BANCO abrir, este resolve QUAIS LINHAS enxergar dentro dele. Os dois conceitos
/// coincidiam enquanto cada filial tinha um banco próprio, e deixam de coincidir
/// agora que uma empresa tem um banco com várias filiais dentro.
///
/// Hoje o valor vem de configuração e é sempre o mesmo — o banco de cada cliente
/// ainda tem uma filial só. Na fase 3 a população passa a vir da claim assinada
/// do JWT, e é só isso que muda: todo o resto do código já lê daqui.
/// </summary>
public class BranchContext
{
    /// <summary>
    /// Filial usada enquanto a filial real não vem do token. As linhas criadas
    /// pelo backfill de <c>product_stocks</c> nascem com este id, e a fase 3
    /// as remapeia para o id verdadeiro da filial no control plane.
    ///
    /// O valor é literal na migration de backfill — mudar aqui exige mudar lá.
    /// </summary>
    public static readonly Guid LegacySingleBranchId = new("00000000-0000-0000-0000-000000000001");

    public Guid BranchId { get; set; } = LegacySingleBranchId;

    /// <summary>
    /// Filiais que esta conta pode acessar, vindas das claims <c>branch</c> do
    /// token — portanto assinadas, e não sujeitas ao que o cliente mandar no
    /// corpo da requisição.
    ///
    /// É o que autoriza as duas únicas operações legítimas que olham além da
    /// filial ativa: escolher o destino de uma transferência e consultar o
    /// estoque da rede. Nunca use isto para ESCREVER em outra filial.
    /// </summary>
    public IReadOnlyCollection<Guid> AllowedBranchIds { get; set; } = [];
}
