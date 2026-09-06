variable "jwt_secret" {
  type        = string
  description = "Segredo de assinatura do JWT (mínimo 32 caracteres)."
  sensitive   = true

  validation {
    condition     = length(var.jwt_secret) >= 32
    error_message = "O segredo do JWT precisa ter ao menos 32 caracteres."
  }
}

variable "branch_connection_strings" {
  type        = map(string)
  description = <<-EOT
    Mapa branch_id => connection string do Neon. Cada entrada vira o segredo
    STOCTABLE-CONN-{ID} no Key Vault, resolvido pelo header X-Branch-Id.
    Exemplo de chave: "001".
  EOT
  sensitive   = true
}

variable "default_branch_connection_string" {
  type        = string
  description = "Connection string usada quando não há filial resolvida (/api/auth, seeding, design-time)."
  sensitive   = true
}
