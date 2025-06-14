using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using Microsoft.Data.SqlClient;

namespace SqlReplicator
{
    public partial class ConfigurationWindow : MetroWindow
    {
        private readonly string _baseConnectionString;
        private readonly string _sourceConnectionString;
        private readonly string _targetConnectionString;
        private readonly ObservableCollection<TableInfo> _sourceTables;
        private readonly ObservableCollection<TableInfo> _targetTables;
        private readonly ObservableCollection<FieldMapping> _fieldMappings;

        public ConfigurationWindow(string baseConnectionString, string sourceConnectionString, string targetConnectionString)
        {
            InitializeComponent();
            
            // Ensure panels are properly initialized
            SimilarDatabasesPanel.Visibility = Visibility.Visible;
            DifferentDatabasesPanel.Visibility = Visibility.Collapsed;
            
            _baseConnectionString = baseConnectionString ?? throw new ArgumentNullException(nameof(baseConnectionString));
            _sourceConnectionString = sourceConnectionString ?? throw new ArgumentNullException(nameof(sourceConnectionString));
            _targetConnectionString = targetConnectionString ?? throw new ArgumentNullException(nameof(targetConnectionString));

            _sourceTables = new ObservableCollection<TableInfo>();
            _targetTables = new ObservableCollection<TableInfo>();
            _fieldMappings = new ObservableCollection<FieldMapping>();

            TablesListBox.ItemsSource = _sourceTables;
            TargetTablesListBox.ItemsSource = _targetTables;
            FieldMappingDataGrid.ItemsSource = _fieldMappings;
        }

        private void DatabaseTypeChanged(object sender, RoutedEventArgs e)
        {
            if (SimilarDatabasesPanel == null || DifferentDatabasesPanel == null)
                return;

            if (SimilarDatabasesRadio.IsChecked == true)
            {
                SimilarDatabasesPanel.Visibility = Visibility.Visible;
                DifferentDatabasesPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SimilarDatabasesPanel.Visibility = Visibility.Collapsed;
                DifferentDatabasesPanel.Visibility = Visibility.Visible;
            }
        }

        private async void AnalyzeDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AnalyzeDatabasesButton.IsEnabled = false;
                AnalysisResultText.Text = "Analyzing database structures...";

                // Load database structures
                await LoadDatabaseStructure();

                // Analyze similarity
                bool isSimilar = AnalyzeSimilarity();
                AnalysisResultText.Text = isSimilar
                    ? "Databases have similar structures"
                    : "Databases have different structures";

                // Update UI based on similarity
                SimilarDatabasesRadio.IsChecked = isSimilar;
                DifferentDatabasesRadio.IsChecked = !isSimilar;
                DatabaseTypeChanged(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error analyzing databases: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AnalyzeDatabasesButton.IsEnabled = true;
            }
        }

        private async Task LoadDatabaseStructure()
        {
            _sourceTables.Clear();
            _targetTables.Clear();

            // Load source database structure
            using (var connection = new SqlConnection(_sourceConnectionString))
            {
                await connection.OpenAsync();
                var tables = await GetTables(connection);
                foreach (var table in tables)
                {
                    var fields = await GetFields(connection, table);
                    _sourceTables.Add(new TableInfo { TableName = table, Fields = fields });
                }
            }

            // Load target database structure
            using (var connection = new SqlConnection(_targetConnectionString))
            {
                await connection.OpenAsync();
                var tables = await GetTables(connection);
                foreach (var table in tables)
                {
                    var fields = await GetFields(connection, table);
                    _targetTables.Add(new TableInfo { TableName = table, Fields = fields });
                }
            }
        }

        private async Task<List<string>> GetTables(SqlConnection connection)
        {
            var tables = new List<string>();
            var command = new SqlCommand(
                @"SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE' 
                ORDER BY TABLE_NAME", connection);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var tableName = reader.GetString(0);
                    if (!string.IsNullOrEmpty(tableName))
                    {
                        tables.Add(tableName);
                    }
                }
            }
            return tables;
        }

        private async Task<List<FieldInfo>> GetFields(SqlConnection connection, string tableName)
        {
            var fields = new List<FieldInfo>();
            
            // Get all columns
            var command = new SqlCommand(
                @"SELECT c.COLUMN_NAME, c.DATA_TYPE, 
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY
                  FROM INFORMATION_SCHEMA.COLUMNS c
                  LEFT JOIN (
                      SELECT ku.TABLE_CATALOG, ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                      FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
                      JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
                          ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                          AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                  ) pk 
                  ON c.TABLE_CATALOG = pk.TABLE_CATALOG
                  AND c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                  AND c.TABLE_NAME = pk.TABLE_NAME
                  AND c.COLUMN_NAME = pk.COLUMN_NAME
                  WHERE c.TABLE_NAME = @TableName 
                  ORDER BY c.ORDINAL_POSITION", connection);
            
            command.Parameters.AddWithValue("@TableName", tableName);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var fieldName = reader.GetString(0);
                    var dataType = reader.GetString(1);
                    if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(dataType))
                    {
                        fields.Add(new FieldInfo
                        {
                            FieldName = fieldName,
                            DataType = dataType,
                            IsPrimaryKey = reader.GetInt32(2) == 1,
                            IsSelected = false
                        });
                    }
                }
            }
            return fields;
        }

        private bool AnalyzeSimilarity()
        {
            // Check if tables match
            var sourceTableNames = _sourceTables.Select(t => t.TableName).ToList();
            var targetTableNames = _targetTables.Select(t => t.TableName).ToList();

            if (!sourceTableNames.SequenceEqual(targetTableNames))
                return false;

            // Check if fields match for each table
            foreach (var sourceTable in _sourceTables)
            {
                var targetTable = _targetTables.FirstOrDefault(t => t.TableName == sourceTable.TableName);
                if (targetTable == null)
                    return false;

                var sourceFieldNames = sourceTable.Fields.Select(f => f.FieldName).ToList();
                var targetFieldNames = targetTable.Fields.Select(f => f.FieldName).ToList();

                if (!sourceFieldNames.SequenceEqual(targetFieldNames))
                    return false;
            }

            return true;
        }

        private void SelectAllTables_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var table in _sourceTables)
            {
                table.IsSelected = true;
            }
        }

        private void SelectAllTables_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var table in _sourceTables)
            {
                table.IsSelected = false;
            }
        }

        private void SelectAllFields_Checked(object sender, RoutedEventArgs e)
        {
            var selectedTable = TablesListBox.SelectedItem as TableInfo;
            if (selectedTable != null)
            {
                foreach (var field in selectedTable.Fields)
                {
                    field.IsSelected = true;
                }
            }
        }

        private void SelectAllFields_Unchecked(object sender, RoutedEventArgs e)
        {
            var selectedTable = TablesListBox.SelectedItem as TableInfo;
            if (selectedTable != null)
            {
                foreach (var field in selectedTable.Fields)
                {
                    field.IsSelected = false;
                }
            }
        }

        private async void TablesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTable = TablesListBox.SelectedItem as TableInfo;
            if (selectedTable != null)
            {
                FieldsListBox.ItemsSource = selectedTable.Fields;
                SelectedTableText.Text = selectedTable.TableName;
                UpdatePrimaryKeyComboBox(selectedTable);
            }
            else
            {
                FieldsListBox.ItemsSource = null;
                SelectedTableText.Text = "(Select a table)";
                PrimaryKeyComboBox.ItemsSource = null;
            }
        }

        private void UpdatePrimaryKeyComboBox(TableInfo table)
        {
            if (table == null)
                return;

            // Show all fields in the combo box
            PrimaryKeyComboBox.ItemsSource = table.Fields;
            PrimaryKeyComboBox.DisplayMemberPath = "FieldName";
            
            // If there's a primary key, select it
            var primaryKeyField = table.Fields.FirstOrDefault(f => f.IsPrimaryKey);
            if (primaryKeyField != null)
            {
                PrimaryKeyComboBox.SelectedItem = primaryKeyField;
            }
            else if (table.Fields.Any())
            {
                // If no primary key, select the first field
                PrimaryKeyComboBox.SelectedIndex = 0;
            }
        }

        private void PrimaryKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedField = PrimaryKeyComboBox.SelectedItem as FieldInfo;
            if (selectedField != null)
            {
                var selectedTable = TablesListBox.SelectedItem as TableInfo;
                if (selectedTable != null)
                {
                    selectedTable.SelectedPrimaryKey = selectedField.FieldName;
                }
            }
        }

        private void TargetTablesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTable = TargetTablesListBox.SelectedItem as TableInfo;
            if (selectedTable != null)
            {
                SelectedTargetTableText.Text = selectedTable.TableName;
                UpdateFieldMappings(selectedTable);
            }
            else
            {
                SelectedTargetTableText.Text = "(Select a target table)";
                FieldMappingDataGrid.ItemsSource = null;
            }
        }

        private void UpdateFieldMappings(TableInfo targetTable)
        {
            if (targetTable == null)
                return;

            _fieldMappings.Clear();
            foreach (var field in targetTable.Fields)
            {
                _fieldMappings.Add(new FieldMapping
                {
                    TargetField = field.FieldName,
                    TargetDataType = field.DataType,
                    IsSelected = false
                });
            }
        }

        private void RefreshTables_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDatabaseStructure();
        }

        private void RefreshTargetTables_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDatabaseStructure();
        }

        private void EditCustomQuery_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var mapping = button?.Tag as FieldMapping;
            if (mapping != null)
            {
                // TODO: Implement custom query editor
                MessageBox.Show("Custom query editor will be implemented here.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected tables
                var selectedTables = _sourceTables.Where(t => t.IsSelected).ToList();
                if (!selectedTables.Any())
                {
                    MessageBox.Show("Please select at least one table to replicate.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if primary key is selected for all tables
                var tablesWithoutPrimaryKey = selectedTables.Where(t => string.IsNullOrEmpty(t.SelectedPrimaryKey)).ToList();
                if (tablesWithoutPrimaryKey.Any())
                {
                    var tableNames = string.Join(", ", tablesWithoutPrimaryKey.Select(t => t.TableName));
                    MessageBox.Show($"Please select a primary key for the following tables:\n{tableNames}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save selected tables and fields to database
                using (var connection = new SqlConnection(_baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Create necessary tables if they don't exist
                            var createTablesCommand = new SqlCommand(@"
                                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TableSelections')
                                BEGIN
                                    CREATE TABLE TableSelections (
                                        Id INT IDENTITY(1,1) PRIMARY KEY,
                                        TableName NVARCHAR(128),
                                        IsSelected BIT,
                                        PrimaryKeyField NVARCHAR(128),
                                        CreatedAt DATETIME DEFAULT GETDATE()
                                    )
                                END
                                ELSE
                                BEGIN
                                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('TableSelections') AND name = 'PrimaryKeyField')
                                    BEGIN
                                        ALTER TABLE TableSelections ADD PrimaryKeyField NVARCHAR(128)
                                    END
                                END

                                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FieldMappings')
                                BEGIN
                                    CREATE TABLE FieldMappings (
                                        Id INT IDENTITY(1,1) PRIMARY KEY,
                                        TableName NVARCHAR(128),
                                        SourceField NVARCHAR(128),
                                        TargetField NVARCHAR(128),
                                        IsPrimaryKey BIT,
                                        CreatedAt DATETIME DEFAULT GETDATE()
                                    )
                                END", connection, transaction);

                            await createTablesCommand.ExecuteNonQueryAsync();

                            // Clear existing data
                            var clearCommand = new SqlCommand(@"
                                TRUNCATE TABLE TableSelections;
                                TRUNCATE TABLE FieldMappings;", connection, transaction);
                            await clearCommand.ExecuteNonQueryAsync();

                            // Save selected tables
                            var tableCommand = new SqlCommand(@"
                                INSERT INTO TableSelections (TableName, IsSelected, PrimaryKeyField)
                                VALUES (@TableName, @IsSelected, @PrimaryKeyField)", connection, transaction);

                            foreach (var table in selectedTables)
                            {
                                tableCommand.Parameters.Clear();
                                tableCommand.Parameters.AddWithValue("@TableName", table.TableName);
                                tableCommand.Parameters.AddWithValue("@IsSelected", table.IsSelected);
                                tableCommand.Parameters.AddWithValue("@PrimaryKeyField", table.SelectedPrimaryKey);
                                await tableCommand.ExecuteNonQueryAsync();
                            }

                            // Save field mappings
                            var fieldCommand = new SqlCommand(@"
                                INSERT INTO FieldMappings (TableName, SourceField, TargetField, IsPrimaryKey)
                                VALUES (@TableName, @SourceField, @TargetField, @IsPrimaryKey)", connection, transaction);

                            foreach (var table in selectedTables)
                            {
                                foreach (var field in table.Fields.Where(f => f.IsSelected))
                                {
                                    fieldCommand.Parameters.Clear();
                                    fieldCommand.Parameters.AddWithValue("@TableName", table.TableName);
                                    fieldCommand.Parameters.AddWithValue("@SourceField", field.FieldName);
                                    fieldCommand.Parameters.AddWithValue("@TargetField", field.FieldName);
                                    fieldCommand.Parameters.AddWithValue("@IsPrimaryKey", field.FieldName == table.SelectedPrimaryKey);
                                    await fieldCommand.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show("Table and field mappings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Error saving configuration: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class TableInfo
    {
        public string TableName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
        public string SelectedPrimaryKey { get; set; } = string.Empty;
    }

    public class FieldInfo
    {
        public string FieldName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsPrimaryKey { get; set; }
    }

    public class FieldMapping
    {
        public string TargetField { get; set; } = string.Empty;
        public string TargetDataType { get; set; } = string.Empty;
        public TableInfo SourceTable { get; set; } = new TableInfo();
        public string SourceField { get; set; } = string.Empty;
        public string CustomQuery { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsPrimaryKey { get; set; } = false;
    }
}
