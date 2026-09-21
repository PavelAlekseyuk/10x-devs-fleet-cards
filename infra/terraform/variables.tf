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

variable "resource_suffix" {
  description = "Unique lowercase alphanumeric suffix that prevents global Web App and SQL server name collisions."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{4,12}$", var.resource_suffix))
    error_message = "resource_suffix must contain 4-12 lowercase letters or digits."
  }
}

variable "sql_database_is_free" {
  description = "Set true only when the selected SQL SKU is verified as a free offer for this subscription and region."
  type        = bool
  default     = false
}

variable "enable_azure_services_sql_firewall_rule" {
  description = "Acknowledges the temporary demo limitation of allowing Azure-hosted services through Azure SQL's firewall."
  type        = bool
  default     = false
}

variable "budget_amount" {
  description = "Monthly cost budget in the subscription currency."
  type        = number

  validation {
    condition     = var.budget_amount > 0
    error_message = "budget_amount must be greater than zero."
  }
}

variable "budget_alert_email" {
  description = "Email address that receives cost-budget notifications."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.budget_alert_email))
    error_message = "budget_alert_email must be a valid email address."
  }
}

variable "budget_start_date" {
  description = "UTC first day of a month, in RFC3339 format, when the monthly budget begins."
  type        = string

  validation {
    condition     = can(regex("^\\d{4}-\\d{2}-01T00:00:00Z$", var.budget_start_date))
    error_message = "budget_start_date must be the first day of a month in UTC, for example 2026-10-01T00:00:00Z."
  }
}

variable "azuread_administrator_login" {
  description = "Display name of the Microsoft Entra user or group that will administer the Azure SQL logical server."
  type        = string
}

variable "azuread_administrator_object_id" {
  description = "Object ID of the Microsoft Entra user or group that will administer the Azure SQL logical server."
  type        = string

  validation {
    condition     = can(regex("^[0-9a-fA-F-]{36}$", var.azuread_administrator_object_id))
    error_message = "azuread_administrator_object_id must be a GUID."
  }
}