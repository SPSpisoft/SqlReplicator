using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using Microsoft.Data.SqlClient;
using System.Collections.Generic; // Added for List
using System.Threading.Tasks; // Added for Task
using System.Windows.Input;
using System.ComponentModel;
using Microsoft.IdentityModel.Tokens;

namespace SqlReplicator
{
    public partial class ConfigurationWindow : MetroWindow
    {
        private readonly string _baseConnectionString;
        private readonly string _sourceConnectionString;
        private readonly string _targetConnectionString;
        public ObservableCollection<TableInfo> _sourceTables { get; private set; }
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

            // Set DataContext to this window so DataGrid can access _sourceTables
            this.DataContext = this;

            TablesListBox.ItemsSource = _sourceTables;
            TargetTablesListBox.ItemsSource = _targetTables;
            FieldMappingDataGrid.ItemsSource = _fieldMappings;
            ListenerTypeComboBox.SelectedIndex = 0; // Default to Trigger

            // اطمینان از وجود و صحت جداول کانفیگ
            _ = EnsureConfigTables();
        }

        /// <summary>
        /// ساخت یا اصلاح جداول کانفیگ به صورت داخلی (FieldMappings)
        /// </summary>
        private async Task EnsureConfigTables()
        {
            try
            {
                using (var connection = new SqlConnection(_baseConnectionString))
                {
                    await connection.OpenAsync();

                    // حذف جداول قبلی (در صورت وجود)
                    //var dropFieldMappings = new SqlCommand(@"IF OBJECT_ID('dbo.FieldMappings', 'U') IS NOT NULL DROP TABLE dbo.FieldMappings;", connection);
                    //await dropFieldMappings.ExecuteNonQueryAsync();

                    // FieldMappings
                    var cmdFieldMappings = new SqlCommand(@"
                                        IF OBJECT_ID('dbo.FieldMappings', 'U') IS NULL
                                        BEGIN
                                            CREATE TABLE dbo.FieldMappings (
                                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                                TargetTableName NVARCHAR(128) NOT NULL,
                                                SourceTableName NVARCHAR(128) NOT NULL,
                                                SourceField NVARCHAR(128) NOT NULL,
                                                TargetField NVARCHAR(128) NOT NULL,
                                                CustomQuery NVARCHAR(MAX) NULL,
                                                IsPrimaryKey BIT NOT NULL DEFAULT 0,
                                                CreatedAt DATETIME DEFAULT GETDATE()
                                            );
                                        END
                                        ELSE
                                        BEGIN
                                            IF COL_LENGTH('FieldMappings', 'TargetTableName') IS NULL
                                                ALTER TABLE FieldMappings ADD TargetTableName NVARCHAR(128) NOT NULL DEFAULT('');
                                            IF COL_LENGTH('FieldMappings', 'IsPrimaryKey') IS NULL
                                                ALTER TABLE FieldMappings ADD IsPrimaryKey BIT NOT NULL DEFAULT 0;
                                            -- حذف TableName قدیمی اگر وجود دارد
                                            IF COL_LENGTH('FieldMappings', 'TableName') IS NOT NULL
                                                ALTER TABLE FieldMappings DROP COLUMN TableName;
                                        END
                                        ", connection);
                    await cmdFieldMappings.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ensuring config tables: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            try
            {
                // Load source database structure
                using (var connection = new SqlConnection(_sourceConnectionString))
                {
                    await connection.OpenAsync();
                    var sourceTableNames = await GetTables(connection);
                    _sourceTables.Clear();
                    foreach (var tableName in sourceTableNames)
                    {
                        var fields = await GetFields(connection, tableName);
                        _sourceTables.Add(new TableInfo
                        {
                            TableName = tableName,
                            Fields = fields
                        });
                    }
                }

                // Load target database structure
                using (var connection = new SqlConnection(_targetConnectionString))
                {
                    await connection.OpenAsync();
                    var targetTableNames = await GetTables(connection);
                    _targetTables.Clear();
                    foreach (var tableName in targetTableNames)
                    {
                        var fields = await GetFields(connection, tableName);
                        _targetTables.Add(new TableInfo
                        {
                            TableName = tableName,
                            Fields = fields
                        });
                    }
                }

                // تست: نمایش اطلاعات لود شده
                var sourceTablesInfo = string.Join(", ", _sourceTables.Select(t => $"{t.TableName}({t.Fields.Count})"));
                var targetTablesInfo = string.Join(", ", _targetTables.Select(t => $"{t.TableName}({t.Fields.Count})"));
                //MessageBox.Show($"Source Tables: {sourceTablesInfo}\nTarget Tables: {targetTablesInfo}", "Loaded Data");

                // Update UI
                TablesListBox.ItemsSource = _sourceTables;
                TargetTablesListBox.ItemsSource = _targetTables;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading database structure: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>
        /// بارگذاری کانفیگ جدول مقصد از دیتابیس و نمایش روی فرم
        /// </summary>
        private async Task LoadTargetTableConfig(string targetTableName)
        {
            try
            {
                using (var connection = new SqlConnection(_baseConnectionString))
                {
                    await connection.OpenAsync();
                    // بارگذاری مپینگ فیلدها
                    var mappings = new ObservableCollection<FieldMapping>();
                    using (var cmd = new SqlCommand("SELECT SourceTableName, SourceField, TargetField, CustomQuery, IsPrimaryKey FROM FieldMappings WHERE TargetTableName = @TargetTableName", connection))
                    {
                        cmd.Parameters.AddWithValue("@TargetTableName", targetTableName);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                mappings.Add(new FieldMapping
                                {
                                    SourceTableName = reader.GetString(0),
                                    SourceField = reader.GetString(1),
                                    TargetField = reader.GetString(2),
                                    CustomQuery = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    IsSelected = true
                                });
                            }
                        }
                    }
                    // اگر هیچ مپینگی وجود نداشت، دیتاگرید را با فیلدهای جدول مقصد پر کن
                    if (mappings.Count == 0)
                    {
                        var targetTable = _targetTables.FirstOrDefault(t => t.TableName == targetTableName);
                        if (targetTable != null)
                        {
                            foreach (var field in targetTable.Fields)
                            {
                                mappings.Add(new FieldMapping
                                {
                                    TargetTableName = targetTable.TableName,
                                    TargetField = field.FieldName,
                                    TargetDataType = field.DataType,
                                    IsSelected = false,
                                    SourceTableName = "",
                                    SourceField = ""
                                });
                            }
                        }
                    }
                    // نمایش روی فرم
                    var targetTableObj = _targetTables.FirstOrDefault(t => t.TableName == targetTableName);
                    if (targetTableObj != null)
                    {
                        // کلید اصلی را از مپینگ‌ها استخراج کن
                        var pkMapping = mappings.FirstOrDefault(m => m.TargetField == targetTableObj.SelectedPrimaryKey || m.IsSelected);
                        if (pkMapping != null && pkMapping.IsSelected)
                            targetTableObj.SelectedPrimaryKey = pkMapping.TargetField;
                        _fieldMappings.Clear();
                        foreach (var m in mappings)
                            _fieldMappings.Add(m);
                        FieldMappingDataGrid.ItemsSource = _fieldMappings;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading table config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ذخیره کانفیگ جدول مقصد جاری
        /// </summary>
        private async Task SaveTargetTableConfig(TableInfo targetTable)
        {
            try
            {
                // بررسی انتخاب کلید اصلی جدید
                if (!_fieldMappings.Any(f => f.IsSelected && f.IsPrimaryKey))
                {
                    MessageBox.Show("لطفاً یک فیلد را به عنوان کلید اصلی جدول مقصد انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                using (var connection = new SqlConnection(_baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // حذف رکوردهای قبلی جدول مقصد
                        var delFieldsCmd = new SqlCommand("DELETE FROM FieldMappings WHERE TargetTableName = @TargetTableName", connection, transaction);
                        delFieldsCmd.Parameters.AddWithValue("@TargetTableName", targetTable.TableName);
                        await delFieldsCmd.ExecuteNonQueryAsync();
                        // درج فقط فیلدهای انتخاب‌شده
                        var insFieldCmd = new SqlCommand("INSERT INTO FieldMappings (TargetTableName, SourceTableName, SourceField, TargetField, CustomQuery, IsPrimaryKey) VALUES (@TargetTableName, @SourceTableName, @SourceField, @TargetField, @CustomQuery, @IsPrimaryKey)", connection, transaction);
                        foreach (var mapping in _fieldMappings.Where(m => m.IsSelected))
                        {
                            insFieldCmd.Parameters.Clear();
                            insFieldCmd.Parameters.AddWithValue("@TargetTableName", targetTable.TableName);
                            insFieldCmd.Parameters.AddWithValue("@SourceTableName", mapping.SourceTableName ?? "");
                            insFieldCmd.Parameters.AddWithValue("@SourceField", mapping.SourceField ?? "");
                            insFieldCmd.Parameters.AddWithValue("@TargetField", mapping.TargetField ?? "");
                            insFieldCmd.Parameters.AddWithValue("@CustomQuery", mapping.CustomQuery ?? "");
                            insFieldCmd.Parameters.AddWithValue("@IsPrimaryKey", mapping.IsPrimaryKey ? 1 : 0);
                            await insFieldCmd.ExecuteNonQueryAsync();
                        }
                        transaction.Commit();
                        targetTable.IsDirty = false;
                        MessageBox.Show("کانفیگ جدول مقصد ذخیره شد.", "ذخیره", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving table config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // به‌روزرسانی رویداد ذخیره کانفیگ جدول مقصد
        private async void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var selectedTable = TargetTablesListBox.SelectedItem as TableInfo;
            if (selectedTable == null)
            {
                MessageBox.Show("جدول مقصدی انتخاب نشده است.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await SaveTargetTableConfig(selectedTable);
        }

        private async void GenerateServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // خواندن آخرین کانفیگ از ReplicationConfig
                string listenerType = "Trigger";
                string sourceConnectionString = string.Empty;
                string targetConnectionString = string.Empty;
                var fieldMappings = new List<FieldMapping>();
                using (var connection = new SqlConnection(_baseConnectionString))
                {
                    await connection.OpenAsync();
                    // خواندن ListenerType و ConnectionStrings
                    using (var cmd = new SqlCommand(@"SELECT TOP 1 SourceConnectionString, TargetConnectionString, ListenerType FROM ReplicationConfig ORDER BY Id DESC", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            sourceConnectionString = reader.GetString(0);
                            targetConnectionString = reader.GetString(1);
                            if (!reader.IsDBNull(2))
                                listenerType = reader.GetString(2);
                        }
                    }
                    // خواندن فیلدهای انتخاب‌شده
                    using (var cmd = new SqlCommand(@"SELECT TargetTableName, SourceTableName, SourceField, TargetField, CustomQuery, IsPrimaryKey FROM FieldMappings ", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            fieldMappings.Add(new FieldMapping
                            {
                                TargetTableName = reader.GetString(0),
                                SourceTableName = reader.GetString(1),
                                SourceField = reader.GetString(2),
                                TargetField = reader.GetString(3),
                                CustomQuery = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                IsPrimaryKey = !reader.IsDBNull(5) && reader.GetBoolean(5),
                            });
                        }
                    }
                }
                if (_fieldMappings.Count == 0)
                {
                    MessageBox.Show("There Is Not Mapping Set", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var generator = new SqlServiceGenerator(sourceConnectionString, targetConnectionString, fieldMappings, listenerType);
                await generator.GenerateServices();
                MessageBox.Show("سرویسها با موفقیت ساخته شدند.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                // دیالوگ تایید برای استارت سرویس
                var result = MessageBox.Show("Do you want to install/start the Windows service now?", "Start Service", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // فرض: baseConnectionString همان _baseConnectionString است
                    var progress = new Progress<Tuple<string, bool>>(report =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(report.Item1, report.Item2 ? "Service" : "Error", MessageBoxButton.OK, report.Item2 ? MessageBoxImage.Information : MessageBoxImage.Error);
                        });
                    });
                    bool success = await ServiceInstallerManager.ManageReplicationService(_baseConnectionString, progress);
                    if (success)
                        MessageBox.Show("Service started successfully!", "Service", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Service failed to start.", "Service", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("آیا از حذف کامل کانفیگ مطمئن هستید؟ این عملیات غیرقابل بازگشت است.", "حذف کانفیگ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            try
            {
                using (var connection = new SqlConnection(_baseConnectionString))
                {
                    await connection.OpenAsync();
                    var deleteFields = new SqlCommand("DELETE FROM FieldMappings", connection);
                    await deleteFields.ExecuteNonQueryAsync();
                }
                _sourceTables.Clear();
                _fieldMappings.Clear();
                MessageBox.Show("کانفیگ با موفقیت حذف شد.", "حذف کانفیگ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string ListenerType => (ListenerTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Trigger";

        private void SourceTableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is FieldMapping mapping)
            {
                var selectedTable = comboBox.SelectedValue as string;
                mapping.SourceTableName = selectedTable ?? "";
            }
        }

        // فعال‌سازی Dirty Flag هنگام تغییر کلید اصلی جدول مقصد
        private void PrimaryKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedField = PrimaryKeyComboBox.SelectedItem as FieldInfo;
            if (selectedField != null)
            {
                var selectedTable = TargetTablesListBox.SelectedItem as TableInfo;
                if (selectedTable != null)
                {
                    selectedTable.SelectedPrimaryKey = selectedField.FieldName;
                    selectedTable.IsDirty = true;
                }
            }
        }

        // فعال‌سازی Dirty Flag هنگام تغییر مپینگ فیلدها در DataGrid
        private void FieldMappingDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var selectedTable = TargetTablesListBox.SelectedItem as TableInfo;
            if (selectedTable != null)
            {
                selectedTable.IsDirty = true;
            }
        }

        // رویداد انتخاب جدول مقصد (برای XAML)
        private async void TargetTablesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // کنترل Dirty Flag جدول قبلی
            var previousTable = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as TableInfo : null;
            if (previousTable != null && previousTable.IsDirty)
            {
                var result = MessageBox.Show($"تغییرات جدول '{previousTable.TableName}' ذخیره نشده است. آیا مایل به ذخیره هستید؟", "ذخیره تغییرات", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel)
                {
                    // بازگشت به جدول قبلی
                    TargetTablesListBox.SelectedItem = previousTable;
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    await SaveTargetTableConfig(previousTable);
                }
                else if (result == MessageBoxResult.No)
                {
                    previousTable.IsDirty = false;
                }
            }
            var selectedTable = TargetTablesListBox.SelectedItem as TableInfo;
            if (selectedTable != null)
            {
                SelectedTargetTableText.Text = selectedTable.TableName;
                await LoadTargetTableConfig(selectedTable.TableName);
            }
            else
            {
                SelectedTargetTableText.Text = "(Select a target table)";
                FieldMappingDataGrid.ItemsSource = null;
            }
        }

        // کنترل قبل از خروج از فرم
        protected override void OnClosing(CancelEventArgs e)
        {
            var dirtyTable = _targetTables.FirstOrDefault(t => t.IsDirty);
            if (dirtyTable != null)
            {
                var result = MessageBox.Show($"تغییرات جدول '{dirtyTable.TableName}' ذخیره نشده است. آیا مایل به ذخیره هستید؟", "ذخیره تغییرات", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    SaveTargetTableConfig(dirtyTable).Wait();
                }
                else if (result == MessageBoxResult.No)
                {
                    dirtyTable.IsDirty = false;
                }
            }
            base.OnClosing(e);
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Do you want to install/start the Windows service now?", "Start Service", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            var progress = new Progress<Tuple<string, bool>>(report =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(report.Item1, report.Item2 ? "Service" : "Error", MessageBoxButton.OK, report.Item2 ? MessageBoxImage.Information : MessageBoxImage.Error);
                });
            });
            bool success = await ServiceInstallerManager.ManageReplicationService(_baseConnectionString, progress);
            if (success)
                MessageBox.Show("Service started successfully!", "Service", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Service failed to start.", "Service", MessageBoxButton.OK, MessageBoxImage.Error);
        }


    }

    public class TableInfo : IEquatable<TableInfo>
    {
        public string TableName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
        public string SelectedPrimaryKey { get; set; } = string.Empty;
        // Dirty flag برای تشخیص تغییرات جدول مقصد
        public bool IsDirty { get; set; } = false;

        public bool Equals(TableInfo? other)
        {
            if (other is null) return false;
            return TableName == other.TableName;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TableInfo);
        }

        public override int GetHashCode()
        {
            return TableName?.GetHashCode() ?? 0;
        }
    }

    public class FieldInfo
    {
        public string FieldName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsPrimaryKey { get; set; }
    }

    public class FieldMapping : INotifyPropertyChanged
    {
        public string TargetTableName { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
        public string TargetDataType { get; set; } = string.Empty;
        public TableInfo SourceTable { get; set; } = new TableInfo();
        private string _sourceField = "";
        public string SourceField
        {
            get => _sourceField;
            set
            {
                if (_sourceField != value)
                {
                    _sourceField = value;
                    OnPropertyChanged(nameof(SourceField));

                    // منطق تعیین IsPrimaryKey بر اساس فیلد انتخابی از جدول مبدا
                    var window = Application.Current.Windows.OfType<ConfigurationWindow>().FirstOrDefault();
                    if (window != null && !string.IsNullOrEmpty(SourceTableName) && !string.IsNullOrEmpty(_sourceField))
                    {
                        var table = window._sourceTables.FirstOrDefault(t => t.TableName == SourceTableName);
                        if (table != null)
                        {
                            var field = table.Fields.FirstOrDefault(f => f.FieldName == _sourceField);
                            IsPrimaryKey = field != null && field.IsPrimaryKey;
                        }
                        else
                        {
                            IsPrimaryKey = false;
                        }
                    }
                    else
                    {
                        IsPrimaryKey = false;
                    }
                }
            }
        }
        public string CustomQuery { get; set; } = string.Empty;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        public bool IsPrimaryKey { get; set; } = false;
        //private bool _isPrimaryKey;
        //public bool IsPrimaryKey
        //{
        //    get => _isPrimaryKey;
        //    set
        //    {
        //        if (_isPrimaryKey != value)
        //        {
        //            _isPrimaryKey = value;
        //            OnPropertyChanged(nameof(IsPrimaryKey));
        //        }
        //    }
        //}

        private string _sourceTableName = "";
        public ObservableCollection<string> AvailableSourceFields { get; set; } = new ObservableCollection<string>();

        public string SourceTableName
        {
            get => _sourceTableName;
            set
            {
                _sourceTableName = value;
                OnPropertyChanged(nameof(SourceTableName));

                // به‌روزرسانی AvailableSourceFields
                AvailableSourceFields.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    var window = Application.Current.Windows.OfType<ConfigurationWindow>().FirstOrDefault();
                    if (window != null)
                    {
                        var table = window._sourceTables.FirstOrDefault(t => t.TableName == value);
                        if (table != null)
                        {
                            foreach (var field in table.Fields)
                            {
                                AvailableSourceFields.Add(field.FieldName);
                            }
                        }
                    }
                }

                // ریست کردن SourceField
                SourceField = "";
                OnPropertyChanged(nameof(SourceField));
                // حذف Refresh DataGrid از اینجا
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


    }
}
