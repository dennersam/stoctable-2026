variable "name" {
  type        = string
  description = "Nome do App Service (deve ser único globalmente)."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "sku_name" {
  type        = string
  description = "SKU do App Service Plan. F1 é o plano gratuito."
  default     = "F1"
}

variable "always_on" {
  type        = bool
  description = "Precisa ser false no plano F1 — Always On não é suportado lá e o apply falha."
  default     = false
}

variable "aspnetcore_environment" {
  type    = string
  default = "Production"
}

variable "key_vault_url" {
  type        = string
  description = "URI do Key Vault. Vazio desativa o SecretClient na aplicação."
  default     = ""
}

variable "app_settings" {
  type    = map(string)
  default = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}

resource "azurerm_service_plan" "this" {
  name                = "${var.name}-plan"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.sku_name

  tags = var.tags
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
      "KeyVault__Url"                       = var.key_vault_url
      "ASPNETCORE_ENVIRONMENT"              = var.aspnetcore_environment
      "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"

      # O App Service termina o TLS no front end e repassa a requisição em HTTP,
      # sinalizando o protocolo original em X-Forwarded-Proto. Sem isto o
      # Request.IsHttps é falso, o UseHttpsRedirection do Program.cs devolve 307
      # em toda requisição e o preflight de CORS morre no redirect — aparecendo
      # no navegador como "No 'Access-Control-Allow-Origin' header".
      "ASPNETCORE_FORWARDEDHEADERS_ENABLED" = "true"
    },
    var.app_settings
  )

  site_config {
    application_stack {
      dotnet_version = "10.0"
    }
    always_on = var.always_on
  }

  https_only = true

  tags = var.tags
}

output "app_service_name" {
  value = azurerm_linux_web_app.this.name
}

output "principal_id" {
  value = azurerm_linux_web_app.this.identity[0].principal_id
}

output "default_hostname" {
  value = azurerm_linux_web_app.this.default_hostname
}
