variable "name"                { type = string }
variable "resource_group_name"  { type = string }
variable "location"             { type = string }
variable "tenant_id"            { type = string }
variable "app_service_principal_id" { type = string }
variable "secrets"              { type = map(string) sensitive = true default = {} }

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  tenant_id           = var.tenant_id
  sku_name            = "standard"
  enable_rbac_authorization = true

  tags = { environment = "production" }
}

# Grant App Service managed identity access to read secrets
resource "azurerm_role_assignment" "app_service_kv_reader" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.app_service_principal_id
}

resource "azurerm_key_vault_secret" "secrets" {
  for_each     = var.secrets
  name         = each.key
  value        = each.value
  key_vault_id = azurerm_key_vault.this.id
}

output "vault_uri" { value = azurerm_key_vault.this.vault_uri }
output "vault_id"  { value = azurerm_key_vault.this.id }
