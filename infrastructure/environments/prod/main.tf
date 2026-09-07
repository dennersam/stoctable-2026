terraform {
  required_version = ">= 1.6"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "stoctable-tfstate"
    storage_account_name = "stoctabletfstate"
    container_name       = "tfstate"
    key                  = "prod.terraform.tfstate"
  }
}

provider "azurerm" {
  features {}
}

data "azurerm_client_config" "current" {}

locals {
  location   = "brazilsouth"
  prefix     = "stoctable"
  branch_ids = ["001", "002"] # adicione IDs de filiais conforme necessário
  tags = {
    environment = "production"
    project     = "stoctable"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "Stoctable-Prod"
  location = local.location
  tags     = local.tags
}

module "postgresql" {
  source              = "../../modules/postgresql"
  name                = "${local.prefix}-psql"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  admin_username      = var.db_admin_username
  admin_password      = var.db_admin_password
  branch_databases    = local.branch_ids
  tags                = local.tags
}

# O vault usa RBAC, então quem roda o Terraform também precisa de permissão de
# PLANO DE DADOS para gravar os segredos — sem isso o apply falha com 403 em
# azurerm_key_vault_secret. Ser Owner da subscription não basta: Owner é plano
# de controle. O escopo é o resource group, e não o vault, de propósito: assim
# o módulo key_vault pode declarar depends_on sem formar ciclo.
resource "azurerm_role_assignment" "operator_kv_secrets" {
  scope                = azurerm_resource_group.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

module "key_vault" {
  depends_on = [azurerm_role_assignment.operator_kv_secrets]

  source              = "../../modules/key_vault"
  name                = "${local.prefix}-kv"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  tags                = local.tags

  secrets = merge(
    {
      # O provider de configuração do Key Vault converte '--' em ':', então
      # este segredo chega na aplicação como Jwt:Secret — que é o que
      # Program.cs lê. Com o nome antigo (STOCTABLE-JWT-SECRET) a chave nunca
      # era encontrada e a aplicação falhava no startup.
      "Jwt--Secret" = var.jwt_secret
    },
    {
      for branch_id in local.branch_ids :
      "STOCTABLE-CONN-${upper(branch_id)}" => "Host=${module.postgresql.server_fqdn};Database=stoctable_branch_${branch_id};Username=${var.db_admin_username};Password=${var.db_admin_password};SSL Mode=VerifyFull;"
    }
  )
}

module "app_service" {
  source                 = "../../modules/app_service"
  name                   = "${local.prefix}-api"
  resource_group_name    = azurerm_resource_group.main.name
  location               = local.location
  sku_name               = "P1v3"
  always_on              = true
  aspnetcore_environment = "Production"
  key_vault_url          = module.key_vault.vault_uri
  tags                   = local.tags
}

# Fora dos módulos de propósito: é o que quebra o ciclo app_service <-> key_vault.
resource "azurerm_role_assignment" "app_kv_secrets" {
  scope                = module.key_vault.vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.app_service.principal_id
}

module "static_web_app" {
  source              = "../../modules/static_web_app"
  name                = "${local.prefix}-web"
  resource_group_name = azurerm_resource_group.main.name
  location            = "eastus2" # Static Web Apps não existe em brazilsouth
  sku_tier            = "Standard"
  sku_size            = "Standard"
  tags                = local.tags
}

variable "db_admin_username" {
  type      = string
  sensitive = true
}

variable "db_admin_password" {
  type      = string
  sensitive = true
}

variable "jwt_secret" {
  type      = string
  sensitive = true
}

output "api_url" {
  value = "https://${module.app_service.default_hostname}"
}

output "web_url" {
  value = "https://${module.static_web_app.default_host_name}"
}

output "db_fqdn" {
  value = module.postgresql.server_fqdn
}

output "kv_uri" {
  value = module.key_vault.vault_uri
}
