# Infraestrutura — Stoctable

Terraform para Azure, com dois ambientes independentes.

| | **dev** | **prod** |
|---|---|---|
| Resource Group | `Stoctable-Dev` | `Stoctable-Prod` |
| Banco de dados | **Neon** (externo) | Azure PostgreSQL Flexible Server |
| App Service | `stoctable-api-dev` — **F1 (gratuito)** | `stoctable-api` — P1v3 |
| Static Web App | `stoctable-web-dev` — Free | `stoctable-web` — Standard |
| Key Vault | `stoctable-kv-dev` | `stoctable-kv` |
| State | `dev.terraform.tfstate` | `prod.terraform.tfstate` |

O ambiente de dev **não cria banco na Azure** — o módulo `postgresql` não é
instanciado lá. As connection strings do Neon entram no Key Vault através da
variável `branch_connection_strings`.

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

## Subindo o ambiente de dev

**Pré-requisitos**

1. Criar no Neon um database por filial (`stoctable_branch_001`, …) e coletar a
   connection string de cada um no formato Npgsql (`Host=...;Database=...`),
   e não a URL `postgres://`.
2. Garantir que o storage do state existe:
   ```bash
   az group create -n stoctable-tfstate -l brazilsouth
   az storage account create -n stoctabletfstate -g stoctable-tfstate -l brazilsouth --sku Standard_LRS
   az storage container create -n tfstate --account-name stoctabletfstate
   ```

**Apply**

```bash
cd infrastructure/environments/dev
cp terraform.tfvars.example terraform.tfvars   # preencha os valores
terraform init
terraform validate
terraform plan
terraform apply
```

**Depois do apply**

```bash
API=$(terraform output -raw api_url)
curl "$API/health"                              # {"status":"healthy",...}
curl -i "$API/api/products"                     # 400 — header X-Branch-Id obrigatório
curl -i -H "X-Branch-Id: 001" "$API/api/products"  # 401 (não 503)
```

Um **503** em vez de 401 significa que o segredo `STOCTABLE-CONN-001` não foi
encontrado ou que a managed identity não recebeu o papel `Key Vault Secrets User`.

Na F1 a primeira requisição após ociosidade leva 20-40s (sem Always On), e o
Neon no plano gratuito também hiberna. Não é defeito.

## Secrets necessários no GitHub

| Secret | Usado por |
|---|---|
| `AZURE_APP_SERVICE_PUBLISH_PROFILE_DEV` | deploy dev do backend |
| `AZURE_APP_SERVICE_PUBLISH_PROFILE` | deploy prod do backend |
| `AZURE_STATIC_WEB_APPS_TOKEN_DEV` | deploy dev do frontend |
| `AZURE_STATIC_WEB_APPS_TOKEN` | deploy prod do frontend |
| `VITE_API_BASE_URL_DEV` | build do frontend (dev) |
| `VITE_API_BASE_URL` | build do frontend (prod) |
| `BRANCH_CONNECTION_STRINGS_JSON_DEV` | migrations no Neon |
| `BRANCH_CONNECTION_STRINGS_JSON` | migrations em prod |

Os dois últimos são um JSON `{"001": "Host=...", "002": "Host=..."}`.

Criar também os *environments* `development` e `production` em
Settings → Environments do repositório.

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

## Trocar F1 por B1

No `environments/dev/main.tf`, no módulo `app_service`:

```hcl
  sku_name  = "B1"
  always_on = true
```

`always_on = true` com F1 faz o apply falhar — os dois valores andam juntos.

## Notas sobre os módulos

- A `azurerm_role_assignment` que dá acesso do App Service ao Key Vault fica no
  **módulo raiz**, não no módulo `key_vault`. Se ela voltar para lá, o Terraform
  aborta com `Cycle`: o App Service precisa da URI do vault e o vault precisaria
  do principal do App Service.
- `azurerm_key_vault_secret` usa `for_each = nonsensitive(toset(keys(var.secrets)))`
  porque `for_each` não aceita valor sensível. Só as **chaves** são destravadas;
  os valores continuam marcados como sensíveis.
