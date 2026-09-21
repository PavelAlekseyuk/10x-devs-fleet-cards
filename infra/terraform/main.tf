data "azurerm_client_config" "current" {}

locals {
  web_app_name    = lower("${var.project_prefix}-${var.resource_suffix}-web")
  sql_server_name = lower("${replace(var.project_prefix, "-", "")}${var.resource_suffix}sql")
  common_tags = {
    application = "FleetCards"
    environment = "demo"
    managed-by  = "terraform"
  }
}

resource "terraform_data" "deployment_guardrails" {
  input = {
    sql_database_sku_name                   = var.sql_database_sku_name
    sql_database_is_free                    = var.sql_database_is_free
    allow_paid_resources                    = var.allow_paid_resources
    enable_azure_services_sql_firewall_rule = var.enable_azure_services_sql_firewall_rule
  }

  lifecycle {
    precondition {
      condition     = var.sql_database_sku_name != null
      error_message = "Select and record an exact Azure SQL SKU before applying Terraform."
    }

    precondition {
      condition     = var.sql_database_is_free || var.allow_paid_resources
      error_message = "Set sql_database_is_free=true after verified free-tier availability, or explicitly set allow_paid_resources=true for a paid SKU."
    }

    precondition {
      condition     = var.enable_azure_services_sql_firewall_rule
      error_message = "Set enable_azure_services_sql_firewall_rule=true only after accepting the documented public-demo Azure SQL firewall limitation."
    }
  }
}

resource "azurerm_service_plan" "demo" {
  name                = "${var.project_prefix}-${var.resource_suffix}-plan"
  resource_group_name = data.azurerm_resource_group.demo.name
  location            = data.azurerm_resource_group.demo.location
  os_type             = "Windows"
  sku_name            = var.app_service_sku
  tags                = local.common_tags
}

resource "azurerm_windows_web_app" "demo" {
  name                = local.web_app_name
  resource_group_name = data.azurerm_resource_group.demo.name
  location            = data.azurerm_resource_group.demo.location
  service_plan_id     = azurerm_service_plan.demo.id
  https_only          = true
  tags                = local.common_tags

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT          = "Production"
    WEBSITE_RUN_FROM_PACKAGE        = "1"
    "ConnectionStrings__fleetcards" = "Server=tcp:${azurerm_mssql_server.demo.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.demo.name};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }

  site_config {
    ftps_state          = "Disabled"
    minimum_tls_version = "1.2"
    health_check_path   = var.app_service_sku == "F1" ? null : "/health"

    application_stack {
      dotnet_version = "v10.0"
    }
  }

  depends_on = [terraform_data.deployment_guardrails]
}

resource "azurerm_mssql_server" "demo" {
  name                          = local.sql_server_name
  resource_group_name           = data.azurerm_resource_group.demo.name
  location                      = data.azurerm_resource_group.demo.location
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true
  tags                          = local.common_tags

  azuread_administrator {
    azuread_authentication_only = true
    login_username              = var.azuread_administrator_login
    object_id                   = var.azuread_administrator_object_id
    tenant_id                   = data.azurerm_client_config.current.tenant_id
  }

  depends_on = [terraform_data.deployment_guardrails]
}

resource "azurerm_mssql_database" "demo" {
  name      = "fleetcards"
  server_id = azurerm_mssql_server.demo.id
  sku_name  = var.sql_database_sku_name
  tags      = local.common_tags
}

resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServicesForDemo"
  server_id        = azurerm_mssql_server.demo.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_consumption_budget_resource_group" "demo" {
  name              = "${var.project_prefix}-demo-budget"
  resource_group_id = data.azurerm_resource_group.demo.id
  amount            = var.budget_amount
  time_grain        = "Monthly"

  time_period {
    start_date = var.budget_start_date
  }

  notification {
    enabled        = true
    threshold      = 80
    operator       = "GreaterThan"
    contact_emails = [var.budget_alert_email]
  }

  notification {
    enabled        = true
    threshold      = 100
    operator       = "GreaterThan"
    contact_emails = [var.budget_alert_email]
  }
}