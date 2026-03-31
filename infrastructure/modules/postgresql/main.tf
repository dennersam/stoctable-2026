variable "name"                { type = string }
variable "resource_group_name"  { type = string }
variable "location"             { type = string }
variable "admin_username"       { type = string }
variable "admin_password"       { type = string  sensitive = true }
variable "sku_name"             { type = string  default = "B_Standard_B1ms" }
variable "branch_databases"     { type = list(string) }

resource "azurerm_postgresql_flexible_server" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  version             = "16"
  administrator_login = var.admin_username
  administrator_password = var.admin_password
  sku_name            = var.sku_name
  storage_mb          = 32768
  backup_retention_days = 7

  tags = { environment = "production" }
}

resource "azurerm_postgresql_flexible_server_database" "branch" {
  for_each  = toset(var.branch_databases)
  name      = "stoctable_branch_${each.key}"
  server_id = azurerm_postgresql_flexible_server.this.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name      = "allow-azure-services"
  server_id = azurerm_postgresql_flexible_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

output "server_fqdn" { value = azurerm_postgresql_flexible_server.this.fqdn }
output "server_id"   { value = azurerm_postgresql_flexible_server.this.id }
