variable "name" {
  type        = string
  description = "Nome do Key Vault (único globalmente, máx. 24 caracteres)."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "tenant_id" {
  type = string
}

variable "secrets" {
  type      = map(string)
  sensitive = true
  default   = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}

resource "azurerm_key_vault" "this" {
  name                       = var.name
  resource_group_name        = var.resource_group_name
  location                   = var.location
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true

  tags = var.tags
}

# for_each não aceita valor sensível porque as chaves viram endereços de
# recurso no state. Os NOMES dos segredos não são sigilosos (só os valores),
# então destravamos apenas as chaves com nonsensitive() e buscamos o valor
# pelo índice — que permanece sensível.
resource "azurerm_key_vault_secret" "secrets" {
  for_each     = nonsensitive(toset(keys(var.secrets)))
  name         = each.key
  value        = var.secrets[each.key]
  key_vault_id = azurerm_key_vault.this.id
}

# A concessão de acesso à managed identity do App Service NÃO vive aqui de
# propósito: o App Service precisa da URI do vault e o vault precisaria do
# principal_id do App Service, o que forma um ciclo. A role assignment fica
# no módulo raiz, onde as duas pontas já existem.

output "vault_uri" {
  value = azurerm_key_vault.this.vault_uri
}

output "vault_id" {
  value = azurerm_key_vault.this.id
}
