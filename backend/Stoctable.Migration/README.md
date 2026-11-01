# Stoctable.Migration

Ferramenta de linha de comando para as operações que não cabem numa migration
do EF — as que precisam falar com o control plane e o tenant ao mesmo tempo, ou
que exigem julgamento humano antes de gravar.

```bash
cd backend/Stoctable.Migration
dotnet run -- <comando>
```

| Comando | O que faz |
|---|---|
| `sic` | Importa o sistema legado (SQL Server) para o Postgres. |
| `backfill` | Cria a empresa, as três filiais e as contas de login no control plane, e reaponta as linhas de escopo de filial do id provisório para a MEGA. |
| `verify` | Confere se o control plane e o tenant estão coerentes. |
| `stock-cutover [apply]` | Corte do estoque. Sem `apply`, só relata. |
| `import-lojas [--sem-historico]` | Importa as três lojas do SIC (MEGA, MOTO, VILLA) para um banco Stoctable, com vendas e orçamentos. |
| `payment-audit` | Lista vendas inconsistentes deixadas pelos caminhos antigos de pagamento. Só lê; sai com código 2 se achar algo. |

Connection strings vêm de `DEFAULT_CONN_STRING` (tenant) e
`CONTROL_PLANE_CONN_STRING` (control plane), que sobrescrevem o
`appsettings.json` — é assim que se aponta para produção sem gravar credencial
em arquivo versionado.

---

## Importação das três lojas (`import-lojas`)

Traz MEGAMOTOS, MOTOPENHA e VILLAMOTOS de uma vez para um único banco Stoctable,
**preservando os dados como estão no SIC** — cadastros, estoque, vendas e
orçamentos.

```bash
DEFAULT_CONN_STRING="Host=...;Database=...;Username=...;Password=..." \
SIC_MEGA_CONN_STRING="Server=host\SQLEXPRESS;Database=SIC_MEGA;Trusted_Connection=True;TrustServerCertificate=True;" \
SIC_MOTO_CONN_STRING="...Database=SIC_MOTO..." \
SIC_VILLA_CONN_STRING="...Database=SIC_VILLA..." \
dotnet run -- import-lojas
```

Os ids de filial vêm de `BRANCH_ID_MEGA`, `BRANCH_ID_MOTO` e `BRANCH_ID_VILLA`.
Sem eles, o comando usa um id determinístico derivado do código da loja — bom
para banco de teste, **errado para produção**, onde os ids têm de ser os do
control plane.

⚠️ **As tabelas de destino são esvaziadas antes da carga.** O comando é uma carga
inicial, não uma sincronização incremental.

### Por que o catálogo não é unificado

O SIC não tem chave que identifique o mesmo produto entre lojas:

- o **código de barras está 100% vazio** nas três bases;
- o `Codigo` é sequência interna de cada loja — dos 12.899 códigos que MEGA e
  MOTOPENHA têm em comum, **só metade tem o mesmo produto**. O código 1149 é
  "Parafuso Coroa CG/ML/TITAN" numa loja e "Parafuso Coroa T.T125" na outra.

Unificar por código fundiria o estoque de peças diferentes. Então cada loja traz
o catálogo inteiro, com SKU prefixado (`MEGA-1214`, `MOTO-1214`), e o produto
ganha linha em `product_stocks` **apenas na filial de origem** — nas outras
aparece com saldo zero, que é o comportamento normal do modelo para produto
nunca movimentado ali. Nada de schema muda.

**Cliente e fornecedor, ao contrário, são unificados por CPF/CNPJ**, onde a chave
é confiável: 512 linhas de fornecedor viram 331 registros. Quem não tem documento
fica separado por loja.

### SKU e o código antigo do SIC

O SKU é **numérico sequencial**, contínuo entre as lojas — o mesmo formato que o
`ProductService` gera para produto novo (`GetNextSkuAsync` devolve o maior
inteiro + 1). Cada loja acaba ocupando uma faixa:

| Loja | Faixa |
|---|---|
| MEGA | 1 – 14504 |
| MOTOPENHA | 14505 – 29229 |
| VILLAMOTOS | 29230 – 43544 |

**O código do SIC não pode virar o SKU:** ele é sequência interna de cada loja, e
o mesmo `1214` existe nas três apontando para peças diferentes. Ele fica
preservado nas **observações** do produto, na forma `Código SIC: MEGA-1214`,
junto de qualquer observação que já viesse do legado.

⚠️ **A busca não encontra pelo código antigo.** Os campos pesquisados são nome,
SKU, código de barras e fabricante — observações não entram. Se a equipe ainda
procura peça pelo código do SIC, a alternativa é gravá-lo em `barcode`, que está
100% vazio e é indexado pela busca.

A numeração é **reproduzível**: a consulta de produtos tem `ORDER BY` explícito
(numéricos primeiro, na ordem do código; não-numéricos depois), então recarregar
gera exatamente os mesmos SKUs para os mesmos produtos. Sem esse `ORDER BY` o SQL
Server não garante ordem e uma recarga renumeraria o catálogo inteiro — com a
equipe já usando os números antigos.

### Decisões que valem revisar antes de rodar em produção

- **Orçamentos entram como `cancelled`**, com o motivo "importado do SIC —
  orçamento histórico, não efetivado". É deliberado: orçamento finalizado
  reservaria estoque e o saldo das lojas nasceria errado. A carga não cria
  nenhuma `StockReservation`.
- **Vendas entram como `paid`.** O SIC tem uma coluna `cancelada`, mas ela está
  nula em todas as linhas — não há marcação de cancelamento aproveitável.
- **Usuários entram inativos e com hash de senha inválido.** Existem só para que
  as vendas antigas tenham vendedor; o login real é pelo control plane.
- As colunas de estoque em `products` ficam zeradas: o saldo vai para
  `product_stocks`.

### Reconciliação (conferido em 08/09/2026)

| | SIC | Importado |
|---|---|---|
| Produtos | 43.544 | 43.544 |
| Estoque (qtd) | 57.179 / 114.724 / 32.756 | idêntico nas três |
| Vendas | 681.029 | 681.029 |
| Orçamentos | 27.387 | 27.387 |
| Itens de venda | 1.189.699 | 1.181.164 |
| Itens de orçamento | 105.918 | 105.137 |
| Pagamentos (de vendas) | 545.383 | 544.669 |

As diferenças são **inteiramente explicadas por registros órfãos do SIC** e
batem linha a linha:

- **8.535 itens de venda** e **781 de orçamento** apontam para produtos que já não
  existem em `TABEST1`. O Postgres tem FK e o SIC não; descartar o item preserva
  o documento.
- **714 pagamentos** referenciam forma de pagamento inexistente.
- Os demais 17.118 registros de `TABEST3R` pertencem a orçamentos ou a documentos
  apagados — orçamento não tem pagamento no modelo.

⚠️ **Consequência a conhecer:** 8.256 vendas (1,2% do total) têm `total_amount`
fiel ao SIC mas com um item a menos, porque o produto sumiu do cadastro. O total
financeiro foi mantido de propósito — é o valor efetivamente cobrado. Se preferir
que a venda feche com a soma dos itens, recalcule `total_amount` após a carga.

A soma financeira reflete isso: VILLA bate ao centavo (nenhum órfão), MEGA fica
R$ 410 mil abaixo em itens e MOTOPENHA R$ 18 mil, exatamente o valor dos itens
descartados.

---

## Corte do estoque para `product_stocks`

Antes, o estoque era uma coluna na linha do produto, que é entidade da EMPRESA.
Isso tornava "catálogo compartilhado" e "estoque por loja" mutuamente
impossíveis. Hoje a fonte da verdade é `product_stocks`, com chave
`(branch_id, product_id)`; as colunas antigas de `products` continuam no banco,
mortas, apenas como rede de segurança para reconciliação.

Este é o procedimento para remover essa rede. **A ordem importa, e o último
passo é irreversível na prática** — depois dele não existe mais com o que
comparar.

### 1. Backfill (no deploy que sobe o código novo)

```bash
dotnet run -- stock-cutover          # relatório, só leitura
dotnet run -- stock-cutover apply    # grava
```

O saldo de `products.stock_quantity` vira o estoque da filial **MEGA**; MOTO e
VILLA começam zeradas e são preenchidas por inventário ou transferência. O
comando é idempotente e não toca em nenhuma outra filial.

**Momento certo:** no mesmo deploy do código novo. Antes disso o dual-write
ainda mantinha as duas fontes próximas; depois, `products.stock_quantity`
congela e passa a divergir de forma legítima conforme as lojas movimentam
estoque. Rodar tarde demais sobrescreve saldo novo com um retrato velho — o
comando avisa quando detecta que outras filiais já têm saldo, mas não bloqueia,
porque a decisão é de quem conhece a operação.

### 2. Observação (pelo menos um ciclo de produção)

```bash
dotnet run -- stock-cutover    # precisa fechar em zero divergências
```

Sai com código 2 enquanto houver divergência, então dá para agendar e ser
avisado. Divergência aqui significa que algum caminho de escrita ficou de fora
da migração — investigue antes de seguir.

### 3. Derrubar as colunas (outro deploy, depois da janela)

⚠️ **A migration deste passo ainda não existe, de propósito.** O workflow
`migrations.yml` roda logo após o `Backend CI/CD` num push para `main`: uma
migration de drop presente no repositório seria aplicada junto com o código
novo, sem a janela de observação acima. Ela deve ser criada no momento de
executar o passo 3, e não antes.

Quando for a hora:

1. Remover de `Stoctable.Domain/Entities/Product.cs` as três propriedades
   marcadas como `COLUNA MORTA` (`StockQuantity`, `StockReserved`,
   `StockMinimum`) e as linhas correspondentes em `ProductConfiguration.cs`.
2. Conferir que ninguém mais as referencia:
   ```bash
   grep -rn "StockQuantity\|StockReserved\|StockMinimum\|stock_quantity\|stock_reserved" \
     --include=*.cs backend/ | grep -v /obj/ | grep -v Migrations/
   ```
   O que sobrar em `ProductResponse` é a forma pública da API, que permanece —
   ela passou a ser preenchida a partir de `product_stocks`, e o frontend não
   precisa mudar.
3. Gerar a migration:
   ```bash
   dotnet ef migrations add DropLegacyStockColumns \
     --context StoctableDbContext \
     --project Stoctable.Infrastructure \
     --startup-project Stoctable.Api
   ```
4. Deploy sozinho, sem outras mudanças junto.

Este arquivo e o `StockCutoverTests` deixam de fazer sentido depois disso e
podem ser removidos no mesmo commit.
