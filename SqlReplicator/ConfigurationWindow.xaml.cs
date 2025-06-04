using MahApps.Metro.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

namespace SqlReplicator
{
    public partial class ConfigWindow : MetroWindow
    {
        private readonly string baseConnectionString;
        private readonly string sourceConnectionString;
        private readonly string targetConnectionString;

        private ObservableCollection<TableInfo> sourceTables = new ObservableCollection<TableInfo>();
        private ObservableCollection<TableInfo> targetTables = new ObservableCollection<TableInfo>();
        private ObservableCollection<FieldMapping> fieldMappings = new ObservableCollection<FieldMapping>();

        private bool isDatabasesSimilar = false;

        public ConfigWindow(string baseConn, string sourceConn, string targetConn)
        {
            InitializeComponent();
            baseConnectionString = baseConn;
            sourceConnectionString = sourceConn;
            targetConnectionString = targetConn;

            MappingGrid.ItemsSource = fieldMappings;
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            AnalyzeButton.IsEnabled = false;
            AnalyzeButton.Content = "Analyzing...";
            AnalysisStatus.Text = "Analyzing database structures...";

            try
            {
                // Load source tables
                sourceTables = await LoadDatabaseStructure(sourceConnectionString);
                SourceTreeView.ItemsSource = sourceTables;

                // Load target tables
                targetTables = await LoadDatabaseStructure(targetConnectionString);
                TargetTreeView.ItemsSource = targetTables;

                // Analyze similarity
                bool similar = AnalyzeSimilarity();

                SimilarityPanel.Visibility = Visibility.Visible;

                if (similar)
                {
                    SimilarRadio.IsChecked = true;
                    AnalysisStatus.Text = "Analysis complete - Databases appear to have similar structure";
                }
                else
                {
                    DifferentRadio.IsChecked = true;
                    AnalysisStatus.Text = "Analysis complete - Databases have different structures";
                }

                ConfigPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error analyzing databases: {ex.Message}", "Analysis Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                AnalysisStatus.Text = "Analysis failed";
            }
            finally
            {
                AnalyzeButton.IsEnabled = true;
                AnalyzeButton.Content = "Re-analyze";
            }
        }

        private async Task<ObservableCollection<TableInfo>> LoadDatabaseStructure(string connectionString)
        {
            var tables = new ObservableCollection<TableInfo>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get tables
                var tablesQuery = @"
                    SELECT TABLE_NAME 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE' 
                    ORDER BY TABLE_NAME";

                using (var cmd = new SqlCommand(tablesQuery, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var tableName = reader.GetString(0);
                        var table = new TableInfo { Name = tableName };
                        tables.Add(table);
                    }
                }

                // Get columns for each table
                foreach (var table in tables)
                {
                    var columnsQuery = @"
                        SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = @TableName
                        ORDER BY ORDINAL_POSITION";

                    using (var cmd = new SqlCommand(columnsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@TableName", table.Name);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var column = new ColumnInfo
                                {
                                    Name = reader.GetString(0),
                                    DataType = reader.GetString(1),
                                    IsNullable = reader.GetString(2) == "YES",
                                    DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    IsSelected = true,
                                    IsEnabled = true
                                };
                                table.Columns.Add(column);
                            }
                        }
                    }
                }
            }

            return tables;
        }

        private bool AnalyzeSimilarity()
        {
            // Simple similarity check - compare table names and column structures
            var sourceTableNames = sourceTables.Select(t => t.Name).ToHashSet();
            var targetTableNames = targetTables.Select(t => t.Name).ToHashSet();

            // Check if at least 70% of tables match
            var commonTables = sourceTableNames.Intersect(targetTableNames).Count();
            var totalTables = Math.Max(sourceTableNames.Count, targetTableNames.Count);

            if (totalTables == 0) return false;

            var similarityRatio = (double)commonTables / totalTables;
            return similarityRatio >= 0.7;
        }

        private void SimilarityChanged(object sender, RoutedEventArgs e)
        {
            isDatabasesSimilar = SimilarRadio.IsChecked == true;

            if (isDatabasesSimilar)
            {
                // Similar databases mode
                TargetTreeView.Visibility = Visibility.Visible;
                MappingPanel.Visibility = Visibility.Collapsed;

                // Sync selections
                SyncTargetWithSource();
            }
            else
            {
                // Different databases mode
                TargetTreeView.Visibility = Visibility.Collapsed;
                MappingPanel.Visibility = Visibility.Visible;

                // Create field mappings
                CreateFieldMappings();
            }

            UpdateSummary();
        }

        private void SyncTargetWithSource()
        {
            foreach (var sourceTable in sourceTables)
            {
                var targetTable = targetTables.FirstOrDefault(t => t.Name == sourceTable.Name);
                if (targetTable != null)
                {
                    targetTable.IsSelected = sourceTable.IsSelected;

                    foreach (var sourceColumn in sourceTable.Columns)
                    {
                        var targetColumn = targetTable.Columns.FirstOrDefault(c => c.Name == sourceColumn.Name);
                        if (targetColumn != null)
                        {
                            targetColumn.IsSelected = sourceColumn.IsSelected;
                        }
                    }
                }
            }
        }

        private void CreateFieldMappings()
        {
            fieldMappings.Clear();

            foreach (var targetTable in targetTables)
            {
                foreach (var targetColumn in targetTable.Columns)
                {
                    var mapping = new FieldMapping
                    {
                        TargetTable = targetTable.Name,
                        TargetField = targetColumn.Name,
                        TargetDataType = targetColumn.DataType,
                        SourceTables = sourceTables.ToList(),
                        UseCustomQuery = false
                    };

                    // Try to find matching source field
                    var sourceTable = sourceTables.FirstOrDefault(t => t.Name == targetTable.Name);
                    if (sourceTable != null)
                    {
                        var sourceColumn = sourceTable.Columns.FirstOrDefault(c => c.Name == targetColumn.Name);
                        if (sourceColumn != null)
                        {
                            mapping.SourceTable = sourceTable.Name;
                            mapping.SourceField = sourceColumn.Name;
                        }
                    }

                    fieldMappings.Add(mapping);
                }
            }
        }

        private void SourceTable_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var table = ((FrameworkElement)checkBox.Parent).DataContext as TableInfo;

            if (table != null)
            {
                // Enable/disable all columns
                foreach (var column in table.Columns)
                {
                    column.IsEnabled = table.IsSelected;
                    if (!table.IsSelected)
                        column.IsSelected = false;
                }

                if (isDatabasesSimilar)
                    SyncTargetWithSource();

                UpdateSummary();
            }
        }

        private void SourceTable_Unchecked(object sender, RoutedEventArgs e)
        {
            SourceTable_Checked(sender, e);
        }

        private void EditQuery_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var mapping = button.Tag as FieldMapping;

            if (mapping != null)
            {
                var queryWindow = new QueryEditorWindow(mapping.CustomQuery ?? "");
                if (queryWindow.ShowDialog() == true)
                {
                    mapping.CustomQuery = queryWindow.Query;
                    UpdateSummary();
                }
            }
        }

        private void UpdateSummary()
        {
            var summary = "Configuration Summary:\n\n";

            if (isDatabasesSimilar)
            {
                var selectedTables = sourceTables.Where(t => t.IsSelected).ToList();
                summary += $"Database Mode: Similar Structures\n";
                summary += $"Tables to replicate: {selectedTables.Count}\n\n";

                foreach (var table in selectedTables)
                {
                    var selectedColumns = table.Columns.Where(c => c.IsSelected).ToList();
                    summary += $"• {table.Name} ({selectedColumns.Count} columns)\n";
                }
            }
            else
            {
                var mappedFields = fieldMappings.Where(m => !string.IsNullOrEmpty(m.SourceTable) || m.UseCustomQuery).ToList();
                summary += $"Database Mode: Different Structures\n";
                summary += $"Field mappings configured: {mappedFields.Count}\n";
                summary += $"Custom queries: {mappedFields.Count(m => m.UseCustomQuery)}\n\n";

                var tableGroups = mappedFields.GroupBy(m => m.TargetTable);
                foreach (var group in tableGroups)
                {
                    summary += $"• {group.Key} ({group.Count()} mappings)\n";
                }
            }

            SummaryText.Text = summary;
            SummaryPanel.Visibility = Visibility.Visible;
            SaveConfigButton.IsEnabled = true;
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new ReplicationConfig
                {
                    IsSimilarDatabase = isDatabasesSimilar,
                    SourceTables = sourceTables.Where(t => t.IsSelected).ToList(),
                    FieldMappings = fieldMappings.Where(m => !string.IsNullOrEmpty(m.SourceTable) || m.UseCustomQuery).ToList(),
                    CreatedDate = DateTime.Now
                };

                // Save configuration to base database
                SaveConfigurationToDatabase(config);

                MessageBox.Show("Configuration saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveConfigurationToDatabase(ReplicationConfig config)
        {
            using (var connection = new SqlConnection(baseConnectionString))
            {
                await connection.OpenAsync();

                // Create configuration tables if they don't exist
                await CreateConfigurationTables(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Save main configuration
                        var configId = await SaveMainConfig(connection, transaction, config);

                        if (config.IsSimilarDatabase)
                        {
                            // Save table selections
                            await SaveTableSelections(connection, transaction, configId, config.SourceTables);
                        }
                        else
                        {
                            // Save field mappings
                            await SaveFieldMappings(connection, transaction, configId, config.FieldMappings);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private async Task CreateConfigurationTables(SqlConnection connection)
        {
            var createTablesScript = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ReplicationConfigs' AND xtype='U')
                BEGIN
                    CREATE TABLE ReplicationConfigs (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        IsSimilarDatabase BIT NOT NULL,
                        CreatedDate DATETIME NOT NULL,
                        IsActive BIT NOT NULL DEFAULT 1
                    )
                END

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TableSelections' AND xtype='U')
                BEGIN
                    CREATE TABLE TableSelections (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        ConfigId INT NOT NULL,
                        TableName NVARCHAR(255) NOT NULL,
                        SelectedColumns NVARCHAR(MAX),
                        FOREIGN KEY (ConfigId) REFERENCES ReplicationConfigs(Id)
                    )
                END

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FieldMappings' AND xtype='U')
                BEGIN
                    CREATE TABLE FieldMappings (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        ConfigId INT NOT NULL,
                        TargetTable NVARCHAR(255) NOT NULL,
                        TargetField NVARCHAR(255) NOT NULL,
                        SourceTable NVARCHAR(255),
                        SourceField NVARCHAR(255),
                        UseCustomQuery BIT NOT NULL DEFAULT 0,
                        CustomQuery NVARCHAR(MAX),
                        FOREIGN KEY (ConfigId) REFERENCES ReplicationConfigs(Id)
                    )
                END";

            using (var cmd = new SqlCommand(createTablesScript, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<int> SaveMainConfig(SqlConnection connection, SqlTransaction transaction, ReplicationConfig config)
        {
            var sql = @"
                INSERT INTO ReplicationConfigs (IsSimilarDatabase, CreatedDate)
                VALUES (@IsSimilarDatabase, @CreatedDate);
                SELECT SCOPE_IDENTITY();";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@IsSimilarDatabase", config.IsSimilarDatabase);
                cmd.Parameters.AddWithValue("@CreatedDate", config.CreatedDate);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        private async Task SaveTableSelections(SqlConnection connection, SqlTransaction transaction, int configId, List<TableInfo> tables)
        {
            foreach (var table in tables)
            {
                var selectedColumns = table.Columns.Where(c => c.IsSelected).Select(c => c.Name);
                var columnsJson = string.Join(",", selectedColumns);

                var sql = @"
                    INSERT INTO TableSelections (ConfigId, TableName, SelectedColumns)
                    VALUES (@ConfigId, @TableName, @SelectedColumns)";

                using (var cmd = new SqlCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@ConfigId", configId);
                    cmd.Parameters.AddWithValue("@TableName", table.Name);
                    cmd.Parameters.AddWithValue("@SelectedColumns", columnsJson);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task SaveFieldMappings(SqlConnection connection, SqlTransaction transaction, int configId, List<FieldMapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                var sql = @"
                    INSERT INTO FieldMappings (ConfigId, TargetTable, TargetField, SourceTable, SourceField, UseCustomQuery, CustomQuery)
                    VALUES (@ConfigId, @TargetTable, @TargetField, @SourceTable, @SourceField, @UseCustomQuery, @CustomQuery)";

                using (var cmd = new Sql