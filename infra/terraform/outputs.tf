output "app_service_name" {
  description = "Name used by the deployment workflow for the Azure Web App."
  value       = azurerm_windows_web_app.demo.name
}

output "app_url" {
  description = "Public HTTPS URL of the demo application."
  value       = "https://${azurerm_windows_web_app.demo.default_hostname}"
}

output "resource_group_name" {
  description = "Dedicated resource group containing the demo application resources."
  value       = data.azurerm_resource_group.demo.name
}

output "sql_server_name" {
  description = "Logical Azure SQL server name used by the deployment migration job."
  value       = azurerm_mssql_server.demo.name
}

output "sql_server_identity_principal_id" {
  description = "Managed identity principal ID that an owner must grant Directory Readers before automated database-user provisioning."
  value       = azurerm_mssql_server.demo.identity[0].principal_id
}

output "sql_database_name" {
  description = "Azure SQL database name used by the application and migration job."
  value       = azurerm_mssql_database.demo.name
}