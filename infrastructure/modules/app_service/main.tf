variable "name"                { type = string }
variable "resource_group_name"  { type = string }
variable "location"             { type = string }
variable "sku_name"             { type = string  default = "P1v3" }
variable "key_vault_url"        { type = string }
variable "app_settings"         { type = map(string) default = {} }

resource "azurerm_service_plan" "this" {
  name                = "${var.name}-plan"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.sku_name
}

resource "azurerm_linux_web_app" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id

  identity {
    type = "SystemAssigned"
  }

  app_settings = merge(
    {
      "KeyVault__Url"                   = var.key_vault_url
      "ASPNETCORE_ENVIRONMENT"          = "Production"
      "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    },
    var.app_settings
  )

  site_config {
    application_stack {
      dotnet_version = "10.0"
    }
    always_on = true
  }

  https_only = true

  tags = { environment = "production" }
}

output "app_service_name"       { value = azurerm_linux_web_app.this.name }
output "principal_id"           { value = azurerm_linux_web_app.this.identity[0].principal_id }
output "default_hostname"       { value = azurerm_linux_web_app.this.default_hostname }
