variable "name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type        = string
  description = "Static Web Apps só existe em algumas regiões (ex: eastus2)."
}

variable "sku_tier" {
  type    = string
  default = "Free"
}

variable "sku_size" {
  type    = string
  default = "Free"
}

variable "tags" {
  type    = map(string)
  default = {}
}

resource "azurerm_static_web_app" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku_tier            = var.sku_tier
  sku_size            = var.sku_size

  tags = var.tags
}

output "api_key" {
  value     = azurerm_static_web_app.this.api_key
  sensitive = true
}

output "default_host_name" {
  value = azurerm_static_web_app.this.default_host_name
}
