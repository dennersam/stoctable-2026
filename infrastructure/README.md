# Infraestrutura — Stoctable

Terraform para Azure, com dois ambientes independentes.

| | **dev** | **prod** |
|---|---|---|
| Resource Group | `Stoctable-Dev` | `Stoctable-Prod` |
| Banco de dados | **Neon** (externo) | Azure PostgreSQL Flexible Server |
| App Service | `stoctable-api-dev` — **B1** | `stoctable-api` — P1v3 |
| Static Web App | `stoctable-web-dev` — Free | `stoctable-web` — Standard |
| Key Vault | `stoctable-kv-dev` | `stoctable-kv` |
| State | `dev.terraform.tfstate` | `prod.terraform.tfstate` |

O ambiente de dev **não cria banco na Azure** — o módulo `postgresql` não é
instanciado lá. As connection strings do Neon são **montadas** em
`environments/dev/main.tf` (local `branch_connection_strings`) a partir de
`neon_host`, `neon_username`, `neon_password` e do mapa `branch_databases`, e
daí entram no Key Vault. Ninguém digita uma connection string inteira.

## Como o app encontra o banco

`TenantResolutionMiddleware` lê o header `X-Branch-Id` e busca no Key Vault o
segredo `STOCTABLE-CONN-{ID}` (em maiúsculas). Por isso o Terraform escreve os
segredos com `upper(branch_id)`.

Dois segredos têm nomes com significado especial, porque o provider de
configuração do Key Vault converte `--` em `:`:

| Segredo no Key Vault | Chave de configuração no .NET |
|---|---|
| `Jwt--Secret` | `Jwt:Secret` |
| `DefaultBranchConnectionString` | `DefaultBranchConnectionString` |
| `STOCTABLE-CONN-001` | *(lido direto pelo `SecretClient`, não pelo config)* |

> Um segredo chamado `STOCTABLE-JWT-SECRET` **não** funciona: viraria a chave
> `STOCTABLE-JWT-SECRET`, e `Program.cs` lê `Jwt:Secret`. A aplicação falha no
> startup com `InvalidOperationException("Jwt:Secret não configurado.")`.

### Toda filial usada precisa estar em `branch_databases`

O mapa `branch_databases` (em `environments/dev/variables.tf`) é a **única**
fonte dos segredos `STOCTABLE-CONN-*`. Um `X-Branch-Id` que não tenha entrada
lá gera 404 no Key Vault em toda requisição. Hoje o mapa contém:

| `X-Branch-Id` | Database no Neon | Origem do valor |
|---|---|---|
| `dev` | `neondb` | fallback embutido no frontend (`src/lib/api.ts`) |
| `001` | `stoctable_branch_001` | filial escolhida no login |
| `002` | `stoctable_branch_002` | filial escolhida no login |

A entrada `dev` existe porque o cliente HTTP do frontend manda `X-Branch-Id: dev`
enquanto nenhuma filial foi selecionada. Ao mudar esse default no frontend,
mude o mapa junto — os dois têm de casar.

## Subindo o ambiente de dev — passo a passo

### 1. Pré-requisitos

1. **Azure CLI autenticado na subscription certa** — o Terraform e o plano de
   dados do Key Vault usam essa credencial:
   ```powershell
   az login
   az account set --subscription "Stoctable-Development"
   az account show --query name -o tsv
   ```
   A conta precisa ser Owner (ou Contributor + User Access Administrator) do
   resource group: o apply cria uma role assignment.
2. **Databases no Neon**, um por filial do mapa acima, mais o `neondb` padrão.
   Anote apenas **host, usuário e senha** — não a URL `postgres://`.
3. **Storage do state** (só na primeira vez):
   ```powershell
   az group create -n stoctable-tfstate -l brazilsouth
   az storage account create -n stoctabletfstate -g stoctable-tfstate -l brazilsouth --sku Standard_LRS
   az storage container create -n tfstate --account-name stoctabletfstate
   ```

### 2. Apply

Não existe `terraform.tfvars` neste ambiente: os quatro valores sensíveis são
perguntados no terminal a cada plan/apply. Como o Terraform **não oculta** o que
se digita no prompt, prefira exportar as variáveis de ambiente:

```powershell
cd infrastructure/environments/dev

$env:TF_VAR_jwt_secret    = '<32+ caracteres>'
$env:TF_VAR_neon_host     = 'ep-....aws.neon.tech'   # só o host
$env:TF_VAR_neon_username = 'neondb_owner'
$env:TF_VAR_neon_password = '<senha do Neon>'

terraform init
terraform validate
terraform plan
terraform apply
```

**Nunca envolva esses valores em aspas.** As validações em `variables.tf`
barram aspas, URL `postgres://`, connection string inteira e os placeholders de
exemplo (`ep-xxxx`, `REGIAO`, `USUARIO`) — cada uma dessas regras existe porque
o erro correspondente já derrubou um deploy. Veja *Troubleshooting*.

### 3. Reiniciar a API

Os segredos são lidos no startup e ficam em cache. Depois de qualquer mudança
no Key Vault, o restart é obrigatório — sem ele o app continua com o valor
antigo:

```powershell
az webapp restart -g Stoctable-Dev -n stoctable-api-dev
```

### 4. Verificar

```powershell
$API = terraform output -raw api_url
curl "$API/health"                                   # {"status":"healthy",...}
curl -i "$API/api/products"                          # 400 — header X-Branch-Id obrigatório
curl -i -H "X-Branch-Id: dev" "$API/api/products"    # 401 (não 503, não 404)
```

E confira o log de startup — o seeding roda antes de a API atender:

```powershell
az webapp log tail -g Stoctable-Dev -n stoctable-api-dev
```

Uma linha `Falha ao aplicar migrations/seed no startup` significa que o
`DefaultBranchConnectionString` está inválido; a API sobe assim mesmo, e por
isso o `/health` pode responder 200 com o banco inacessível.

## Troubleshooting

Os três erros abaixo aconteceram na configuração inicial e foram diagnosticados
lendo os valores reais do vault. O comando que revela cada um:

```powershell
az keyvault secret list --vault-name stoctable-kv-dev --query "[].name" -o tsv
$v = az keyvault secret show --vault-name stoctable-kv-dev --name DefaultBranchConnectionString --query value -o tsv
"LEN=$($v.Length) FIRST=[$($v[0])] LAST=[$($v[-1])]"
```

**`ArgumentException: Format of the initialization string ... at index N`**
O valor do segredo estava **envolvido em aspas duplas** — resquício de uma
versão anterior em que a connection string inteira era digitada no prompt e foi
colada com as aspas. O índice do erro aponta exatamente a aspa final. `FIRST` e
`LAST` no comando acima têm de ser `H` e `;`, nunca `"`.

**`SecretNotFound: STOCTABLE-CONN-XXX was not found`**
O `X-Branch-Id` recebido não tem entrada em `branch_databases`. Foi o caso do
`dev`, o fallback do frontend, que não estava no mapa. Acrescente a filial ao
mapa e reaplique — não crie o segredo à mão, ou o próximo apply o remove.

**Conecta, mas o host não resolve / autenticação falha**
Os segredos ainda guardam os **placeholders de exemplo** (`ep-xxxx`, `REGIAO`,
`USUARIO`) de um apply feito antes de as validações existirem. Reaplique com os
valores reais.

**503 em vez de 401** — o segredo não foi encontrado *ou* a managed identity do
App Service não recebeu `Key Vault Secrets User` (`azurerm_role_assignment`
`app_kv_secrets` em `main.tf`).

**CORS: "No 'Access-Control-Allow-Origin' header"** — quase sempre é o
`UseHttpsRedirection` devolvendo 307 no preflight. Exige
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (já definido no módulo
`app_service`) e `Cors__AllowedOrigins` apontando para o host da SWA.

## Secrets necessários no GitHub

| Secret | Usado por |
|---|---|
| `AZURE_APP_SERVICE_PUBLISH_PROFILE_DEV` | deploy dev do backend |
| `AZURE_APP_SERVICE_PUBLISH_PROFILE` | deploy prod do backend |
| `AZURE_STATIC_WEB_APPS_TOKEN_DEV` | deploy dev do frontend |
| `AZURE_STATIC_WEB_APPS_TOKEN` | deploy prod do frontend |
| `VITE_API_BASE_URL_DEV` | build do frontend (dev) |
| `VITE_API_BASE_URL` | build do frontend (prod) |
Criar também os *environments* `development` e `production` em
Settings → Environments do repositório.

### Secret de environment (não de repositório)

`BRANCH_CONNECTION_STRINGS_JSON` é definido **dentro de cada environment**, com
o mesmo nome e valores diferentes — o job de migrations escolhe o environment e
o GitHub resolve o valor certo. Não crie este como secret de repositório: o job
leria o valor do ambiente errado.

| Onde | Valor |
|---|---|
| Environment `development` | JSON com as strings do **Neon** |
| Environment `production` | JSON com as strings do **Postgres da Azure** |

Formato: `{"001": "Host=...;Database=...", "002": "Host=..."}` — as chaves são
os IDs de filial enviados no header `X-Branch-Id`.

## Fluxo de deploy

**Desenvolvimento — automático.** Push em `main` dispara build, testes, deploy
do backend e do frontend em dev e, na sequência, as migrations no Neon.

**Produção — somente manual.** Nada publica em produção por push. É preciso ir
em Actions, escolher o workflow (Backend ou Frontend CI/CD), clicar em *Run
workflow* a partir de `main` e marcar **`deploy_production`**. As migrations de
produção são um acionamento à parte: *Database Migrations* → *Run workflow* com
`target_environment = prod`.

Três travas independentes protegem produção:

| Trava | O que impede |
|---|---|
| `github.event_name == 'workflow_dispatch'` | qualquer publicação por push |
| `inputs.deploy_production` | disparo manual sem intenção explícita |
| `github.ref == 'refs/heads/main'` | publicar código de outro branch |

Além disso os jobs de produção declaram `environment: production`. Configure
*required reviewers* nesse environment (Settings → Environments) para exigir
aprovação de alguém antes de cada publicação — é aí que essa proteção vive, não
no arquivo do workflow.

Em pull request o frontend apenas roda lint e build, sem publicar: a SWA no
plano Free não oferece preview environments.

## SKU do App Service de dev

Hoje é **B1**. O F1 (gratuito) foi abandonado: a cota de 60 min de CPU/dia se
esgotava em poucas horas e a Azure suspendia o site (`QuotaExceeded`) até a
meia-noite UTC.

Ao voltar para F1, `always_on` **precisa** virar `false` no mesmo commit — F1
não suporta Always On e o apply falha. Os dois valores andam juntos.

`always_on = true` desde que o provisionamento de novas empresas passou a rodar
como `IHostedService` dentro do App Service — sem isso o processo é descarregado
na ociosidade e um provisionamento em andamento fica parado. De quebra elimina o
cold start de 20-40s. O Neon no plano gratuito continua hibernando, então a
primeira query após ociosidade ainda é lenta; isso não é defeito.

## Notas sobre os módulos

- A `azurerm_role_assignment` que dá acesso do App Service ao Key Vault fica no
  **módulo raiz**, não no módulo `key_vault`. Se ela voltar para lá, o Terraform
  aborta com `Cycle`: o App Service precisa da URI do vault e o vault precisaria
  do principal do App Service.
- `azurerm_key_vault_secret` usa `for_each = nonsensitive(toset(keys(var.secrets)))`
  porque `for_each` não aceita valor sensível. Só as **chaves** são destravadas;
  os valores continuam marcados como sensíveis.
