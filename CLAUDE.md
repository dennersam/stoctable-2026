# Stoctable — CLAUDE.md

## Comandos de Desenvolvimento

### Backend (.NET 10)
```bash
# Rodar a API localmente
cd backend && dotnet run --project Stoctable.Api

# Build
cd backend && dotnet build Stoctable.slnx

# Testes
cd backend && dotnet test Stoctable.Tests

# Migrations (requer DEFAULT_CONN_STRING no ambiente)
cd backend && dotnet ef migrations add <NomeMigration> \
  --project Stoctable.Infrastructure \
  --startup-project Stoctable.Api

cd backend && dotnet ef database update \
  --project Stoctable.Infrastructure \
  --startup-project Stoctable.Api
```

### Frontend (React + Vite)
```bash
# Rodar localmente
cd frontend && npm run dev

# Build de produção
cd frontend && npm run build

# Lint
cd frontend && npm run lint
```

### Docker Compose (desenvolvimento local)
```bash
# Subir PostgreSQL local
docker-compose up -d postgres
```

## Arquitetura

### Backend — Clean Architecture
```
Domain       → Entidades, Enums, Interfaces de repositório (sem dependências)
Application  → Serviços de negócio, Validators (FluentValidation), Mappers (Mapster)
Infrastructure → EF Core DbContext, Repositórios, AuditInterceptor, TenantContext
Api          → Minimal API endpoints, Middleware, Program.cs
Communication → Request/Response DTOs (compartilhados entre Api e Application)
Exceptions   → Constantes de mensagens de erro
Tests        → Testes unitários xUnit
```

### Multi-Branch (Multi-Tenant)
- Cada filial tem um banco PostgreSQL separado: `stoctable_branch_{id}`
- Connection strings ficam no Azure KeyVault como `STOCTABLE-CONN-{BRANCH_ID}`
- O frontend envia o header `X-Branch-Id` em cada request
- `TenantResolutionMiddleware` resolve o connection string e popula `TenantContext` (scoped)
- `StoctableDbContext` usa o connection string do `TenantContext` dinamicamente

### Autenticação
- JWT Bearer com BCrypt para hash de senhas
- Roles: `admin`, `atendente`, `caixa`
- Tokens: access token (15 min) + refresh token (7 dias, armazenado no DB hasheado)

### Auditoria
- `AuditSaveChangesInterceptor` captura todas as mudanças EF Core
- Registra: entity, action (CREATE/UPDATE/DELETE), userId, username, IP, old_values (JSONB), new_values (JSONB)
- Tabela: `audit_logs` em cada banco de filial

## Variáveis de Ambiente

### Backend (appsettings.Development.json ou User Secrets)
```json
{
  "Jwt": {
    "Secret": "dev-secret-min-32-chars-xxxxxxxxxx",
    "Issuer": "stoctable-api",
    "Audience": "stoctable-app",
    "ExpirationMinutes": 15,
    "RefreshTokenDays": 7
  },
  "KeyVault": {
    "Url": ""
  },
  "DefaultBranchConnectionString": "Host=localhost;Database=stoctable_branch_dev;Username=postgres;Password=postgres"
}
```

### Frontend (.env)
```
VITE_API_BASE_URL=http://localhost:5000
```

## GitHub Secrets Necessários
| Secret | Descrição |
|--------|-----------|
| `AZURE_APP_SERVICE_PUBLISH_PROFILE` | Publish profile do App Service |
| `AZURE_STATIC_WEB_APPS_TOKEN` | Token do Static Web Apps |
| `BRANCH_CONNECTION_STRINGS_JSON` | JSON: `{"branch-id": "connstr"}` |
| `VITE_API_BASE_URL` | URL da API para o build do frontend |

## Estrutura de Pastas
```
Stoctable/
├── backend/
│   ├── Stoctable.slnx
│   ├── Stoctable.Domain/        # Entidades, Enums, Interfaces
│   ├── Stoctable.Application/   # Serviços, Validators, Mappers
│   ├── Stoctable.Infrastructure/ # EF Core, Repositórios, Tenant
│   ├── Stoctable.Api/           # Endpoints, Middleware, Program.cs
│   ├── Stoctable.Communication/ # DTOs de Request/Response
│   ├── Stoctable.Exceptions/    # Mensagens de erro
│   └── Stoctable.Tests/         # Testes xUnit
├── frontend/                    # React 19 + Vite + TypeScript + Tailwind
├── infrastructure/              # Terraform (Azure)
├── migrations/                  # Scripts SQL brutos (fallback)
└── .github/workflows/           # CI/CD GitHub Actions
```
