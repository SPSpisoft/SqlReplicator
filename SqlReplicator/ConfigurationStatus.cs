using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlReplicator
{
    public class ConfigurationStatus
    {
        public string BaseConnectionString { get; set; } = string.Empty;
        public string SourceConnectionString { get; set; } = string.Empty;
        public string TargetConnectionString { get; set; } = string.Empty;
        public List<FieldMapping> FieldMappings { get; set; } = new List<FieldMapping>();
        public List<ListenerConfiguration> SelectedListeners { get; set; } = new List<ListenerConfiguration>();
        public bool IsServiceInstalled { get; set; }
        public bool IsServiceRunning { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public int TotalMappedTables { get; set; }
        public int TotalMappedFields { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();

        public async Task<ConfigurationStatus> LoadFromDatabase(string baseConnectionString)
        {
            var status = new ConfigurationStatus
            {
                BaseConnectionString = baseConnectionString
            };

            try
            {
                using (var connection = new SqlConnection(baseConnectionString))
                {
                    await connection.OpenAsync();

                    // Load replication config
                    await LoadReplicationConfig(connection, status);

                    // Load field mappings
                    await LoadFieldMappings(connection, status);

                    // Load listener configurations
                    await LoadListenerConfigurations(connection, status);

                    // Check service status
                    await CheckServiceStatus(status);

                    // Validate configuration
                    await ValidateConfiguration(status);
                }
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Error loading configuration: {ex.Message}");
            }

            return status;
        }

        private async Task LoadReplicationConfig(SqlConnection connection, ConfigurationStatus status)
        {
            try
            {
                var command = new SqlCommand(
                    @"SELECT TOP 1 SourceConnectionString, TargetConnectionString, ListenerType, CreatedAt 
                      FROM ReplicationConfig 
                      ORDER BY Id DESC", connection);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        status.SourceConnectionString = reader.GetString(0);
                        status.TargetConnectionString = reader.GetString(1);
                        
                        if (!reader.IsDBNull(2))
                        {
                            var listenerType = reader.GetString(2);
                            // Parse listener types and add to selected listeners
                            if (!string.IsNullOrEmpty(listenerType))
                            {
                                var listenerTypeNames = listenerType.Split(',');
                                var availableListeners = ListenerConfiguration.GetAvailableListeners();
                                
                                foreach (var typeName in listenerTypeNames)
                                {
                                    if (Enum.TryParse<ListenerType>(typeName.Trim(), out var parsedType))
                                    {
                                        var listener = availableListeners.FirstOrDefault(l => l.Type == parsedType);
                                        if (listener != null)
                                        {
                                            listener.IsEnabled = true;
                                            status.SelectedListeners.Add(listener);
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (!reader.IsDBNull(3))
                        {
                            status.LastSyncTime = reader.GetDateTime(3);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Error loading replication config: {ex.Message}");
            }
        }

        private async Task LoadFieldMappings(SqlConnection connection, ConfigurationStatus status)
        {
            try
            {
                var command = new SqlCommand(
                    @"SELECT TargetTableName, SourceTableName, SourceField, TargetField, IsPrimaryKey 
                      FROM FieldMappings 
                      ORDER BY TargetTableName, TargetField", connection);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var mapping = new FieldMapping
                        {
                            TargetTableName = reader.GetString(0),
                            SourceTableName = reader.GetString(1),
                            SourceField = reader.GetString(2),
                            TargetField = reader.GetString(3),
                            IsPrimaryKey = reader.GetBoolean(4)
                        };
                        status.FieldMappings.Add(mapping);
                    }
                }

                status.TotalMappedTables = status.FieldMappings.Select(f => f.TargetTableName).Distinct().Count();
                status.TotalMappedFields = status.FieldMappings.Count;
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Error loading field mappings: {ex.Message}");
            }
        }

        private async Task LoadListenerConfigurations(SqlConnection connection, ConfigurationStatus status)
        {
            try
            {
                // If no listeners were loaded from ReplicationConfig, use default
                if (status.SelectedListeners.Count == 0)
                {
                    var availableListeners = ListenerConfiguration.GetAvailableListeners();
                    status.SelectedListeners = availableListeners.Where(l => l.IsEnabled).ToList();
                }
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Error loading listener configurations: {ex.Message}");
            }
        }

        private async Task CheckServiceStatus(ConfigurationStatus status)
        {
            try
            {
                // Check if service is installed and running
                var serviceName = "SpsReplicationService";
                var services = System.ServiceProcess.ServiceController.GetServices();
                var service = services.FirstOrDefault(s => s.ServiceName == serviceName);

                if (service != null)
                {
                    status.IsServiceInstalled = true;
                    status.IsServiceRunning = service.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                }
                else
                {
                    status.IsServiceInstalled = false;
                    status.IsServiceRunning = false;
                }
            }
            catch (Exception ex)
            {
                status.Errors.Add($"Error checking service status: {ex.Message}");
            }
        }

        private async Task ValidateConfiguration(ConfigurationStatus status)
        {
            // Check for missing configurations
            if (string.IsNullOrEmpty(status.SourceConnectionString))
            {
                status.Warnings.Add("⚠️ Source database connection is not configured.");
            }

            if (string.IsNullOrEmpty(status.TargetConnectionString))
            {
                status.Warnings.Add("⚠️ Target database connection is not configured.");
            }

            if (status.FieldMappings.Count == 0)
            {
                status.Warnings.Add("⚠️ No field mappings are configured.");
            }

            // Check listener compatibility
            var compatibilityWarnings = ListenerCompatibilityChecker.CheckCompatibility(status.SelectedListeners);
            status.Warnings.AddRange(compatibilityWarnings);

            // Check for tables without primary keys
            var tablesWithoutPrimaryKeys = status.FieldMappings
                .GroupBy(f => f.TargetTableName)
                .Where(g => !g.Any(f => f.IsPrimaryKey))
                .Select(g => g.Key)
                .ToList();

            foreach (var table in tablesWithoutPrimaryKeys)
            {
                status.Warnings.Add($"⚠️ Table '{table}' has no primary key configured.");
            }
        }

        public string GetStatusSummary()
        {
            var summary = new List<string>();

            // Service Status
            if (IsServiceInstalled)
            {
                summary.Add($"🟢 Service Status: {(IsServiceRunning ? "Running" : "Stopped")}");
            }
            else
            {
                summary.Add("🔴 Service Status: Not Installed");
            }

            // Configuration Status
            if (!string.IsNullOrEmpty(SourceConnectionString) && !string.IsNullOrEmpty(TargetConnectionString))
            {
                summary.Add("🟢 Database Connections: Configured");
            }
            else
            {
                summary.Add("🔴 Database Connections: Not Configured");
            }

            // Field Mappings
            if (TotalMappedTables > 0)
            {
                summary.Add($"🟢 Field Mappings: {TotalMappedTables} tables, {TotalMappedFields} fields");
            }
            else
            {
                summary.Add("🔴 Field Mappings: None configured");
            }

            // Listeners
            if (SelectedListeners.Count > 0)
            {
                var listenerNames = string.Join(", ", SelectedListeners.Select(l => l.Name));
                summary.Add($"🟢 Active Listeners: {listenerNames}");
            }
            else
            {
                summary.Add("🔴 Active Listeners: None selected");
            }

            // Last Sync
            if (LastSyncTime.HasValue)
            {
                summary.Add($"🟢 Last Sync: {LastSyncTime.Value:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                summary.Add("🟡 Last Sync: Never");
            }

            return string.Join("\n", summary);
        }

        public string GetDetailedReport()
        {
            var report = new List<string>
            {
                "=== CONFIGURATION STATUS REPORT ===",
                "",
                GetStatusSummary(),
                "",
                "=== DETAILED INFORMATION ===",
                ""
            };

            // Database Connections
            report.Add("📊 DATABASE CONNECTIONS:");
            if (!string.IsNullOrEmpty(SourceConnectionString))
            {
                var sourceServer = ExtractServerName(SourceConnectionString);
                report.Add($"  Source: {sourceServer}");
            }
            if (!string.IsNullOrEmpty(TargetConnectionString))
            {
                var targetServer = ExtractServerName(TargetConnectionString);
                report.Add($"  Target: {targetServer}");
            }
            report.Add("");

            // Field Mappings
            report.Add("📋 FIELD MAPPINGS:");
            var tableGroups = FieldMappings.GroupBy(f => f.TargetTableName);
            foreach (var group in tableGroups)
            {
                report.Add($"  Table: {group.Key}");
                foreach (var mapping in group)
                {
                    var pkIndicator = mapping.IsPrimaryKey ? " (PK)" : "";
                    report.Add($"    {mapping.SourceField} → {mapping.TargetField}{pkIndicator}");
                }
                report.Add("");
            }

            // Listeners
            report.Add("🎧 ACTIVE LISTENERS:");
            foreach (var listener in SelectedListeners.OrderBy(l => l.Priority))
            {
                report.Add($"  {listener.Name} (Priority: {listener.Priority})");
                report.Add($"    Performance Impact: {listener.PerformanceImpact}");
                report.Add($"    Reliability: {listener.ReliabilityLevel}");
                if (listener.Interval.HasValue)
                {
                    report.Add($"    Interval: {listener.Interval.Value.TotalMinutes} minutes");
                }
                report.Add("");
            }

            // Warnings and Errors
            if (Warnings.Count > 0)
            {
                report.Add("⚠️ WARNINGS:");
                foreach (var warning in Warnings)
                {
                    report.Add($"  {warning}");
                }
                report.Add("");
            }

            if (Errors.Count > 0)
            {
                report.Add("❌ ERRORS:");
                foreach (var error in Errors)
                {
                    report.Add($"  {error}");
                }
                report.Add("");
            }

            return string.Join("\n", report);
        }

        private string ExtractServerName(string connectionString)
        {
            try
            {
                var parts = connectionString.Split(';');
                var serverPart = parts.FirstOrDefault(p => p.StartsWith("Server="));
                if (!string.IsNullOrEmpty(serverPart))
                {
                    return serverPart.Replace("Server=", "");
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return "Unknown";
        }
    }
} 