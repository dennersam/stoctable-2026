terraform {
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

locals {
  location = "brazilsouth"
  prefix   = "stoctable"
  branch_ids = ["001", "002"]  # adicione IDs de filiais conforme necessário
}

resource "azurerm_resource_group" "main" {
  name     = "Stoctable-Prod"
  location = local.location
  tags     = { environment = "production", project = "stoctable" }
}

module "app_service" {
  source              = "../../modules/app_service"
  name                = "${local.prefix}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  key_vault_url       = module.key_vault.vault_uri
}

module "static_web_app" {
  source              = "../../modules/static_web_app"
  name                = "${local.prefix}-web"
  resource_group_name = azurerm_resource_group.main.name
  location            = "eastus2"  # Static Web Apps availability
}

module "postgresql" {
  source              = "../../modules/postgresql"
  name                = "${local.prefix}-psql"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  admin_username      = var.db_admin_username
  admin_password      = var.db_admin_password
  branch_databases    = local.branch_ids
}

module "key_vault" {
  source                       = "../../modules/key_vault"
  name                         = "${local.prefix}-kv"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = local.location
  tenant_id                    = data.azurerm_client_config.current.tenant_id
  app_service_principal_id     = module.app_service.principal_id
  secrets = {
    "STOCTABLE-JWT-SECRET" = var.jwt_secret
    for branch_id in local.branch_ids :
      "STOCTABLE-CONN-${branch_id}" => "Host=${module.postgresql.server_fqdn};Database=stoctable_branch_${branch_id};Username=${var.db_admin_username};Password=${var.db_admin_password};SSL Mode=VerifyFull;"
  }
}

data "azurerm_client_config" "current" {}

variable "db_admin_username" { type = string  sensitive = true }
variable "db_admin_password" { type = string  sensitive = true }
variable "jwt_secret"        { type = string  sensitive = true }

output "api_url"     { value = "https://${module.app_service.default_hostname}" }
output "web_url"     { value = "https://${module.static_web_app.default_host_name}" }
output "db_fqdn"     { value = module.postgresql.server_fqdn }
output "kv_uri"      { value = module.key_vault.vault_uri }
