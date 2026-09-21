variable "resource_group_name" {
  description = "Dedicated resource group created by scripts/bootstrap-azure.ps1."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9-]{3,64}$", var.resource_group_name))
    error_message = "resource_group_name must contain 3-64 lowercase letters, digits, or hyphens."
  }
}

variable "location" {
  description = "Azure region selected by the recorded preflight comparison."
  type        = string

  validation {
    condition     = contains(["westeurope", "polandcentral"], lower(var.location))
    error_message = "location must be westeurope or polandcentral."
  }
}

variable "project_prefix" {
  description = "Short lowercase name used to construct globally unique Azure resource names."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9-]{3,20}$", var.project_prefix))
    error_message = "project_prefix must contain 3-20 lowercase letters, digits, or hyphens."
  }
}

variable "app_service_sku" {
  description = "Explicit App Service Plan SKU selected during preflight. F1 is permitted for the public demo only."
  type        = string
  default     = "F1"
}

variable "sql_database_sku_name" {
  description = "Explicit Azure SQL SKU selected during preflight. Never infer a paid SKU."
  type        = string
  nullable    = true
  default     = null
}

variable "allow_paid_resources" {
  description = "Must be true before the infrastructure can use a selected paid Azure SQL SKU."
  type        = bool
  default     = false
}