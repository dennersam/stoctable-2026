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

Connection strings vêm de `DEFAULT_CONN_STRING` (tenant) e
`CONTROL_PLANE_CONN_STRING` (control plane), que sobrescrevem o
`appsettings.json` — é assim que se aponta para produção sem gravar credencial
em arquivo versionado.

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
