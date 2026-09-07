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
    key                  = "dev.terraform.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      # A mudança de região recria o vault. Sem purge, o soft delete mantém o
      # nome reservado por 90 dias e a recriação falha. E recover fica false
      # de propósito: recuperar o vault antigo o traria de volta na região
      # original (brazilsouth), silenciosamente errada.
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = false
    }
  }
}

data "azurerm_client_config" "current" {}

locals {
  # East US 2 e não brazilsouth: a subscription não tem cota de VMs F1
  # liberada na região do Brasil. Produção segue em brazilsouth.
  location = "eastus2"

  # Static Web Apps só existe em algumas regiões. Coincide com location hoje,
  # mas fica separado para que mudar a região do App Service não quebre a SWA.
  swa_location = "eastus2"

  prefix = "stoctable"
  tags = {
    environment = "development"
    project     = "stoctable"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "Stoctable-Dev"
  location = local.location
  tags     = local.tags
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

# O banco fica no Neon — nenhum azurerm_postgresql_* é criado neste ambiente.
# As connection strings entram no Key Vault via var.branch_connection_strings.
module "key_vault" {
  depends_on = [azurerm_role_assignment.operator_kv_secrets]

  source              = "../../modules/key_vault"
  name                = "${local.prefix}-kv-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  tags                = local.tags

  secrets = merge(
    {
      # O provider de configuração do Key Vault converte '--' em ':', então
      # este segredo chega na aplicação como a chave Jwt:Secret, que é o que
      # Program.cs lê. Um nome sem '--' nunca seria encontrado.
      "Jwt--Secret" = var.jwt_secret

      # Usada nos caminhos em que o tenant ainda não foi resolvido:
      # /api/auth, o DbSeeder no startup e o design-time factory.
      "DefaultBranchConnectionString" = var.default_branch_connection_string
    },
    {
      # TenantResolutionMiddleware busca STOCTABLE-CONN-{ID} em maiúsculas.
      for branch_id, conn in var.branch_connection_strings :
      "STOCTABLE-CONN-${upper(branch_id)}" => conn
    }
  )
}

module "app_service" {
  source                 = "../../modules/app_service"
  name                   = "${local.prefix}-api-dev"
  resource_group_name    = azurerm_resource_group.main.name
  location               = local.location
  sku_name               = "F1"
  always_on              = false
  aspnetcore_environment = "Development"
  key_vault_url          = module.key_vault.vault_uri
  tags                   = local.tags

  app_settings = {
    "Cors__AllowedOrigins" = "https://${module.static_web_app.default_host_name}"
  }
}

# Fora dos módulos de propósito: é o que quebra o ciclo app_service <-> key_vault.
resource "azurerm_role_assignment" "app_kv_secrets" {
  scope                = module.key_vault.vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.app_service.principal_id
}


module "static_web_app" {
  source              = "../../modules/static_web_app"
  name                = "${local.prefix}-web-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.swa_location
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = local.tags
}

output "api_url" {
  value = "https://${module.app_service.default_hostname}"
}

output "web_url" {
  value = "https://${module.static_web_app.default_host_name}"
}

output "kv_uri" {
  value = module.key_vault.vault_uri
}
