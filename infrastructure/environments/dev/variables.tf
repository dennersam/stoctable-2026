# Nenhuma destas variáveis tem default e nenhuma é lida de terraform.tfvars:
# o Terraform pergunta cada uma no terminal a cada plan/apply, de modo que
# nenhum segredo fica em arquivo no disco.
#
# Para automação (CI) ou para evitar redigitar, use variáveis de ambiente —
# o Terraform as lê sozinho e elas não tocam o disco:
#   export TF_VAR_neon_password='...'
#
# Atenção: o Terraform NÃO oculta o que você digita no prompt, mesmo em
# variáveis marcadas como sensitive. Para não expor a senha na tela, prefira
# TF_VAR_neon_password. O valor gravado no state e no Key Vault continua
# marcado como sensível de qualquer forma.

variable "jwt_secret" {
  type        = string
  description = "Segredo de assinatura do JWT (mínimo 32 caracteres)"
  sensitive   = true

  validation {
    condition     = length(var.jwt_secret) >= 32
    error_message = "O segredo do JWT precisa ter ao menos 32 caracteres."
  }
}

variable "neon_host" {
  type        = string
  description = "Host do Neon, ex: ep-divine-sky-ae4edken-pooler.c-2.us-east-2.aws.neon.tech"

  validation {
    condition     = !can(regex("^\\s*[\"']|[\"']\\s*$", var.neon_host))
    error_message = "Não inclua aspas: digite apenas o host, sem envolvê-lo em \" ou '."
  }

  validation {
    condition     = !can(regex("(?i)^\\s*(postgres|postgresql)://|;|=", var.neon_host))
    error_message = "Informe somente o host (ep-....neon.tech), não a URL postgres:// nem a connection string inteira."
  }

  validation {
    condition     = !can(regex("ep-xxxx|REGIAO", var.neon_host))
    error_message = "Host de exemplo detectado. Use o endereço real do painel do Neon."
  }
}

variable "neon_username" {
  type        = string
  description = "Usuário do Neon, ex: neondb_owner"

  validation {
    condition     = !can(regex("^\\s*[\"']|[\"']\\s*$|USUARIO", var.neon_username))
    error_message = "Usuário inválido: não use aspas nem o valor de exemplo."
  }
}

variable "neon_password" {
  type        = string
  description = "Senha do Neon"
  sensitive   = true

  validation {
    condition     = length(var.neon_password) > 0 && !can(regex("^\\s*[\"']|[\"']\\s*$|^SENHA$", var.neon_password))
    error_message = "Senha inválida: não pode ser vazia, ter aspas envolvendo o valor, nem ser o texto de exemplo."
  }
}

# Não é segredo, então fica versionado aqui em vez de virar prompt.
variable "branch_databases" {
  type        = map(string)
  description = "Mapa branch_id => nome do database no Neon. A chave é o valor enviado no header X-Branch-Id."
  default = {
    # O frontend usa "dev" como filial padrão enquanto nenhuma é escolhida no
    # login (src/lib/api.ts). Sem esta entrada o middleware procura
    # STOCTABLE-CONN-DEV no vault e toma 404 em toda requisição.
    "dev" = "neondb"

    "001" = "stoctable_branch_001"
    "002" = "stoctable_branch_002"
  }
}

variable "default_database" {
  type        = string
  description = "Database usado quando não há filial resolvida (/api/auth, seeding no startup, design-time)."
  default     = "neondb"
}
