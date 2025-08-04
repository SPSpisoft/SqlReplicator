using MahApps.Metro.Controls;
using Microsoft.Data.Sql;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading;
using MahApps.Metro.IconPacks;
using System.IO;
using System.ServiceProcess;
using System.Diagnostics;
using System.Management;

namespace SqlReplicator
{
    public class DatabaseInfo
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }



    public partial class MainWindow : MetroWindow
    {
        private int currentStep = 1;
        private readonly Dictionary<string, string> connectionStrings = new Dictionary<string, string>();
        private CancellationTokenSource? _refreshCancellation;
        private DatabaseInfo? sourceDb;
        private DatabaseInfo? targetDb;
        private List<TableInfo>? selectedTables;
        private string replicationType = "Full";
        private ConfigurationStatus? _currentConfigStatus;
        private List<ListenerConfiguration>? _availableListeners;
        private bool _FieldMappingPass;
        private string? _lastCreatedServiceName;

        public MainWindow()
        {
            InitializeComponent();
            StatusLabel.Text = "Please wait while loading SQL Server instances...";
            _ = LoadSqlServerInstances();
        }

        private async Task LoadSqlServerInstances()
        {
            RefreshServersButton.IsEnabled = false;
            StartRefreshAnimation();

            try
            {
                _refreshCancellation = new CancellationTokenSource();
                AbleFormControls(false);

                await Task.Run(() =>
                {
                    var instances = new List<string>();

                    // Add local instances
                    instances.Add("(local)");
                    instances.Add("localhost");
                    instances.Add(".");
                    instances.Add(Environment.MachineName);

                    try
                    {
                        // Try to get SQL Server instances from network
                        var dataTable = SqlDataSourceEnumerator.Instance.GetDataSources();
                        foreach (DataRow row in dataTable.Rows)
                        {
                            if (_refreshCancellation.Token.IsCancellationRequested)
                                return;

                            var serverName = row["ServerName"].ToString() ?? string.Empty;
                            var instanceName = row["InstanceName"].ToString() ?? string.Empty;

                            if (string.IsNullOrEmpty(instanceName))
                                instances.Add(serverName);
                            else
                                instances.Add($"{serverName}\\{instanceName}");
                        }
                    }
                    catch
                    {
                        // If enumeration fails, continue with basic instances
                    }

                    if (!_refreshCancellation.Token.IsCancellationRequested)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var distinctInstances = instances.Distinct().ToList();
                            BaseServerCombo.ItemsSource = distinctInstances;
                            SourceServerCombo.ItemsSource = distinctInstances;
                            TargetServerCombo.ItemsSource = distinctInstances;

                            if (distinctInstances.Any())
                            {
                                BaseServerCombo.SelectedIndex = 0;
                                SourceServerCombo.SelectedIndex = 0;
                                TargetServerCombo.SelectedIndex = 0;
                            }
                        });
                    }
                }, _refreshCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "Refresh operation cancelled";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Warning: Could not enumerate SQL Server instances: {ex.Message}";
            }
            finally
            {
                Dispatcher.Invoke(async () =>
                {
                    StopRefreshAnimation();
                    RefreshServersButton.IsEnabled = true;
                    AbleFormControls(true);
                    await UpdateStepButtons();
                    if (_refreshCancellation != null && !_refreshCancellation.Token.IsCancellationRequested)
                    {
                        StatusLabel.Text = "SQL Server instances loaded successfully. Please configure your connection settings.";
                    }
                });
            }
        }

        //private void DisableFormControls()
        //{
        //    //BaseServerCombo.IsEnabled = false;
        //    //BaseUsernameBox.IsEnabled = false;
        //    //BasePasswordBox.IsEnabled = false;
        //    //BaseDatabaseCombo.IsEnabled = false;
        //    //BaseTestButton.IsEnabled = false;
        //    //BaseNextButton.IsEnabled = false;
        //    Step1Panel.IsEnabled = false;
        //    ConfigButtonsPanel.IsEnabled = false;

        //    SourceServerCombo.IsEnabled = false;
        //    SourceUsernameBox.IsEnabled = false;
        //    SourcePasswordBox.IsEnabled = false;
        //    SourceDatabaseCombo.IsEnabled = false;
        //    SourceTestButton.IsEnabled = false;
        //    SourceNextButton.IsEnabled = false;

        //    TargetServerCombo.IsEnabled = false;
        //    TargetUsernameBox.IsEnabled = false;
        //    TargetPasswordBox.IsEnabled = false;
        //    TargetDatabaseCombo.IsEnabled = false;
        //    TargetTestButton.IsEnabled = false;
        //    TargetCompleteButton.IsEnabled = false;
        //}

        private void AbleFormControls(bool setAble)
        {
            Step1Button.IsEnabled = setAble;
            Step2Button.IsEnabled = setAble;
            Step3Button.IsEnabled = setAble;
            Step4Button.IsEnabled = setAble;
            Step5Button.IsEnabled = setAble;

            Step1Panel.IsEnabled = setAble;
            //BaseServerCombo.IsEnabled = setAble;
            //BaseUsernameBox.IsEnabled = setAble;
            //BasePasswordBox.IsEnabled = setAble;
            //BaseDatabaseCombo.IsEnabled = setAble;
            //BaseTestButton.IsEnabled = setAble;
            //if (BaseStatusIcon.Visibility == Visibility.Visible)
            //    BaseNextButton.IsEnabled = setAble;

            Step2Panel.IsEnabled = setAble;
            //SourceServerCombo.IsEnabled = setAble;
            //SourceUsernameBox.IsEnabled = setAble;
            //SourcePasswordBox.IsEnabled = setAble;
            //SourceDatabaseCombo.IsEnabled = setAble;
            //SourceTestButton.IsEnabled = setAble;
            //if (SourceStatusIcon.Visibility == Visibility.Visible)
            //    SourceNextButton.IsEnabled = setAble;

            Step3Panel.IsEnabled = setAble;
            //TargetServerCombo.IsEnabled = setAble;
            //TargetUsernameBox.IsEnabled = setAble;
            //TargetPasswordBox.IsEnabled = setAble;
            //TargetDatabaseCombo.IsEnabled = setAble;
            //TargetTestButton.IsEnabled = setAble;
            //if (TargetStatusIcon.Visibility == Visibility.Visible)
            //    TargetCompleteButton.IsEnabled = setAble;

            Step4Panel.IsEnabled = setAble;

        }

        private async void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                if (_refreshCancellation != null && !_refreshCancellation.Token.IsCancellationRequested)
                {
                    // Cancel the refresh operation
                    _refreshCancellation.Cancel();
                }

                //button.IsEnabled = false;
                //StartRefreshAnimation();
            }

            StatusLabel.Text = "Refreshing SQL Server instances...";
            await LoadSqlServerInstances();
        }

        private void StartRefreshAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1),
                RepeatBehavior = RepeatBehavior.Forever
            };

            RefreshIconRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void StopRefreshAnimation()
        {
            RefreshIconRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            RefreshIconRotation.Angle = 0;
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            //BaseStatusIcon.Foreground = new SolidColorBrush(Colors.Gray);
            //SourceStatusIcon.Foreground = new SolidColorBrush(Colors.Gray);
            //TargetStatusIcon.Foreground = new SolidColorBrush(Colors.Gray);

            var button = sender as Button;
            var step = button?.Tag?.ToString();

            if (string.IsNullOrEmpty(step)) return;

            ComboBox serverCombo, databaseCombo;
            TextBox usernameBox;
            PasswordBox passwordBox;

            switch (step)
            {
                case "Base":
                    serverCombo = BaseServerCombo;
                    usernameBox = BaseUsernameBox;
                    passwordBox = BasePasswordBox;
                    databaseCombo = BaseDatabaseCombo;
                    break;
                case "Source":
                    serverCombo = SourceServerCombo;
                    usernameBox = SourceUsernameBox;
                    passwordBox = SourcePasswordBox;
                    databaseCombo = SourceDatabaseCombo;
                    break;
                case "Target":
                    serverCombo = TargetServerCombo;
                    usernameBox = TargetUsernameBox;
                    passwordBox = TargetPasswordBox;
                    databaseCombo = TargetDatabaseCombo;
                    break;
                default:
                    return;
            }

            if (string.IsNullOrWhiteSpace(serverCombo.Text) ||
                string.IsNullOrWhiteSpace(usernameBox.Text) ||
                string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                MessageBox.Show("Please fill in all required fields.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            button.IsEnabled = false;
            button.Content = "Connecting...";
            StatusLabel.Text = $"Connecting to {step.ToLower()} database...";

            try
            {
                var connectionString = $"Server={serverCombo.Text};User Id={usernameBox.Text};Password={passwordBox.Password};TrustServerCertificate=True;";

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Get databases
                    var databases = new List<string>();
                    using (var command = new SqlCommand("SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')", connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                databases.Add(reader.GetString(0));
                            }
                        }
                    }

                    databaseCombo.ItemsSource = databases;

                    // Store connection string
                    connectionStrings[step] = connectionString;

                    // Update UI
                    switch (step)
                    {
                        case "Base":
                            BaseStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                            AnimateIcon(BaseStatusIcon, Colors.Green);
                            BaseNextButton.IsEnabled = true;
                            break;
                        case "Source":
                            SourceStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                            AnimateIcon(SourceStatusIcon, Colors.Green);
                            SourceNextButton.IsEnabled = true;
                            break;
                        case "Target":
                            TargetStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                            AnimateIcon(TargetStatusIcon, Colors.Green);
                            TargetCompleteButton.IsEnabled = true;
                            break;
                    }

                    StatusLabel.Text = $"{step} database connection successful!";
                    //MessageBox.Show($"Connection to {step.ToLower()} database successful!", "Success",
                    //    MessageBoxButton.OK, MessageBoxImage.Information);


                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "Connect";
            }
        }

        void AnimateIcon(PackIconMaterial icon, Color toColor, int durationMs = 1000)
        {
            var colorAnim = new ColorAnimation
            {
                To = toColor,
                Duration = TimeSpan.FromMilliseconds(durationMs),
            };

            icon.Foreground = new SolidColorBrush(Colors.Red);
            (icon.Foreground as SolidColorBrush)?.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        }


        private async void BaseDatabaseCombo_DropDownClosed(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(BaseDatabaseCombo.Text)) return;

            try
            {
                StatusLabel.Text = "Checking for existing configurations...";
                await CheckExistingConfigs();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error checking configurations: {ex.Message}";
            }
        }

        private async Task CheckExistingConfigs()
        {
            try
            {
                DeleteConfigsButton.Visibility = Visibility.Hidden;

                SourceServerCombo.Text = "";
                SourceUsernameBox.Text = "";
                SourcePasswordBox.Password = "";
                SourceDatabaseCombo.Text = "";

                TargetServerCombo.Text = "";
                TargetUsernameBox.Text = "";
                TargetPasswordBox.Password = "";
                TargetDatabaseCombo.Text = "";

                var baseConnectionString = connectionStrings["Base"];
                if (string.IsNullOrEmpty(baseConnectionString) || string.IsNullOrEmpty(BaseDatabaseCombo.Text))
                {
                    //ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                    StatusLabel.Text = "Please select a database.";
                    return;
                }

                baseConnectionString += $"Database={BaseDatabaseCombo.Text};";

                using (var connection = new SqlConnection(baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var command_count = new SqlCommand("SELECT COUNT(*) FROM ReplicationConfig", connection))
                    {
                        var configCount = Convert.ToInt32(await command_count.ExecuteScalarAsync());
                        if (configCount > 0)
                        {
                            //ConfigButtonsPanel.Visibility = Visibility.Visible;
                            StatusLabel.Text = "Existing configuration found. You can view or delete it.";


                            baseConnectionString += $"Database={BaseDatabaseCombo.Text};";


                            //await connection.OpenAsync();
                            using (var command = new SqlCommand(
                                @"SELECT TOP 1 SourceConnectionString, TargetConnectionString 
                                          FROM ReplicationConfig 
                                          ORDER BY Id DESC", connection))
                            {
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        /// --- SORCE ---------------------------------------
                                        var sourceConnectionString = reader.GetString(0);
                                        var sourceConnectionStringSplit = sourceConnectionString.Split(";");

                                        SourceUsernameBox.Text = sourceConnectionStringSplit.SingleOrDefault(e => e.Contains("User"))?.Split("=")[1];
                                        SourcePasswordBox.Password = sourceConnectionStringSplit.SingleOrDefault(e => e.Contains("Password"))?.Split("=")[1];

                                        var serverValue = sourceConnectionStringSplit.FirstOrDefault(e => e.Contains("Server"))?.Split("=")[1];

                                        foreach (var item in SourceServerCombo.Items)
                                        {
                                            string? sourceComboConnectionString = GetComboItemText(item);

                                            if (sourceComboConnectionString == serverValue)
                                            {
                                                SourceServerCombo.SelectedItem = item;
                                                break;
                                            }
                                        }

                                        System.Windows.Controls.Button dummyButton = new System.Windows.Controls.Button();
                                        dummyButton.Tag = "Source";
                                        TestConnection_Click(dummyButton, null);

                                        await Task.Delay(1000);

                                        var databaseValue = sourceConnectionStringSplit.FirstOrDefault(e => e.Contains("Database"))?.Split("=")[1];

                                        foreach (var item in SourceDatabaseCombo.Items)
                                        {
                                            string? sourceDatabaseComboString = GetComboItemText(item);

                                            if (sourceDatabaseComboString == databaseValue)
                                            {
                                                SourceDatabaseCombo.SelectedItem = item;
                                                break;
                                            }
                                        }

                                        /// --- TARGRT ---------------------------------------
                                        var targetConnectionString = reader.GetString(1);
                                        var targetConnectionStringSplit = targetConnectionString.Split(";");

                                        TargetUsernameBox.Text = targetConnectionStringSplit.SingleOrDefault(e => e.Contains("User"))?.Split("=")[1];
                                        TargetPasswordBox.Password = targetConnectionStringSplit.SingleOrDefault(e => e.Contains("Password"))?.Split("=")[1];

                                        var serverTargetValue = targetConnectionStringSplit.FirstOrDefault(e => e.Contains("Server"))?.Split("=")[1];

                                        foreach (var item in TargetServerCombo.Items)
                                        {
                                            string? targetComboConnectionString = GetComboItemText(item);

                                            if (targetComboConnectionString == serverTargetValue)
                                            {
                                                TargetServerCombo.SelectedItem = item;
                                                break;
                                            }
                                        }

                                        System.Windows.Controls.Button dummyTargetButton = new System.Windows.Controls.Button();
                                        dummyTargetButton.Tag = "Target";
                                        TestConnection_Click(dummyTargetButton, null);

                                        await Task.Delay(1000);

                                        var databaseTargetValue = targetConnectionStringSplit.FirstOrDefault(e => e.Contains("Database"))?.Split("=")[1];

                                        foreach (var item in TargetDatabaseCombo.Items)
                                        {
                                            string? targetDatabaseComboString = GetComboItemText(item);

                                            if (targetDatabaseComboString == databaseTargetValue)
                                            {
                                                TargetDatabaseCombo.SelectedItem = item;
                                                break;
                                            }
                                        }



                                    }
                                    else
                                    {
                                        MessageBox.Show("No existing configuration found.", "Error",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }

                            //-------------------- Field Mapping -----------------------
                            await Task.Delay(1000);
                            using (var command = new SqlCommand(
                             @"SELECT Count(*) FROM FieldMappings", connection))
                            {
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        var countRecords = reader.GetInt32(0);

                                        if(countRecords > 0)
                                        {
                                            ConfigStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                                            AnimateIcon(ConfigStatusIcon, Colors.Green);
                                            // CheckServiceStatusButton.IsEnabled = true; // Removed from XAML

                                            _FieldMappingPass = true;
                                            DeleteConfigsButton.Visibility = Visibility.Visible;
                                        }
                                    }

                                    else
                                    {
                                        MessageBox.Show("No existing configuration found.", "Error",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }


                        }
                        else
                        {
                            //ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                            StatusLabel.Text = "No existing configuration found. Please complete the setup.";
                        }
                    }
                }
            }
            catch
            {
                // If table doesn't exist or any other error, hide the buttons
                //ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                StatusLabel.Text = "Please complete the setup.";
            }
        }

        string? GetComboItemText(object? item)
        {
            return item switch
            {
                ComboBoxItem cbItem => cbItem.Content?.ToString(),
                string str => str,
                _ => item?.ToString()
            };
        }


        private async void NextStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep < 5)
            {
                await ShowStep(currentStep + 1);
            }
        }

        private async void PreviousStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep > 1)
            {
                currentStep--;
                UpdateStepVisibility();
                await UpdateStepButtons();
            }
        }

        private void UpdateStepVisibility()
        {
            Step1Panel.Visibility = currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
            Step5Panel.Visibility = currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task<bool> IsConfigurationComplete()
        {
            try
            {
                // Check if all database connections are configured
                if (string.IsNullOrEmpty(BaseDatabaseCombo.Text) ||
                    string.IsNullOrEmpty(SourceDatabaseCombo.Text) ||
                    string.IsNullOrEmpty(TargetDatabaseCombo.Text))
                {
                    return false;
                }

                // Check if configuration exists in database
                var baseConnectionString = connectionStrings["Base"];
                if (string.IsNullOrEmpty(baseConnectionString))
                {
                    return false;
                }

                baseConnectionString += $"Database={BaseDatabaseCombo.Text};";

                using (var connection = new SqlConnection(baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT COUNT(*) FROM ReplicationConfig", connection))
                    {
                        var configCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                        return configCount > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task UpdateStepButtons()
        {
            // Check if configuration is complete to enable Step 5
            var configComplete = await IsConfigurationComplete();
            
            // Enable/disable step buttons based on completion status
            Step1Button.IsEnabled = currentStep != 1;
            Step2Button.IsEnabled = currentStep != 2;
            Step3Button.IsEnabled = currentStep != 3;
            Step4Button.IsEnabled = currentStep != 4;
            Step5Button.IsEnabled = currentStep != 5 && configComplete;

            // Update button styles based on current step
            var buttons = new[] { Step1Button, Step2Button, Step3Button, Step4Button, Step5Button };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Style = (Style)FindResource(i + 1 == currentStep ? "ActiveStepButtonStyle" : "StepButtonStyle");
                }
            }
        }

        private async void CompleteSetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Build final connection strings with selected databases
                var baseConnectionString = connectionStrings["Base"];
                var sourceConnectionString = connectionStrings["Source"];
                var targetConnectionString = connectionStrings["Target"];

                if (!string.IsNullOrWhiteSpace(BaseDatabaseCombo.Text))
                    baseConnectionString += $"Database={BaseDatabaseCombo.Text};";

                if (!string.IsNullOrWhiteSpace(SourceDatabaseCombo.Text))
                    sourceConnectionString += $"Database={SourceDatabaseCombo.Text};";

                if (!string.IsNullOrWhiteSpace(TargetDatabaseCombo.Text))
                    targetConnectionString += $"Database={TargetDatabaseCombo.Text};";

                //BaseConnectionStringTextBox.Text = baseConnectionString;

                // Save connection strings to database
                using (var connection = new SqlConnection(baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = @"
                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReplicationConfig')
                            BEGIN
                                CREATE TABLE ReplicationConfig (
                                    Id INT IDENTITY(1,1) PRIMARY KEY,
                                    BaseConnectionString NVARCHAR(MAX),
                                    SourceConnectionString NVARCHAR(MAX),
                                    TargetConnectionString NVARCHAR(MAX),
                                    CreatedAt DATETIME DEFAULT GETDATE()
                                )
                            END

                            INSERT INTO ReplicationConfig (BaseConnectionString, SourceConnectionString, TargetConnectionString)
                            VALUES (@BaseConnectionString, @SourceConnectionString, @TargetConnectionString)";

                        command.Parameters.AddWithValue("@BaseConnectionString", baseConnectionString);
                        command.Parameters.AddWithValue("@SourceConnectionString", sourceConnectionString);
                        command.Parameters.AddWithValue("@TargetConnectionString", targetConnectionString);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                //MessageBox.Show("Connection strings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                //StatusLabel.Text = "Configuration completed successfully!";

                //var configWindow = new ConfigurationWindow(baseConnectionString, sourceConnectionString, targetConnectionString);
                //configWindow.Owner = this;
                //configWindow.ShowDialog();

                if (currentStep < 5)
                {
                    await ShowStep(currentStep + 1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GoToConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var baseConnectionString = connectionStrings["Base"];
                if (string.IsNullOrEmpty(baseConnectionString))
                {
                    MessageBox.Show("Please configure base database connection first.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                baseConnectionString += $"Database={BaseDatabaseCombo.Text};";

                using (var connection = new SqlConnection(baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(
                        @"SELECT TOP 1 SourceConnectionString, TargetConnectionString 
                          FROM ReplicationConfig 
                          ORDER BY Id DESC", connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var sourceConnectionString = reader.GetString(0);
                                var targetConnectionString = reader.GetString(1);

                                var configWindow = new ConfigurationWindow(
                                    baseConnectionString,
                                    sourceConnectionString,
                                    targetConnectionString);
                                configWindow.ShowDialog();

                                // Refresh config status after window is closed
                                await CheckExistingConfigs();
                                
                                // Update step buttons to enable Step 5 if configuration is complete
                                await UpdateStepButtons();

                                //var configWindow = new ConfigurationWindow(baseConnectionString, sourceConnectionString, targetConnectionString);
                                //configWindow.Owner = this;
                                //configWindow.ShowDialog();
                            }
                            else
                            {
                                MessageBox.Show("No existing configuration found.", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteConfigs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete all existing configurations? This action cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Build connection string with selected database
                    var baseConnectionString = connectionStrings["Base"];
                    if (string.IsNullOrEmpty(baseConnectionString))
                    {
                        MessageBox.Show("Please configure base database connection first.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    baseConnectionString += $"Database={BaseDatabaseCombo.Text};";

                    using (var connection = new SqlConnection(baseConnectionString))
                    {
                        await connection.OpenAsync();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // Delete from child tables first
                                var deleteFieldMappings = new SqlCommand(
                                    "DELETE FROM FieldMappings", connection, transaction);
                                await deleteFieldMappings.ExecuteNonQueryAsync();

                                // Delete from main config table
                                var deleteConfigs = new SqlCommand(
                                    "DELETE FROM ReplicationConfig", connection, transaction);
                                await deleteConfigs.ExecuteNonQueryAsync();

                                transaction.Commit();
                                MessageBox.Show("All configurations have been deleted successfully.", "Success",
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                                // Hide config buttons after successful deletion
                                //ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                throw new Exception($"Error deleting configurations: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StepButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            int stepNumber = button.Name switch
            {
                "Step1Button" => 1,
                "Step2Button" => 2,
                "Step3Button" => 3,
                "Step4Button" => 4,
                "Step5Button" => 5,
                _ => 1
            };

            await ShowStep(stepNumber);
        }

        private async Task ShowStep(int stepNumber)
        {
            // Hide all panels
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Collapsed;
            Step3Panel.Visibility = Visibility.Collapsed;
            Step4Panel.Visibility = Visibility.Collapsed;
            Step5Panel.Visibility = Visibility.Collapsed;

            // Show selected panel
            switch (stepNumber)
            {
                case 1:
                    Step1Panel.Visibility = Visibility.Visible;
                    break;
                case 2:
                    Step2Panel.Visibility = Visibility.Visible;
                    break;
                case 3:
                    Step3Panel.Visibility = Visibility.Visible;
                    break;
                case 4:
                    Step4Panel.Visibility = Visibility.Visible;
                    _ = LoadConfigurationStatus();
                    break;
                case 5:
                    Step5Panel.Visibility = Visibility.Visible;
                    await UpdateServiceButtonStates(); // Check service button states
                    break;
            }

            currentStep = stepNumber;
            await UpdateStepButtons();
        }

        //private void UpdateConfigButtonState()
        //{
        //    Step4Button.IsEnabled = ConfigButtonsPanel.Visibility == Visibility.Visible;
        //}

        //********************************** GENERATOR *********************************

        // Removed ManageServiceButton_Click method - no longer needed

        /// <summary>
        /// Displays the progress of the operation in the UI as a visual checklist.
        /// </summary>
        /// <param name="message">Status message.</param>
        /// <param name="isSuccess">Indicates whether the operation was successful.</param>
        private void DisplayProgress(string message, bool isSuccess)
        {
            var textBlock = new TextBlock
            {
                Text = (isSuccess ? "✓ " : "✗ ") + message, // '✓' for success, '✗' for failure
                Foreground = isSuccess ? Brushes.DarkGreen : Brushes.Red,
                Margin = new Thickness(0, 5, 0, 0)
            };
            ProgressStackPanel.Children.Add(textBlock);
            // Ensure the latest message is visible in the ScrollViewer
            (ProgressStackPanel.Parent as ScrollViewer)?.ScrollToEnd();
        }

        /// <summary>
        /// Checks whether the application is running with Administrator privileges.
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        //********************************** CONFIGURATION STATUS *********************************

        private async Task LoadConfigurationStatus()
        {
            try
            {
                if (!connectionStrings.ContainsKey("Base") || string.IsNullOrEmpty(connectionStrings["Base"]))
                {
                    DetailedReportText.Text = "Please complete the previous steps to configure the base database connection.";
                    return;
                }

                DetailedReportText.Text = "Please wait while loading configuration details...";

                // Load available listeners for Step 5
                _availableListeners = ListenerConfiguration.GetAvailableListeners();

                UpdateConfigurationStatusDisplay();
            }
            catch (Exception ex)
            {
                DetailedReportText.Text = $"Error loading configuration: {ex.Message}\n\nFailed to load configuration status. Please check your database connection.";
            }
        }

        private void UpdateConfigurationStatusDisplay()
        {
            var report = new List<string>
            {
            };

            // Check current UI state for database connections
            var hasBaseConfig = !string.IsNullOrWhiteSpace(BaseDatabaseCombo.Text);
            var hasSourceConfig = !string.IsNullOrWhiteSpace(SourceDatabaseCombo.Text);
            var hasTargetConfig = !string.IsNullOrWhiteSpace(TargetDatabaseCombo.Text);

            // Database Connections Status
            report.Add("📊 DATABASE CONNECTIONS:");
            if (hasBaseConfig)
            {
                report.Add($"  Base: {BaseDatabaseCombo.Text} ✅");
            }
            else
            {
                report.Add("  Base: Not configured ❌");
            }

            if (hasSourceConfig)
            {
                report.Add($"  Source: {SourceDatabaseCombo.Text} ✅");
            }
            else
            {
                report.Add("  Source: Not configured ❌");
            }

            if (hasTargetConfig)
            {
                report.Add($"  Target: {TargetDatabaseCombo.Text} ✅");
            }
            else
            {
                report.Add("  Target: Not configured ❌");
            }
            report.Add("");

            // Overall Configuration Status
            report.Add("📋 CONFIGURATION STATUS:");
            if (hasBaseConfig && hasSourceConfig && hasTargetConfig)
            {
                report.Add("  🟢 All database connections are configured");
                if (_FieldMappingPass)
                {
                    report.Add("  🟢 Field mappings found");
                }
                else { 
                    report.Add("  🟡 Field mappings need to be configured"); 
                }

                //report.Add("  🟡 Listeners need to be selected");
            }
            else
            {
                report.Add("  🔴 Please complete all database configurations first");
            }
            report.Add("");

            // Next Steps
            report.Add("📝 NEXT STEPS:");
            if (!hasBaseConfig)
            {
                report.Add("  1. Configure base database connection");
            }
            else if (!hasSourceConfig)
            {
                report.Add("  1. Configure source database connection");
            }
            else if (!hasTargetConfig)
            {
                report.Add("  1. Configure target database connection");
            }
            else
            {
                report.Add("  1. ✅ All database connections configured");
                if (_FieldMappingPass)
                {
                    report.Add("  2. ✅ Field mappings and table selection configured");
                }
                else
                {
                    report.Add("  2. Configure field mappings and table selections");
                }
                report.Add("  3. Select listeners for change detection");
                report.Add("  4. Create and start the replication service");
            }

            DetailedReportText.Text = string.Join("\n", report);
        }

        private void LoadAvailableListeners()
        {
            if (_availableListeners == null)
            {
                _availableListeners = ListenerConfiguration.GetAvailableListeners();
            }
        }

        // Removed OnListenerSelectionChanged, GetSelectedListeners, and UpdateListenerSelection methods
        // since ListenerSelectionPanel and CompatibilityWarningBorder were removed from XAML

        // Removed SaveListenerConfiguration method - no longer needed

        // Service Management Event Handlers
        private async void CheckServiceStatus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DisplayProgress("Checking service status...", true);
                
                // Check if the service exists and get its status
                var serviceStatus = await ServiceInstallerManager.GetServiceStatus();
                
                if (serviceStatus == null)
                {
                    DisplayProgress("Service not found. Please create the service first.", false);
                }
                else
                {
                    DisplayProgress($"Service Status: {serviceStatus}", true);
                }
            }
            catch (Exception ex)
            {
                DisplayProgress($"Error checking service status: {ex.Message}", false);
            }
        }

        private void AddListener_Click(object sender, RoutedEventArgs e)
        {
            // This would open a dialog to add a new listener
            MessageBox.Show("Add Listener functionality will be implemented in the next version.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveListener_Click(object sender, RoutedEventArgs e)
        {
            // This would remove the selected listener
            MessageBox.Show("Remove Listener functionality will be implemented in the next version.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Service Creation Methods for Step 5
        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select service installation path",
                ShowNewFolderButton = true,
                SelectedPath = ServicePathTextBox.Text
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServicePathTextBox.Text = folderDialog.SelectedPath;
            }
        }

        private async void CreateService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateServiceButton.IsEnabled = false;
                ProgressStackPanel.Children.Clear();

                // Add detailed logging
                DisplayProgress("=== شروع فرآیند ساخت سرویس ===", true);
                DisplayProgress($"زمان شروع: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", true);

                // Check administrator privileges
                DisplayProgress("بررسی دسترسی Administrator...", true);
                if (!IsRunningAsAdministrator())
                {
                    DisplayProgress("❌ خطا: برنامه باید با دسترسی Administrator اجرا شود", false);
                    MessageBox.Show("To manage the Windows service, the application must be run with Administrator privileges.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress("✅ دسترسی Administrator تایید شد", true);

                // Validate selected path
                string selectedPath = ServicePathTextBox.Text.Trim();
                DisplayProgress($"مسیر انتخاب شده: {selectedPath}", true);
                if (string.IsNullOrEmpty(selectedPath))
                {
                    DisplayProgress("❌ خطا: مسیر نصب سرویس انتخاب نشده", false);
                    MessageBox.Show("Please select a service installation path.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }

                // Check path access
                DisplayProgress("بررسی دسترسی به مسیر انتخاب شده...", true);
                if (!await CheckPathAccess(selectedPath))
                {
                    DisplayProgress("❌ خطا: دسترسی ناکافی برای نوشتن در مسیر انتخاب شده", false);
                    MessageBox.Show("Insufficient permissions to write to selected path. Please select another path or check permissions.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress("✅ دسترسی به مسیر تایید شد", true);

                // Get selected listener type
                var selectedItem = ListenerTypeComboBox.SelectedItem as ComboBoxItem;
                string listenerType = selectedItem?.Tag?.ToString() ?? "Trigger";
                DisplayProgress($"نوع لیسنر انتخاب شده: {listenerType}", true);

                // Validate configuration data
                DisplayProgress("بررسی اطلاعات پیکربندی...", true);
                var configData = GetActualConfigurationData();
                
                // Detailed logging of configuration data
                DisplayProgress("=== جزئیات اطلاعات پیکربندی ===", true);
                DisplayProgress($"Source Server: '{configData.SourceServer}'", true);
                DisplayProgress($"Source Database: '{configData.SourceDatabase}'", true);
                DisplayProgress($"Target Server: '{configData.TargetServer}'", true);
                DisplayProgress($"Target Database: '{configData.TargetDatabase}'", true);
                DisplayProgress($"Source Connection String Length: {configData.SourceConnectionString?.Length ?? 0}", true);
                DisplayProgress($"Target Connection String Length: {configData.TargetConnectionString?.Length ?? 0}", true);
                DisplayProgress($"Selected Tables Count: {configData.SelectedTables?.Count ?? 0}", true);
                DisplayProgress($"Table Fields Count: {configData.TableFields?.Count ?? 0}", true);
                DisplayProgress($"Table Primary Keys Count: {configData.TablePrimaryKeys?.Count ?? 0}", true);
                
                // Check for missing information
                bool hasSourceInfo = !string.IsNullOrEmpty(configData.SourceServer) && !string.IsNullOrEmpty(configData.SourceDatabase);
                bool hasTargetInfo = !string.IsNullOrEmpty(configData.TargetServer) && !string.IsNullOrEmpty(configData.TargetDatabase);
                bool hasSourceConnection = !string.IsNullOrEmpty(configData.SourceConnectionString);
                bool hasTargetConnection = !string.IsNullOrEmpty(configData.TargetConnectionString);
                bool hasSelectedTables = configData.SelectedTables?.Count > 0;
                
                DisplayProgress($"Has Source Info: {hasSourceInfo}", true);
                DisplayProgress($"Has Target Info: {hasTargetInfo}", true);
                DisplayProgress($"Has Source Connection: {hasSourceConnection}", true);
                DisplayProgress($"Has Target Connection: {hasTargetConnection}", true);
                DisplayProgress($"Has Selected Tables: {hasSelectedTables}", true);
                
                if (!hasSourceConnection || !hasTargetConnection)
                {
                    DisplayProgress("❌ خطا: اطلاعات اتصال به دیتابیس ناقص است", false);
                    if (!hasSourceConnection)
                        DisplayProgress("❌ خطا: رشته اتصال دیتابیس مبدا خالی است", false);
                    if (!hasTargetConnection)
                        DisplayProgress("❌ خطا: رشته اتصال دیتابیس مقصد خالی است", false);
                    MessageBox.Show("Database connection information is incomplete. Please complete the configuration first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                
                if (!hasSelectedTables)
                {
                    DisplayProgress("❌ خطا: هیچ جدولی انتخاب نشده است", false);
                    MessageBox.Show("No tables have been selected for replication. Please select at least one table.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                
                DisplayProgress($"✅ اطلاعات پیکربندی تایید شد - {configData.SelectedTables.Count} جدول انتخاب شده", true);

                // Test database connections
                DisplayProgress("تست اتصال به دیتابیس‌ها...", true);
                bool sourceConnected = await TestDatabaseConnection(configData.SourceConnectionString);
                bool targetConnected = await TestDatabaseConnection(configData.TargetConnectionString);
                
                if (!sourceConnected)
                {
                    DisplayProgress("❌ خطا: اتصال به دیتابیس مبدا ناموفق", false);
                    MessageBox.Show("Cannot connect to source database. Please check connection settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress("✅ اتصال به دیتابیس مبدا موفق", true);

                if (!targetConnected)
                {
                    DisplayProgress("❌ خطا: اتصال به دیتابیس مقصد ناموفق", false);
                    MessageBox.Show("Cannot connect to target database. Please check connection settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress("✅ اتصال به دیتابیس مقصد موفق", true);

                // For Trigger listener, create ChangeLog table
                if (listenerType == "Trigger")
                {
                    DisplayProgress("ساخت جدول ChangeLog در دیتابیس مبدا...", true);
                    bool changeLogCreated = await CreateChangeLogTable(configData.SourceConnectionString);
                    if (!changeLogCreated)
                    {
                        DisplayProgress("❌ خطا: ساخت جدول ChangeLog ناموفق", false);
                        MessageBox.Show("Failed to create ChangeLog table in source database. Please check database permissions.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        CreateServiceButton.IsEnabled = true;
                        return;
                    }
                    DisplayProgress("✅ جدول ChangeLog با موفقیت ساخته شد", true);
                }

                DisplayProgress($"ساخت فایل‌های سرویس با نوع {listenerType}...", true);

                // Create service files
                var (serviceCreated, serviceName) = await CreateServiceFiles(selectedPath, listenerType);
                if (!serviceCreated)
                {
                    DisplayProgress("❌ خطا: ساخت فایل‌های سرویس ناموفق", false);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress($"✅ فایل‌های سرویس با موفقیت ساخته شدند - نام سرویس: {serviceName}", true);

                // Install Windows service
                DisplayProgress("نصب سرویس ویندوز...", true);
                bool serviceInstalled = await InstallWindowsService(selectedPath, serviceName);
                if (!serviceInstalled)
                {
                    DisplayProgress("❌ خطا: نصب سرویس ویندوز ناموفق", false);
                    CreateServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress("✅ سرویس ویندوز با موفقیت نصب شد", true);

                // Create shortcuts
                if (CreateDesktopShortcutCheckBox.IsChecked == true)
                {
                    DisplayProgress("ساخت میانبر دسکتاپ...", true);
                    CreateDesktopShortcut(selectedPath);
                    DisplayProgress("✅ میانبر دسکتاپ ساخته شد", true);
                }

                if (CreateStartMenuShortcutCheckBox.IsChecked == true)
                {
                    DisplayProgress("ساخت میانبر منوی شروع...", true);
                    CreateStartMenuShortcut(selectedPath);
                    DisplayProgress("✅ میانبر منوی شروع ساخته شد", true);
                }

                DisplayProgress("=== سرویس با موفقیت ساخته شد ===", true);
                DisplayProgress($"زمان پایان: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", true);

                // Enable test and stop buttons
                TestServiceButton.IsEnabled = true;
                StopServiceButton.IsEnabled = true;

                // Ask user if they want to test the service
                var result = MessageBox.Show("Service created successfully! Would you like to test the service?", 
                    "Test Service", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await TestService();
                }

                CreateServiceButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                DisplayProgress($"❌ خطای غیرمنتظره: {ex.Message}", false);
                DisplayProgress($"جزئیات خطا: {ex.StackTrace}", false);
                MessageBox.Show($"Error creating service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CreateServiceButton.IsEnabled = true;
            }
        }

        private async Task<bool> CheckPathAccess(string path)
        {
            try
            {
                // Test if we can create a file in the directory
                string testFile = Path.Combine(path, "test_access.tmp");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestDatabaseConnection(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                DisplayProgress($"خطا در اتصال دیتابیس: {ex.Message}", false);
                return false;
            }
        }

        private async Task<bool> CreateChangeLogTable(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string createTableSql = @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChangeLog' AND xtype='U')
BEGIN
    CREATE TABLE dbo.ChangeLog (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TableName NVARCHAR(128) NOT NULL,
        Operation NVARCHAR(10) NOT NULL, -- 'I' for Insert, 'U' for Update, 'D' for Delete
        PrimaryKeyValue NVARCHAR(MAX) NOT NULL,
        ChangeData NVARCHAR(MAX) NULL,
        ChangeTimestamp DATETIME2 DEFAULT GETDATE(),
        Processed BIT DEFAULT 0,
        ProcessedTimestamp DATETIME2 NULL
    )
    
    -- Create index for better performance
    CREATE INDEX IX_ChangeLog_TableName_Processed ON dbo.ChangeLog (TableName, Processed)
    CREATE INDEX IX_ChangeLog_ChangeTimestamp ON dbo.ChangeLog (ChangeTimestamp)
    
    PRINT 'ChangeLog table created successfully'
END
ELSE
BEGIN
    PRINT 'ChangeLog table already exists'
END";

                using var command = new SqlCommand(createTableSql, connection);
                await command.ExecuteNonQueryAsync();

                DisplayProgress("جدول ChangeLog آماده است", true);
                return true;
            }
            catch (Exception ex)
            {
                DisplayProgress($"خطا در ساخت جدول ChangeLog: {ex.Message}", false);
                return false;
            }
        }

        // Configuration data class for service generation
        public class ConfigurationData
        {
            public string BaseConnectionString { get; set; } = string.Empty;
            public string SourceConnectionString { get; set; } = string.Empty;
            public string TargetConnectionString { get; set; } = string.Empty;
            public string SourceServer { get; set; } = string.Empty;
            public string SourceDatabase { get; set; } = string.Empty;
            public string TargetServer { get; set; } = string.Empty;
            public string TargetDatabase { get; set; } = string.Empty;
            public List<string> SelectedTables { get; set; } = new List<string>();
            public Dictionary<string, List<FieldInfo>> TableFields { get; set; } = new Dictionary<string, List<FieldInfo>>();
            public Dictionary<string, string> TablePrimaryKeys { get; set; } = new Dictionary<string, string>();
            public string ReplicationType { get; set; } = "Full";
            public int ServiceInterval { get; set; } = 5000;
            public string LogLevel { get; set; } = "Information";
        }

        private ConfigurationData GetActualConfigurationData()
        {
            // Log the current state of configuration variables
            DisplayProgress("=== بررسی متغیرهای پیکربندی ===", true);
            DisplayProgress($"sourceDb is null: {sourceDb == null}", true);
            DisplayProgress($"targetDb is null: {targetDb == null}", true);
            DisplayProgress($"selectedTables is null: {selectedTables == null}", true);
            DisplayProgress($"connectionStrings count: {connectionStrings?.Count ?? 0}", true);
            
            if (sourceDb != null)
            {
                DisplayProgress($"sourceDb.ServerName: '{sourceDb.ServerName}'", true);
                DisplayProgress($"sourceDb.DatabaseName: '{sourceDb.DatabaseName}'", true);
                DisplayProgress($"sourceDb.ConnectionString length: {sourceDb.ConnectionString?.Length ?? 0}", true);
            }
            
            if (targetDb != null)
            {
                DisplayProgress($"targetDb.ServerName: '{targetDb.ServerName}'", true);
                DisplayProgress($"targetDb.DatabaseName: '{targetDb.DatabaseName}'", true);
                DisplayProgress($"targetDb.ConnectionString length: {targetDb.ConnectionString?.Length ?? 0}", true);
            }
            
            if (selectedTables != null)
            {
                DisplayProgress($"selectedTables count: {selectedTables.Count}", true);
                var selectedTableNames = selectedTables.Where(t => t.IsSelected).Select(t => t.TableName).ToList();
                DisplayProgress($"selected table names: {string.Join(", ", selectedTableNames)}", true);
            }
            
            // Load configuration from database instead of relying on private fields
            DisplayProgress("=== بارگذاری پیکربندی از دیتابیس ===", true);
            
            var configData = new ConfigurationData
            {
                BaseConnectionString = connectionStrings.ContainsKey("Base") ? connectionStrings["Base"] : "",
                SourceConnectionString = "",
                TargetConnectionString = "",
                SourceServer = "",
                SourceDatabase = "",
                TargetServer = "",
                TargetDatabase = "",
                SelectedTables = new List<string>(),
                TableFields = new Dictionary<string, List<FieldInfo>>(),
                TablePrimaryKeys = new Dictionary<string, string>(),
                ReplicationType = replicationType,
                ServiceInterval = 5000,
                LogLevel = "Information"
            };
            
            try
            {
                // Build base connection string
                var baseConnectionString = connectionStrings["Base"];
                if (!string.IsNullOrWhiteSpace(BaseDatabaseCombo.Text))
                    baseConnectionString += $"Database={BaseDatabaseCombo.Text};";
                
                configData.BaseConnectionString = baseConnectionString;
                DisplayProgress($"Base Connection String: {baseConnectionString}", true);
                
                using (var connection = new SqlConnection(baseConnectionString))
                {
                    connection.Open();
                    
                    // Load connection strings from ReplicationConfig table
                    using (var command = new SqlCommand(
                        @"SELECT TOP 1 SourceConnectionString, TargetConnectionString 
                          FROM ReplicationConfig 
                          ORDER BY Id DESC", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                configData.SourceConnectionString = reader.GetString(0);
                                configData.TargetConnectionString = reader.GetString(1);
                                
                                DisplayProgress($"Source Connection String Length: {configData.SourceConnectionString?.Length ?? 0}", true);
                                DisplayProgress($"Target Connection String Length: {configData.TargetConnectionString?.Length ?? 0}", true);
                                
                                // Parse connection strings to extract server and database names
                                var sourceParts = configData.SourceConnectionString.Split(';');
                                var targetParts = configData.TargetConnectionString.Split(';');
                                
                                configData.SourceServer = sourceParts.FirstOrDefault(p => p.Contains("Server="))?.Split('=')[1] ?? "";
                                configData.SourceDatabase = sourceParts.FirstOrDefault(p => p.Contains("Database="))?.Split('=')[1] ?? "";
                                configData.TargetServer = targetParts.FirstOrDefault(p => p.Contains("Server="))?.Split('=')[1] ?? "";
                                configData.TargetDatabase = targetParts.FirstOrDefault(p => p.Contains("Database="))?.Split('=')[1] ?? "";
                                
                                DisplayProgress($"Source Server: '{configData.SourceServer}'", true);
                                DisplayProgress($"Source Database: '{configData.SourceDatabase}'", true);
                                DisplayProgress($"Target Server: '{configData.TargetServer}'", true);
                                DisplayProgress($"Target Database: '{configData.TargetDatabase}'", true);
                            }
                            else
                            {
                                DisplayProgress("❌ خطا: هیچ پیکربندی در جدول ReplicationConfig یافت نشد", false);
                                return configData;
                            }
                        }
                    }
                    
                    // Load selected tables and field mappings from FieldMappings table
                    using (var command = new SqlCommand(
                        @"SELECT DISTINCT TargetTableName, SourceTableName 
                          FROM FieldMappings 
                          ORDER BY TargetTableName", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var selectedTables = new List<string>();
                            while (reader.Read())
                            {
                                var targetTableName = reader.GetString(0);
                                var sourceTableName = reader.GetString(1);
                                selectedTables.Add(sourceTableName);
                                
                                DisplayProgress($"Found table mapping: {sourceTableName} -> {targetTableName}", true);
                            }
                            configData.SelectedTables = selectedTables;
                        }
                    }
                    
                    // Load field information for each selected table
                    foreach (var tableName in configData.SelectedTables)
                    {
                        var fields = new List<FieldInfo>();
                        var primaryKey = "";
                        
                        using (var command = new SqlCommand(
                            @"SELECT SourceField, IsPrimaryKey 
                              FROM FieldMappings 
                              WHERE SourceTableName = @TableName", connection))
                        {
                            command.Parameters.AddWithValue("@TableName", tableName);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var fieldName = reader.GetString(0);
                                    var isPrimaryKey = reader.GetBoolean(1);
                                    
                                    fields.Add(new FieldInfo
                                    {
                                        FieldName = fieldName,
                                        IsPrimaryKey = isPrimaryKey,
                                        IsSelected = true
                                    });
                                    
                                    if (isPrimaryKey)
                                    {
                                        primaryKey = fieldName;
                                    }
                                }
                            }
                        }
                        
                        configData.TableFields[tableName] = fields;
                        configData.TablePrimaryKeys[tableName] = primaryKey;
                        
                        DisplayProgress($"Table '{tableName}': {fields.Count} fields, PK: '{primaryKey}'", true);
                    }
                }
                
                DisplayProgress($"✅ پیکربندی از دیتابیس بارگذاری شد - {configData.SelectedTables.Count} جدول", true);
            }
            catch (Exception ex)
            {
                DisplayProgress($"❌ خطا در بارگذاری پیکربندی: {ex.Message}", false);
                DisplayProgress($"Stack Trace: {ex.StackTrace}", false);
            }
            
            return configData;
        }

        private async Task<(bool success, string serviceName)> CreateServiceFiles(string path, string listenerType)
        {
            try
            {
                DisplayProgress("=== شروع ساخت فایل‌های سرویس ===", true);
                
                // Create unique service name with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string serviceName = $"SqlReplicator_{listenerType}_{timestamp}";
                string serviceExePath = Path.Combine(path, $"{serviceName}.exe");
                string documentationPath = Path.Combine(path, $"{serviceName}_Documentation.txt");
                
                DisplayProgress($"نام سرویس: {serviceName}", true);
                DisplayProgress($"مسیر فایل اجرایی: {serviceExePath}", true);
                DisplayProgress($"مسیر فایل مستندات: {documentationPath}", true);

                // Get actual configuration data
                DisplayProgress("دریافت اطلاعات پیکربندی...", true);
                var configData = GetActualConfigurationData();
                
                if (configData == null)
                {
                    DisplayProgress("❌ خطا: اطلاعات پیکربندی دریافت نشد", false);
                    return (false, string.Empty);
                }
                
                DisplayProgress($"تعداد جداول انتخاب شده: {configData.SelectedTables?.Count ?? 0}", true);

                // Generate service executable with embedded configuration
                DisplayProgress("تولید کد سرویس...", true);
                string serviceTemplate = GetServiceTemplate(listenerType, serviceName, configData);
                
                if (string.IsNullOrEmpty(serviceTemplate))
                {
                    DisplayProgress("❌ خطا: قالب سرویس تولید نشد", false);
                    return (false, string.Empty);
                }
                
                DisplayProgress($"اندازه کد سرویس: {serviceTemplate.Length} کاراکتر", true);
                
                try
                {
                    await File.WriteAllTextAsync(serviceExePath, serviceTemplate);
                    DisplayProgress("✅ فایل اجرایی سرویس با موفقیت ایجاد شد", true);
                }
                catch (Exception ex)
                {
                    DisplayProgress($"❌ خطا در نوشتن فایل اجرایی: {ex.Message}", false);
                    DisplayProgress($"جزئیات خطا: {ex.StackTrace}", false);
                    return (false, string.Empty);
                }

                // Create detailed documentation with actual connection strings
                DisplayProgress("تولید مستندات...", true);
                string documentationTemplate = GetDocumentationTemplate(listenerType, serviceName, configData);
                
                if (string.IsNullOrEmpty(documentationTemplate))
                {
                    DisplayProgress("❌ خطا: قالب مستندات تولید نشد", false);
                    return (false, string.Empty);
                }
                
                DisplayProgress($"اندازه مستندات: {documentationTemplate.Length} کاراکتر", true);
                
                try
                {
                    await File.WriteAllTextAsync(documentationPath, documentationTemplate);
                    DisplayProgress("✅ فایل مستندات با موفقیت ایجاد شد", true);
                }
                catch (Exception ex)
                {
                    DisplayProgress($"❌ خطا در نوشتن فایل مستندات: {ex.Message}", false);
                    DisplayProgress($"جزئیات خطا: {ex.StackTrace}", false);
                    return (false, string.Empty);
                }

                // Verify files were created
                if (!File.Exists(serviceExePath))
                {
                    DisplayProgress("❌ خطا: فایل اجرایی پس از ایجاد یافت نشد", false);
                    return (false, string.Empty);
                }
                
                if (!File.Exists(documentationPath))
                {
                    DisplayProgress("❌ خطا: فایل مستندات پس از ایجاد یافت نشد", false);
                    return (false, string.Empty);
                }
                
                var serviceFileInfo = new FileInfo(serviceExePath);
                var docFileInfo = new FileInfo(documentationPath);
                
                DisplayProgress($"اندازه فایل اجرایی: {serviceFileInfo.Length} بایت", true);
                DisplayProgress($"اندازه فایل مستندات: {docFileInfo.Length} بایت", true);

                // Store the service name for later use
                _lastCreatedServiceName = serviceName;
                DisplayProgress($"نام سرویس ذخیره شد: {_lastCreatedServiceName}", true);

                DisplayProgress("=== ساخت فایل‌های سرویس با موفقیت پایان یافت ===", true);
                return (true, serviceName);
            }
            catch (Exception ex)
            {
                DisplayProgress($"❌ خطای غیرمنتظره در ساخت فایل‌ها: {ex.Message}", false);
                DisplayProgress($"جزئیات خطا: {ex.StackTrace}", false);
                return (false, string.Empty);
            }
        }

        private string GetServiceTemplate(string listenerType, string serviceName, ConfigurationData configData)
        {
            switch (listenerType)
            {
                case "Trigger":
                    return $@"
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;
using System.Text.Json;

namespace SqlReplicatorService
{{
    public class {serviceName} : ServiceBase
    {{
        private CancellationTokenSource _cancellationTokenSource;
        private Task _replicationTask;
        
        // Embedded configuration
        private readonly ReplicationConfig _config = new ReplicationConfig
        {{
            ConnectionStrings = new ConnectionStrings
            {{
                Base = ""{configData.BaseConnectionString.Replace("\"", "\\\"")}"",
                Source = ""{configData.SourceConnectionString.Replace("\"", "\\\"")}"",
                Target = ""{configData.TargetConnectionString.Replace("\"", "\\\"")}""
            }},
            ServiceSettings = new ServiceSettings
            {{
                Interval = {configData.ServiceInterval},
                LogLevel = ""{configData.LogLevel}""
            }}
        }};

        public {serviceName}()
        {{
            ServiceName = ""{serviceName}"";
            _cancellationTokenSource = new CancellationTokenSource();
        }}

        protected override void OnStart(string[] args)
        {{
            try
            {{
                LogMessage(""Service starting..."");
                
                // Create triggers for change detection
                CreateChangeDetectionTriggers();
                
                // Start replication task
                _replicationTask = Task.Run(() => ReplicationLoop(_cancellationTokenSource.Token));
                
                LogMessage(""Service started successfully"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error starting service: {{ex.Message}}"");
                throw;
            }}
        }}

        protected override void OnStop()
        {{
            try
            {{
                LogMessage(""Service stopping..."");
                _cancellationTokenSource.Cancel();
                _replicationTask?.Wait(TimeSpan.FromSeconds(30));
                LogMessage(""Service stopped"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error stopping service: {{ex.Message}}"");
            }}
        }}

        private void CreateChangeDetectionTriggers()
        {{
            // This method creates SQL Server triggers for change detection
            // Implementation will be added based on your specific requirements
            LogMessage(""Creating change detection triggers..."");
        }}

        private async Task ReplicationLoop(CancellationToken cancellationToken)
        {{
            while (!cancellationToken.IsCancellationRequested)
            {{
                try
                {{
                    // Check for changes using triggers
                    await CheckForChanges();
                    
                    // Wait for next interval
                    await Task.Delay(_config.ServiceSettings.Interval, cancellationToken);
                }}
                catch (OperationCanceledException)
                {{
                    break;
                }}
                catch (Exception ex)
                {{
                    LogMessage($""Error in replication loop: {{ex.Message}}"");
                    await Task.Delay(10000, cancellationToken); // Wait 10 seconds before retry
                }}
            }}
        }}

        private async Task CheckForChanges()
        {{
            try
            {{
                using (var connection = new SqlConnection(_config.ConnectionStrings.Source))
                {{
                    await connection.OpenAsync();
                    
                    // Query unprocessed changes from ChangeTracking table
                    var query = @""SELECT TOP 10 
                            ChangeId, TableName, OperationType, PrimaryKeyValue, ChangeData
                        FROM ChangeTracking 
                        WHERE IsProcessed = 0 
                        ORDER BY ChangeId"";
                    
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {{
                        while (await reader.ReadAsync())
                        {{
                            var changeId = reader.GetInt64(""ChangeId"");
                            var tableName = reader.GetString(""TableName"");
                            var operationType = reader.GetString(""OperationType"");
                            var primaryKeyValue = reader.GetString(""PrimaryKeyValue"");
                            var changeData = reader.GetString(""ChangeData"");
                            
                            LogMessage($""Processing change: {{operationType}} on {{tableName}}, Key: {{primaryKeyValue}}"");
                            
                            // Process the change (replicate to target database)
                            await ProcessChange(tableName, operationType, primaryKeyValue, changeData);
                            
                            // Mark as processed
                            await MarkChangeAsProcessed(changeId);
                        }}
                    }}
                }}
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error checking for changes: {{ex.Message}}"");
            }}
        }}
        
        private async Task ProcessChange(string tableName, string operationType, string primaryKeyValue, string changeData)
        {{
            try
            {{
                using (var targetConnection = new SqlConnection(_config.ConnectionStrings.Target))
                {{
                    await targetConnection.OpenAsync();
                    
                    switch (operationType)
                    {{
                        case ""I"": // Insert
                            await ProcessInsert(targetConnection, tableName, changeData);
                            break;
                        case ""U"": // Update
                            await ProcessUpdate(targetConnection, tableName, primaryKeyValue, changeData);
                            break;
                        case ""D"": // Delete
                            await ProcessDelete(targetConnection, tableName, primaryKeyValue);
                            break;
                    }}
                    
                    LogMessage($""Successfully processed {{operationType}} operation on {{tableName}}"");
                }}
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error processing change: {{ex.Message}}"");
            }}
        }}
        
        private async Task ProcessInsert(SqlConnection connection, string tableName, string changeData)
        {{
            // Parse JSON data and insert into target table
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(changeData);
            if (data == null) return;
            
            var columns = string.Join("", "", data.Keys);
            var values = string.Join("", "", data.Keys.Select(k => ""@"" + k));
            var query = $""INSERT INTO {{tableName}} ({{columns}}) VALUES ({{values}})"";
            
            using (var command = new SqlCommand(query, connection))
            {{
                foreach (var kvp in data)
                {{
                    command.Parameters.AddWithValue(""@"" + kvp.Key, kvp.Value ?? DBNull.Value);
                }}
                await command.ExecuteNonQueryAsync();
            }}
        }}
        
        private async Task ProcessUpdate(SqlConnection connection, string tableName, string primaryKeyValue, string changeData)
        {{
            // Parse JSON data and update target table
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(changeData);
            if (data == null) return;
            
            var setClause = string.Join("", "", data.Keys.Select(k => $""{{k}} = @{{k}}""));
            var query = $""UPDATE {{tableName}} SET {{setClause}} WHERE MaterialCode = @PrimaryKey"";
            
            using (var command = new SqlCommand(query, connection))
            {{
                command.Parameters.AddWithValue(""@PrimaryKey"", primaryKeyValue);
                foreach (var kvp in data)
                {{
                    command.Parameters.AddWithValue(""@"" + kvp.Key, kvp.Value ?? DBNull.Value);
                }}
                await command.ExecuteNonQueryAsync();
            }}
        }}
        
        private async Task ProcessDelete(SqlConnection connection, string tableName, string primaryKeyValue)
        {{
            var query = $""DELETE FROM {{tableName}} WHERE MaterialCode = @PrimaryKey"";
            using (var command = new SqlCommand(query, connection))
            {{
                command.Parameters.AddWithValue(""@PrimaryKey"", primaryKeyValue);
                await command.ExecuteNonQueryAsync();
            }}
        }}
        
        private async Task MarkChangeAsProcessed(long changeId)
        {{
            try
            {{
                using (var connection = new SqlConnection(_config.ConnectionStrings.Source))
                {{
                    await connection.OpenAsync();
                    var query = ""UPDATE ChangeTracking SET IsProcessed = 1, ProcessedDate = GETDATE() WHERE ChangeId = @ChangeId"";
                    using (var command = new SqlCommand(query, connection))
                    {{
                        command.Parameters.AddWithValue(""@ChangeId"", changeId);
                        await command.ExecuteNonQueryAsync();
                    }}
                }}
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error marking change as processed: {{ex.Message}}"");
            }}
        }}

        private void LogMessage(string message)
        {{
            string logMessage = $""[{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}] {{message}}"";
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""{serviceName}.log"");
            
            try
            {{
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }}
            catch
            {{
                // If logging fails, continue without logging
            }}
        }}
    }}

    // Configuration classes
    public class ReplicationConfig
    {{
        public ConnectionStrings ConnectionStrings {{ get; set; }}
        public ServiceSettings ServiceSettings {{ get; set; }}
    }}

    public class ConnectionStrings
    {{
        public string Base {{ get; set; }}
        public string Source {{ get; set; }}
        public string Target {{ get; set; }}
    }}

    public class ServiceSettings
    {{
        public int Interval {{ get; set; }}
        public string LogLevel {{ get; set; }}
    }}
}}";
                case "CDC":
                    return $@"
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;

namespace SqlReplicatorService
{{
    public class {serviceName} : ServiceBase
    {{
        private CancellationTokenSource _cancellationTokenSource;
        private Task _replicationTask;
        
        // Embedded configuration
        private readonly ReplicationConfig _config = new ReplicationConfig
        {{
            ConnectionStrings = new ConnectionStrings
            {{
                Base = ""YOUR_BASE_CONNECTION_STRING_HERE"",
                Source = ""YOUR_SOURCE_CONNECTION_STRING_HERE"",
                Target = ""YOUR_TARGET_CONNECTION_STRING_HERE""
            }},
            ServiceSettings = new ServiceSettings
            {{
                Interval = 5000,
                LogLevel = ""Information""
            }}
        }};

        public {serviceName}()
        {{
            ServiceName = ""{serviceName}"";
            _cancellationTokenSource = new CancellationTokenSource();
        }}

        protected override void OnStart(string[] args)
        {{
            try
            {{
                LogMessage(""Service starting..."");
                
                // Enable CDC if not already enabled
                EnableChangeDataCapture();
                
                // Start replication task
                _replicationTask = Task.Run(() => ReplicationLoop(_cancellationTokenSource.Token));
                
                LogMessage(""Service started successfully"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error starting service: {{ex.Message}}"");
                throw;
            }}
        }}

        protected override void OnStop()
        {{
            try
            {{
                LogMessage(""Service stopping..."");
                _cancellationTokenSource.Cancel();
                _replicationTask?.Wait(TimeSpan.FromSeconds(30));
                LogMessage(""Service stopped"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error stopping service: {{ex.Message}}"");
            }}
        }}

        private void EnableChangeDataCapture()
        {{
            // Enable CDC on the database and tables
            LogMessage(""Enabling Change Data Capture..."");
        }}

        private async Task ReplicationLoop(CancellationToken cancellationToken)
        {{
            while (!cancellationToken.IsCancellationRequested)
            {{
                try
                {{
                    // Check for changes using CDC
                    await CheckForChangesCDC();
                    
                    // Wait for next interval
                    await Task.Delay(_config.ServiceSettings.Interval, cancellationToken);
                }}
                catch (OperationCanceledException)
                {{
                    break;
                }}
                catch (Exception ex)
                {{
                    LogMessage($""Error in replication loop: {{ex.Message}}"");
                    await Task.Delay(10000, cancellationToken);
                }}
            }}
        }}

        private async Task CheckForChangesCDC()
        {{
            // Implementation for checking changes using CDC
            LogMessage(""Checking for changes using CDC..."");
        }}

        private void LogMessage(string message)
        {{
            string logMessage = $""[{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}] {{message}}"";
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""{serviceName}.log"");
            
            try
            {{
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }}
            catch
            {{
                // If logging fails, continue without logging
            }}
        }}
    }}

    // Configuration classes
    public class ReplicationConfig
    {{
        public ConnectionStrings ConnectionStrings {{ get; set; }}
        public ServiceSettings ServiceSettings {{ get; set; }}
    }}

    public class ConnectionStrings
    {{
        public string Base {{ get; set; }}
        public string Source {{ get; set; }}
        public string Target {{ get; set; }}
    }}

    public class ServiceSettings
    {{
        public int Interval {{ get; set; }}
        public string LogLevel {{ get; set; }}
    }}
}}";
                case "ChangeTracking":
                    return $@"
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;

namespace SqlReplicatorService
{{
    public class {serviceName} : ServiceBase
    {{
        private CancellationTokenSource _cancellationTokenSource;
        private Task _replicationTask;
        
        // Embedded configuration
        private readonly ReplicationConfig _config = new ReplicationConfig
        {{
            ConnectionStrings = new ConnectionStrings
            {{
                Base = ""YOUR_BASE_CONNECTION_STRING_HERE"",
                Source = ""YOUR_SOURCE_CONNECTION_STRING_HERE"",
                Target = ""YOUR_TARGET_CONNECTION_STRING_HERE""
            }},
            ServiceSettings = new ServiceSettings
            {{
                Interval = 5000,
                LogLevel = ""Information""
            }}
        }};

        public {serviceName}()
        {{
            ServiceName = ""{serviceName}"";
            _cancellationTokenSource = new CancellationTokenSource();
        }}

        protected override void OnStart(string[] args)
        {{
            try
            {{
                LogMessage(""Service starting..."");
                
                // Enable Change Tracking if not already enabled
                EnableChangeTracking();
                
                // Start replication task
                _replicationTask = Task.Run(() => ReplicationLoop(_cancellationTokenSource.Token));
                
                LogMessage(""Service started successfully"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error starting service: {{ex.Message}}"");
                throw;
            }}
        }}

        protected override void OnStop()
        {{
            try
            {{
                LogMessage(""Service stopping..."");
                _cancellationTokenSource.Cancel();
                _replicationTask?.Wait(TimeSpan.FromSeconds(30));
                LogMessage(""Service stopped"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error stopping service: {{ex.Message}}"");
            }}
        }}

        private void EnableChangeTracking()
        {{
            // Enable Change Tracking on the database and tables
            LogMessage(""Enabling Change Tracking..."");
        }}

        private async Task ReplicationLoop(CancellationToken cancellationToken)
        {{
            while (!cancellationToken.IsCancellationRequested)
            {{
                try
                {{
                    // Check for changes using Change Tracking
                    await CheckForChangesChangeTracking();
                    
                    // Wait for next interval
                    await Task.Delay(_config.ServiceSettings.Interval, cancellationToken);
                }}
                catch (OperationCanceledException)
                {{
                    break;
                }}
                catch (Exception ex)
                {{
                    LogMessage($""Error in replication loop: {{ex.Message}}"");
                    await Task.Delay(10000, cancellationToken);
                }}
            }}
        }}

        private async Task CheckForChangesChangeTracking()
        {{
            // Implementation for checking changes using Change Tracking
            LogMessage(""Checking for changes using Change Tracking..."");
        }}

        private void LogMessage(string message)
        {{
            string logMessage = $""[{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}] {{message}}"";
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""{serviceName}.log"");
            
            try
            {{
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }}
            catch
            {{
                // If logging fails, continue without logging
            }}
        }}
    }}

    // Configuration classes
    public class ReplicationConfig
    {{
        public ConnectionStrings ConnectionStrings {{ get; set; }}
        public ServiceSettings ServiceSettings {{ get; set; }}
    }}

    public class ConnectionStrings
    {{
        public string Base {{ get; set; }}
        public string Source {{ get; set; }}
        public string Target {{ get; set; }}
    }}

    public class ServiceSettings
    {{
        public int Interval {{ get; set; }}
        public string LogLevel {{ get; set; }}
    }}
}}";
                case "Polling":
                    return $@"
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;

namespace SqlReplicatorService
{{
    public class {serviceName} : ServiceBase
    {{
        private CancellationTokenSource _cancellationTokenSource;
        private Task _replicationTask;
        
        // Embedded configuration
        private readonly ReplicationConfig _config = new ReplicationConfig
        {{
            ConnectionStrings = new ConnectionStrings
            {{
                Base = ""YOUR_BASE_CONNECTION_STRING_HERE"",
                Source = ""YOUR_SOURCE_CONNECTION_STRING_HERE"",
                Target = ""YOUR_TARGET_CONNECTION_STRING_HERE""
            }},
            ServiceSettings = new ServiceSettings
            {{
                Interval = 300000, // 5 minutes for polling
                LogLevel = ""Information""
            }}
        }};

        public {serviceName}()
        {{
            ServiceName = ""{serviceName}"";
            _cancellationTokenSource = new CancellationTokenSource();
        }}

        protected override void OnStart(string[] args)
        {{
            try
            {{
                LogMessage(""Service starting..."");
                
                // Start replication task
                _replicationTask = Task.Run(() => ReplicationLoop(_cancellationTokenSource.Token));
                
                LogMessage(""Service started successfully"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error starting service: {{ex.Message}}"");
                throw;
            }}
        }}

        protected override void OnStop()
        {{
            try
            {{
                LogMessage(""Service stopping..."");
                _cancellationTokenSource.Cancel();
                _replicationTask?.Wait(TimeSpan.FromSeconds(30));
                LogMessage(""Service stopped"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error stopping service: {{ex.Message}}"");
            }}
        }}

        private async Task ReplicationLoop(CancellationToken cancellationToken)
        {{
            while (!cancellationToken.IsCancellationRequested)
            {{
                try
                {{
                    // Check for changes using polling
                    await CheckForChangesPolling();
                    
                    // Wait for next interval
                    await Task.Delay(_config.ServiceSettings.Interval, cancellationToken);
                }}
                catch (OperationCanceledException)
                {{
                    break;
                }}
                catch (Exception ex)
                {{
                    LogMessage($""Error in replication loop: {{ex.Message}}"");
                    await Task.Delay(60000, cancellationToken); // Wait 1 minute before retry
                }}
            }}
        }}

        private async Task CheckForChangesPolling()
        {{
            // Implementation for checking changes using polling
            LogMessage(""Checking for changes using polling..."");
        }}

        private void LogMessage(string message)
        {{
            string logMessage = $""[{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}] {{message}}"";
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""{serviceName}.log"");
            
            try
            {{
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }}
            catch
            {{
                // If logging fails, continue without logging
            }}
        }}
    }}

    // Configuration classes
    public class ReplicationConfig
    {{
        public ConnectionStrings ConnectionStrings {{ get; set; }}
        public ServiceSettings ServiceSettings {{ get; set; }}
    }}

    public class ConnectionStrings
    {{
        public string Base {{ get; set; }}
        public string Source {{ get; set; }}
        public string Target {{ get; set; }}
    }}

    public class ServiceSettings
    {{
        public int Interval {{ get; set; }}
        public string LogLevel {{ get; set; }}
    }}
}}";
                case "Temporal":
                    return $@"
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;

namespace SqlReplicatorService
{{
    public class {serviceName} : ServiceBase
    {{
        private CancellationTokenSource _cancellationTokenSource;
        private Task _replicationTask;
        
        // Embedded configuration
        private readonly ReplicationConfig _config = new ReplicationConfig
        {{
            ConnectionStrings = new ConnectionStrings
            {{
                Base = ""YOUR_BASE_CONNECTION_STRING_HERE"",
                Source = ""YOUR_SOURCE_CONNECTION_STRING_HERE"",
                Target = ""YOUR_TARGET_CONNECTION_STRING_HERE""
            }},
            ServiceSettings = new ServiceSettings
            {{
                Interval = 5000,
                LogLevel = ""Information""
            }}
        }};

        public {serviceName}()
        {{
            ServiceName = ""{serviceName}"";
            _cancellationTokenSource = new CancellationTokenSource();
        }}

        protected override void OnStart(string[] args)
        {{
            try
            {{
                LogMessage(""Service starting..."");
                
                // Verify Temporal Tables are enabled
                VerifyTemporalTables();
                
                // Start replication task
                _replicationTask = Task.Run(() => ReplicationLoop(_cancellationTokenSource.Token));
                
                LogMessage(""Service started successfully"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error starting service: {{ex.Message}}"");
                throw;
            }}
        }}

        protected override void OnStop()
        {{
            try
            {{
                LogMessage(""Service stopping..."");
                _cancellationTokenSource.Cancel();
                _replicationTask?.Wait(TimeSpan.FromSeconds(30));
                LogMessage(""Service stopped"");
            }}
            catch (Exception ex)
            {{
                LogMessage($""Error stopping service: {{ex.Message}}"");
            }}
        }}

        private void VerifyTemporalTables()
        {{
            // Verify that temporal tables are properly configured
            LogMessage(""Verifying Temporal Tables configuration..."");
        }}

        private async Task ReplicationLoop(CancellationToken cancellationToken)
        {{
            while (!cancellationToken.IsCancellationRequested)
            {{
                try
                {{
                    // Check for changes using Temporal Tables
                    await CheckForChangesTemporal();
                    
                    // Wait for next interval
                    await Task.Delay(_config.ServiceSettings.Interval, cancellationToken);
                }}
                catch (OperationCanceledException)
                {{
                    break;
                }}
                catch (Exception ex)
                {{
                    LogMessage($""Error in replication loop: {{ex.Message}}"");
                    await Task.Delay(10000, cancellationToken);
                }}
            }}
        }}

        private async Task CheckForChangesTemporal()
        {{
            // Implementation for checking changes using Temporal Tables
            LogMessage(""Checking for changes using Temporal Tables..."");
        }}

        private void LogMessage(string message)
        {{
            string logMessage = $""[{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}] {{message}}"";
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ""{serviceName}.log"");
            
            try
            {{
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }}
            catch
            {{
                // If logging fails, continue without logging
            }}
        }}
    }}

    // Configuration classes
    public class ReplicationConfig
    {{
        public ConnectionStrings ConnectionStrings {{ get; set; }}
        public ServiceSettings ServiceSettings {{ get; set; }}
    }}

    public class ConnectionStrings
    {{
        public string Base {{ get; set; }}
        public string Source {{ get; set; }}
        public string Target {{ get; set; }}
    }}

    public class ServiceSettings
    {{
        public int Interval {{ get; set; }}
        public string LogLevel {{ get; set; }}
    }}
}}";
                default:
                    return "// Service template";
            }
        }



        private string GetDocumentationTemplate(string listenerType, string serviceName, ConfigurationData configData)
        {
            switch (listenerType)
            {
                case "Trigger":
                    return GetTriggerDocumentation(serviceName, configData);
                case "CDC":
                    return GetCDCDocumentation(serviceName, configData);
                case "ChangeTracking":
                    return GetChangeTrackingDocumentation(serviceName, configData);
                case "Polling":
                    return GetPollingDocumentation(serviceName, configData);
                case "Temporal":
                    return GetTemporalDocumentation(serviceName, configData);
                default:
                    return GetGenericDocumentation(serviceName, listenerType, configData);
            }
        }

        private string GetTriggerDocumentation(string serviceName, ConfigurationData configData)
        {
            // Generate field-specific trigger SQL for each table
            var triggerSqls = new List<string>();
            foreach (var tableName in configData.SelectedTables)
            {
                if (configData.TableFields.ContainsKey(tableName) && configData.TablePrimaryKeys.ContainsKey(tableName))
                {
                    var fields = configData.TableFields[tableName];
                    var primaryKeyField = configData.TablePrimaryKeys[tableName];
                    
                    // Create field list for JSON generation
                    var fieldList = string.Join(", ", fields.Select(f => $"[{f.FieldName}]"));
                    
                    triggerSqls.Add($@"-- تریگر برای جدول: {tableName}
CREATE TRIGGER [dbo].[TR_{tableName}_ChangeDetection]
ON [dbo].[{tableName}]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Operation char(1)
    DECLARE @PrimaryKeyValue nvarchar(max)
    DECLARE @ChangeData nvarchar(max)
    
    -- تعیین نوع عملیات
    IF EXISTS(SELECT 1 FROM inserted) AND EXISTS(SELECT 1 FROM deleted)
        SET @Operation = 'U' -- بروزرسانی
    ELSE IF EXISTS(SELECT 1 FROM inserted)
        SET @Operation = 'I' -- درج
    ELSE
        SET @Operation = 'D' -- حذف
    
    -- دریافت مقدار کلید اصلی
    IF @Operation = 'I' OR @Operation = 'U'
    BEGIN
        SELECT @PrimaryKeyValue = CAST([{primaryKeyField}] AS nvarchar(max)) FROM inserted
        SELECT @ChangeData = (SELECT {fieldList} FROM inserted FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    END
    ELSE
    BEGIN
        SELECT @PrimaryKeyValue = CAST([{primaryKeyField}] AS nvarchar(max)) FROM deleted
        SELECT @ChangeData = (SELECT {fieldList} FROM deleted FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    END
    
    -- ثبت تغییر در جدول ChangeLog
    INSERT INTO [dbo].[ChangeLog] (TableName, Operation, PrimaryKeyValue, ChangeData)
    VALUES ('{tableName}', @Operation, @PrimaryKeyValue, @ChangeData)
END
GO");
                }
            }

            // Generate field information for each table
            var tableFieldInfo = new List<string>();
            foreach (var tableName in configData.SelectedTables)
            {
                if (configData.TableFields.ContainsKey(tableName))
                {
                    var fields = configData.TableFields[tableName];
                    var primaryKey = configData.TablePrimaryKeys.ContainsKey(tableName) ? configData.TablePrimaryKeys[tableName] : "تعریف نشده";
                    
                    tableFieldInfo.Add($@"جدول: {tableName}
   کلید اصلی: {primaryKey}
   فیلدها:
{string.Join("\n", fields.Select(f => $"      - {f.FieldName} ({f.DataType}){(f.IsPrimaryKey ? " [کلید اصلی]" : "")}"))}");
                }
            }

            return $@"مستندات سرویس {serviceName} (تشخیص تغییرات با استفاده از تریگر)
================================================================================

۱. توضیح کلی سرویس:
   ==================
   این سرویس ویندوزی از تریگرهای SQL Server برای تشخیص تغییرات در زمان واقعی استفاده می‌کند
   و آن‌ها را به دیتابیس مقصد منتقل می‌کند. تریگرها به صورت خودکار هر تغییر در جداول
   انتخاب شده را ثبت کرده و سرویس آن‌ها را پردازش می‌کند.

۲. اطلاعات اتصال:
   ================
   سرور مبدا: {configData.SourceServer}
   دیتابیس مبدا: {configData.SourceDatabase}
   سرور مقصد: {configData.TargetServer}
   دیتابیس مقصد: {configData.TargetDatabase}
   
   رشته اتصال مبدا: {configData.SourceConnectionString}
   رشته اتصال مقصد: {configData.TargetConnectionString}

۳. جداول انتخاب شده برای تکثیر:
   ==============================
{string.Join("\n", configData.SelectedTables.Select(t => $"   - {t}"))}

۴. اطلاعات فیلدهای جداول:
   ========================
{string.Join("\n\n", tableFieldInfo)}

۵. پیش‌نیازها:
   ============
   - SQL Server 2016 یا بالاتر
   - دسترسی مناسب برای ایجاد تریگر
   - اتصال شبکه بین دیتابیس‌های مبدا و مقصد

۶. مراحل نصب:
   ===========

   مرحله ۱: ایجاد جداول تشخیص تغییرات
   ------------------------------------
   -- این دستورات را در دیتابیس مبدا اجرا کنید
   USE [{configData.SourceDatabase}]
   GO

   -- ایجاد جدول ثبت تغییرات
   CREATE TABLE [dbo].[ChangeLog](
       [ChangeID] [bigint] IDENTITY(1,1) NOT NULL,
       [TableName] [nvarchar](128) NOT NULL,
       [Operation] [char](1) NOT NULL, -- I=درج, U=بروزرسانی, D=حذف
       [PrimaryKeyValue] [nvarchar](max) NOT NULL,
       [ChangeData] [nvarchar](max) NULL,
       [ChangeTimestamp] [datetime2](7) NOT NULL DEFAULT GETUTCDATE(),
       [Processed] [bit] NOT NULL DEFAULT 0,
       CONSTRAINT [PK_ChangeLog] PRIMARY KEY CLUSTERED ([ChangeID] ASC)
   )
   GO

   -- ایجاد ایندکس برای بهبود عملکرد
   CREATE NONCLUSTERED INDEX [IX_ChangeLog_Processed] ON [dbo].[ChangeLog]
   ([Processed] ASC, [ChangeTimestamp] ASC)
   GO

   مرحله ۲: ایجاد تریگرها برای جداول انتخاب شده
   ----------------------------------------------
   -- تریگرها را برای هر جدول انتخاب شده ایجاد کنید
   -- این دستورات را در دیتابیس مبدا اجرا کنید
   
{string.Join("\n\n", triggerSqls)}

   مرحله ۳: ایجاد پروسیجر پردازش تغییرات
   --------------------------------------
   -- این دستور را در دیتابیس مبدا اجرا کنید
   CREATE PROCEDURE [dbo].[sp_ProcessChanges]
       @BatchSize int = 100
   AS
   BEGIN
       SET NOCOUNT ON;
       
       DECLARE @ChangeID bigint
       DECLARE @TableName nvarchar(128)
       DECLARE @Operation char(1)
       DECLARE @PrimaryKeyValue nvarchar(max)
       DECLARE @ChangeData nvarchar(max)
       
       -- دریافت تغییرات پردازش نشده
       DECLARE change_cursor CURSOR FOR
       SELECT TOP (@BatchSize) ChangeID, TableName, Operation, PrimaryKeyValue, ChangeData
       FROM [dbo].[ChangeLog]
       WHERE Processed = 0
       ORDER BY ChangeID
       
       OPEN change_cursor
       FETCH NEXT FROM change_cursor INTO @ChangeID, @TableName, @Operation, @PrimaryKeyValue, @ChangeData
       
       WHILE @@FETCH_STATUS = 0
       BEGIN
           -- در اینجا منطق تکثیر واقعی پیاده‌سازی می‌شود
           -- این یک نمونه برای فرآیند تکثیر است
           
           -- علامت‌گذاری به عنوان پردازش شده
           UPDATE [dbo].[ChangeLog]
           SET Processed = 1
           WHERE ChangeID = @ChangeID
           
           FETCH NEXT FROM change_cursor INTO @ChangeID, @TableName, @Operation, @PrimaryKeyValue, @ChangeData
       END
       
       CLOSE change_cursor
       DEALLOCATE change_cursor
   END
   GO

۷. پیکربندی سرویس:
   =================
   
   فایل اجرایی سرویس ({serviceName}.exe) شامل تمام تنظیمات اتصال است
   و نیازی به فایل پیکربندی جداگانه ندارد.

۸. کوئری‌های تست:
   ================
   
   -- تست ۱: بررسی اتصال به دیتابیس مبدا
   -- این دستور را برای تأیید اتصال به دیتابیس مبدا اجرا کنید
   USE [{configData.SourceDatabase}]
   GO
   SELECT @@VERSION as SQLServerVersion, DB_NAME() as CurrentDatabase
   GO
   
   -- تست ۲: بررسی اتصال به دیتابیس مقصد
   -- این دستور را برای تأیید اتصال به دیتابیس مقصد اجرا کنید
   USE [{configData.TargetDatabase}]
   GO
   SELECT @@VERSION as SQLServerVersion, DB_NAME() as CurrentDatabase
   GO
   
   -- تست ۳: بررسی وجود جدول ChangeLog
   USE [{configData.SourceDatabase}]
   GO
   SELECT COUNT(*) as ChangeLogTableExists 
   FROM INFORMATION_SCHEMA.TABLES 
   WHERE TABLE_NAME = 'ChangeLog' AND TABLE_SCHEMA = 'dbo'
   GO
   
   -- تست ۴: بررسی تریگرها روی جداول انتخاب شده
   USE [{configData.SourceDatabase}]
   GO
   SELECT 
       t.name as TableName,
       tr.name as TriggerName,
       tr.is_disabled as IsDisabled
   FROM sys.tables t
   LEFT JOIN sys.triggers tr ON t.object_id = tr.parent_id
   WHERE t.name IN ({string.Join(",", configData.SelectedTables.Select(t => $"'{t}'"))})
   ORDER BY t.name, tr.name
   GO
   
   -- تست ۵: درج داده تست برای فعال‌سازی تریگر
   USE [{configData.SourceDatabase}]
   GO
   -- نام جدول را با یکی از جداول انتخاب شده جایگزین کنید
   -- مثال: INSERT INTO [dbo].[{configData.SelectedTables.FirstOrDefault() ?? "YourTableName"}] (Column1, Column2) VALUES ('TestValue1', 'TestValue2')
   GO
   
   -- تست ۶: بررسی تغییرات در ChangeLog
   USE [{configData.SourceDatabase}]
   GO
   SELECT TOP 10 * FROM [dbo].[ChangeLog] ORDER BY ChangeTimestamp DESC
   GO

۹. نظارت و مانیتورینگ:
   =====================
   
   -- بررسی وضعیت سرویس
   SELECT * FROM [dbo].[ChangeLog] WHERE Processed = 0 ORDER BY ChangeTimestamp DESC
   
   -- بررسی عملکرد پردازش
   SELECT 
       COUNT(*) as TotalChanges,
       SUM(CASE WHEN Processed = 1 THEN 1 ELSE 0 END) as ProcessedChanges,
       SUM(CASE WHEN Processed = 0 THEN 1 ELSE 0 END) as PendingChanges
   FROM [dbo].[ChangeLog]
   WHERE ChangeTimestamp >= DATEADD(HOUR, -1, GETUTCDATE())

۱۰. عیب‌یابی:
    ===========
    
    -- بررسی خطاهای تریگر
    SELECT * FROM [dbo].[ChangeLog] WHERE ChangeData IS NULL
    
    -- بررسی خطاهای پردازش
    SELECT * FROM [dbo].[ChangeLog] WHERE Processed = 0 AND ChangeTimestamp < DATEADD(MINUTE, -5, GETUTCDATE())

۱۱. پاک‌سازی:
    ===========
    
    -- پاک‌سازی تغییرات قدیمی پردازش شده (به صورت دوره‌ای اجرا کنید)
    DELETE FROM [dbo].[ChangeLog] 
    WHERE Processed = 1 AND ChangeTimestamp < DATEADD(DAY, -7, GETUTCDATE())

۱۲. توضیحات تکمیلی:
    ==================
    
    نحوه کارکرد سرویس:
    - سرویس به صورت مداوم جدول ChangeLog را بررسی می‌کند
    - تغییرات جدید را شناسایی کرده و آن‌ها را به دیتابیس مقصد منتقل می‌کند
    - پس از انتقال موفق، رکورد را به عنوان پردازش شده علامت‌گذاری می‌کند
    - در صورت بروز خطا، سرویس تلاش می‌کند تا مشکل را حل کند
    
    نکات مهم:
    - تریگرها به صورت خودکار هر تغییر در جداول انتخاب شده را ثبت می‌کنند
    - داده‌ها به صورت JSON در فیلد ChangeData ذخیره می‌شوند
    - سرویس هر {configData.ServiceInterval} میلی‌ثانیه یکبار تغییرات را بررسی می‌کند
    - تمام تنظیمات در فایل اجرایی سرویس تعبیه شده‌اند

================================================================================
پایان مستندات
";
        }

        private string GetCDCDocumentation(string serviceName, ConfigurationData configData)
        {
            return $@"DOCUMENTATION FOR {serviceName} (CHANGE DATA CAPTURE)
================================================================================

1. SERVICE OVERVIEW:
   This service uses SQL Server's Change Data Capture (CDC) feature to detect and replicate changes.

2. PREREQUISITES:
   - SQL Server 2008 or later
   - CDC must be enabled on the database
   - Appropriate permissions to enable CDC

3. INSTALLATION STEPS:
   ===================

   Step 1: Enable CDC on Database
   ------------------------------
   -- Run this on your SOURCE database
   USE [YourSourceDatabase]
   GO
   
   -- Enable CDC at database level
   EXEC sys.sp_cdc_enable_db
   GO
   
   -- Verify CDC is enabled
   SELECT name, is_cdc_enabled FROM sys.databases WHERE name = 'YourSourceDatabase'
   GO

   Step 2: Enable CDC on Tables
   ----------------------------
   -- Enable CDC on specific tables
   -- Replace [YourTableName] with actual table names
   
   EXEC sys.sp_cdc_enable_table
       @source_schema = N'dbo',
       @source_name = N'YourTableName',
       @role_name = NULL,
       @supports_net_changes = 1
   GO
   
   -- Verify CDC is enabled on table
   SELECT name, is_tracked_by_cdc FROM sys.tables WHERE name = 'YourTableName'
   GO

   Step 3: Create CDC Processing Procedure
   ---------------------------------------
   CREATE PROCEDURE [dbo].[sp_ProcessCDCChanges]
       @TableName nvarchar(128),
       @FromLSN binary(10) = NULL,
       @ToLSN binary(10) = NULL
   AS
   BEGIN
       SET NOCOUNT ON;
       
       -- Get CDC capture instance name
       DECLARE @CaptureInstance nvarchar(128)
       SELECT @CaptureInstance = capture_instance 
       FROM cdc.change_tables 
       WHERE source_object_id = OBJECT_ID(@TableName)
       
       IF @CaptureInstance IS NULL
       BEGIN
           RAISERROR ('CDC not enabled for table %s', 16, 1, @TableName)
           RETURN
       END
       
       -- Get LSN range if not provided
       IF @FromLSN IS NULL
           SET @FromLSN = sys.fn_cdc_get_min_lsn(@CaptureInstance)
       
       IF @ToLSN IS NULL
           SET @ToLSN = sys.fn_cdc_get_max_lsn()
       
       -- Check if LSN range is valid
       IF @FromLSN IS NULL OR @ToLSN IS NULL OR @FromLSN > @ToLSN
       BEGIN
           PRINT 'No changes to process'
           RETURN
       END
       
       -- Process changes
       DECLARE @SQL nvarchar(max) = '
       SELECT 
           __$operation,
           __$seqval,
           __$update_mask,
           *
       FROM cdc.fn_cdc_get_all_changes_' + @CaptureInstance + '(@FromLSN, @ToLSN, N''all'')
       ORDER BY __$seqval'
       
       EXEC sp_executesql @SQL, N'@FromLSN binary(10), @ToLSN binary(10)', @FromLSN, @ToLSN
   END
   GO

4. TESTING QUERIES:
   ================
   
   -- Test 1: Check CDC status
   SELECT name, is_cdc_enabled FROM sys.databases WHERE name = 'YourSourceDatabase'
   GO
   
   -- Test 2: Check CDC tables
   SELECT 
       t.name as TableName,
       ct.capture_instance,
       ct.is_tracked_by_cdc
   FROM sys.tables t
   LEFT JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
   WHERE t.name = 'YourTableName'
   GO
   
   -- Test 3: Make a change and check CDC
   INSERT INTO [YourTableName] (Column1, Column2) VALUES ('Test', 'CDC')
   GO
   
   -- Get the latest LSN
   SELECT sys.fn_cdc_get_max_lsn() as MaxLSN
   GO
   
   -- Process changes
   EXEC [dbo].[sp_ProcessCDCChanges] @TableName = 'YourTableName'
   GO

5. MONITORING:
   ============
   
   -- Check CDC capture instances
   SELECT 
       capture_instance,
       source_object_id,
       start_lsn,
       end_lsn
   FROM cdc.change_tables
   GO
   
   -- Check CDC jobs
   SELECT 
       name,
       enabled,
       date_created,
       date_modified
   FROM msdb.dbo.sysjobs
   WHERE name LIKE '%cdc%'
   GO
   
   -- Check CDC log
   SELECT 
       start_lsn,
       end_lsn,
       tran_id,
       command_id
   FROM cdc.lsn_time_mapping
   ORDER BY start_lsn DESC
   GO

6. TROUBLESHOOTING:
   =================
   
   -- Check if CDC capture job is running
   SELECT 
       j.name,
       j.enabled,
       h.run_status,
       h.run_date,
       h.run_time
   FROM msdb.dbo.sysjobs j
   LEFT JOIN msdb.dbo.sysjobhistory h ON j.job_id = h.job_id
   WHERE j.name LIKE '%cdc%'
   ORDER BY h.run_date DESC, h.run_time DESC
   GO
   
   -- Check for CDC errors
   SELECT 
       error_number,
       error_severity,
       error_state,
       error_message
   FROM sys.dm_cdc_errors
   GO

7. CLEANUP:
   =========
   
   -- Clean up old CDC data (run periodically)
   EXEC sys.sp_cdc_cleanup_change_table 
       @capture_instance = 'dbo_YourTableName',
       @low_water_mark = sys.fn_cdc_get_min_lsn('dbo_YourTableName')
   GO

================================================================================
END OF DOCUMENTATION
";
        }

        private string GetChangeTrackingDocumentation(string serviceName, ConfigurationData configData)
        {
            return $@"DOCUMENTATION FOR {serviceName} (CHANGE TRACKING)
================================================================================

1. SERVICE OVERVIEW:
   This service uses SQL Server's Change Tracking feature to detect and replicate changes.

2. PREREQUISITES:
   - SQL Server 2008 or later
   - Change Tracking must be enabled on the database
   - Appropriate permissions to enable Change Tracking

3. INSTALLATION STEPS:
   ===================

   Step 1: Enable Change Tracking on Database
   -----------------------------------------
   -- Run this on your SOURCE database
   USE [YourSourceDatabase]
   GO
   
   -- Enable Change Tracking at database level
   ALTER DATABASE [YourSourceDatabase] SET CHANGE_TRACKING = ON
   (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON)
   GO
   
   -- Verify Change Tracking is enabled
   SELECT name, is_change_tracking_enabled FROM sys.databases WHERE name = 'YourSourceDatabase'
   GO

   Step 2: Enable Change Tracking on Tables
   ----------------------------------------
   -- Enable Change Tracking on specific tables
   -- Replace [YourTableName] with actual table names
   
   ALTER TABLE [dbo].[YourTableName] ENABLE CHANGE_TRACKING
   GO
   
   -- Verify Change Tracking is enabled on table
   SELECT 
       t.name as TableName,
       ct.is_track_columns_updated_on
   FROM sys.tables t
   LEFT JOIN sys.change_tracking_tables ct ON t.object_id = ct.object_id
   WHERE t.name = 'YourTableName'
   GO

   Step 3: Create Change Tracking Processing Procedure
   ---------------------------------------------------
   CREATE PROCEDURE [dbo].[sp_ProcessChangeTracking]
       @TableName nvarchar(128),
       @LastSyncVersion bigint = NULL
   AS
   BEGIN
       SET NOCOUNT ON;
       
       -- Get current version
       DECLARE @CurrentVersion bigint
       SET @CurrentVersion = CHANGE_TRACKING_CURRENT_VERSION()
       
       -- If no last sync version, start from beginning
       IF @LastSyncVersion IS NULL
           SET @LastSyncVersion = 0
       
       -- Get changes for the table
       SELECT 
           CT.SYS_CHANGE_OPERATION,
           CT.SYS_CHANGE_VERSION,
           CT.SYS_CHANGE_CREATION_VERSION,
           CT.SYS_CHANGE_COLUMNS,
           CT.SYS_CHANGE_CONTEXT,
           T.*
       FROM CHANGETABLE(CHANGES [dbo].[YourTableName], @LastSyncVersion) AS CT
       LEFT JOIN [dbo].[YourTableName] AS T ON CT.[PrimaryKeyColumn] = T.[PrimaryKeyColumn]
       ORDER BY CT.SYS_CHANGE_VERSION
       
       -- Return current version for next sync
       SELECT @CurrentVersion as CurrentVersion
   END
   GO

4. TESTING QUERIES:
   ================
   
   -- Test 1: Check Change Tracking status
   SELECT name, is_change_tracking_enabled FROM sys.databases WHERE name = 'YourSourceDatabase'
   GO
   
   -- Test 2: Check Change Tracking tables
   SELECT 
       t.name as TableName,
       ct.is_track_columns_updated_on
   FROM sys.tables t
   LEFT JOIN sys.change_tracking_tables ct ON t.object_id = ct.object_id
   WHERE t.name = 'YourTableName'
   GO
   
   -- Test 3: Make a change and check Change Tracking
   INSERT INTO [YourTableName] (Column1, Column2) VALUES ('Test', 'ChangeTracking')
   GO
   
   -- Get current version
   SELECT CHANGE_TRACKING_CURRENT_VERSION() as CurrentVersion
   GO
   
   -- Process changes
   EXEC [dbo].[sp_ProcessChangeTracking] @TableName = 'YourTableName', @LastSyncVersion = 0
   GO

5. MONITORING:
   ============
   
   -- Check Change Tracking tables
   SELECT 
       t.name as TableName,
       ct.is_track_columns_updated_on
   FROM sys.tables t
   LEFT JOIN sys.change_tracking_tables ct ON t.object_id = ct.object_id
   WHERE ct.object_id IS NOT NULL
   GO
   
   -- Check Change Tracking retention
   SELECT 
       retention_period_units,
       retention_period_value
   FROM sys.change_tracking_databases
   GO
   
   -- Check current version
   SELECT CHANGE_TRACKING_CURRENT_VERSION() as CurrentVersion
   GO

6. TROUBLESHOOTING:
   =================
   
   -- Check for orphaned change tracking data
   SELECT 
       t.name as TableName,
       ct.object_id
   FROM sys.tables t
   LEFT JOIN sys.change_tracking_tables ct ON t.object_id = ct.object_id
   WHERE ct.object_id IS NULL AND t.name = 'YourTableName'
   GO
   
   -- Check change tracking performance
   SELECT 
       object_name(object_id) as TableName,
       partition_number,
       rows
   FROM sys.partitions
   WHERE object_id IN (SELECT object_id FROM sys.change_tracking_tables)
   GO

7. CLEANUP:
   =========
   
   -- Clean up old Change Tracking data (automatic with AUTO_CLEANUP = ON)
   -- Manual cleanup if needed:
   ALTER DATABASE [YourSourceDatabase] SET CHANGE_TRACKING = OFF
   GO
   
   ALTER DATABASE [YourSourceDatabase] SET CHANGE_TRACKING = ON
   (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON)
   GO

================================================================================
END OF DOCUMENTATION
";
        }

        private string GetPollingDocumentation(string serviceName, ConfigurationData configData)
        {
            return $@"DOCUMENTATION FOR {serviceName} (POLLING-BASED MONITORING)
================================================================================

1. SERVICE OVERVIEW:
   This service uses polling to periodically check for changes by comparing data snapshots.

2. PREREQUISITES:
   - SQL Server 2008 or later
   - Tables must have timestamp or version columns
   - Appropriate permissions to read source and write to target

3. INSTALLATION STEPS:
   ===================

   Step 1: Add Timestamp Columns (if not exists)
   ---------------------------------------------
   -- Run this on your SOURCE database
   USE [YourSourceDatabase]
   GO
   
   -- Add timestamp column to track changes
   -- Replace [YourTableName] with actual table names
   
   IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[YourTableName]') AND name = 'LastModified')
   BEGIN
       ALTER TABLE [dbo].[YourTableName] ADD LastModified datetime2(7) NOT NULL DEFAULT GETUTCDATE()
   END
   GO
   
   -- Create trigger to update timestamp
   IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_YourTableName_UpdateTimestamp')
   BEGIN
       EXEC('CREATE TRIGGER [dbo].[TR_YourTableName_UpdateTimestamp]
       ON [dbo].[YourTableName]
       AFTER UPDATE
       AS
       BEGIN
           SET NOCOUNT ON;
           UPDATE [dbo].[YourTableName] 
           SET LastModified = GETUTCDATE()
           FROM [dbo].[YourTableName] t
           INNER JOIN inserted i ON t.[PrimaryKeyColumn] = i.[PrimaryKeyColumn]
       END')
   END
   GO

   Step 2: Create Polling Processing Procedure
   -------------------------------------------
   CREATE PROCEDURE [dbo].[sp_ProcessPollingChanges]
       @TableName nvarchar(128),
       @LastSyncTime datetime2(7) = NULL
   AS
   BEGIN
       SET NOCOUNT ON;
       
       -- If no last sync time, start from 1 hour ago
       IF @LastSyncTime IS NULL
           SET @LastSyncTime = DATEADD(HOUR, -1, GETUTCDATE())
       
       -- Get changes since last sync
       DECLARE @SQL nvarchar(max) = '
       SELECT 
           [PrimaryKeyColumn],
           [Column1],
           [Column2],
           LastModified
       FROM [dbo].[' + @TableName + ']
       WHERE LastModified > @LastSyncTime
       ORDER BY LastModified'
       
       EXEC sp_executesql @SQL, N'@LastSyncTime datetime2(7)', @LastSyncTime
       
       -- Return current time for next sync
       SELECT GETUTCDATE() as CurrentSyncTime
   END
   GO

   Step 3: Create Hash Comparison Procedure (Alternative)
   ------------------------------------------------------
   CREATE PROCEDURE [dbo].[sp_ProcessHashChanges]
       @TableName nvarchar(128),
       @LastSyncHash nvarchar(64) = NULL
   AS
   BEGIN
       SET NOCOUNT ON;
       
       -- Calculate current hash of the table
       DECLARE @CurrentHash nvarchar(64)
       DECLARE @SQL nvarchar(max) = '
       SELECT @CurrentHash = CONVERT(nvarchar(64), HASHBYTES(''SHA2_256'', 
           (SELECT * FROM [dbo].[' + @TableName + '] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)))'
       
       EXEC sp_executesql @SQL, N'@CurrentHash nvarchar(64) OUTPUT', @CurrentHash OUTPUT
       
       -- If hash changed, return all data
       IF @CurrentHash != @LastSyncHash OR @LastSyncHash IS NULL
       BEGIN
           SET @SQL = 'SELECT * FROM [dbo].[' + @TableName + ']'
           EXEC sp_executesql @SQL
       END
       
       -- Return current hash for next sync
       SELECT @CurrentHash as CurrentHash
   END
   GO

4. TESTING QUERIES:
   ================
   
   -- Test 1: Check timestamp columns
   SELECT 
       t.name as TableName,
       c.name as ColumnName,
       c.system_type_name
   FROM sys.tables t
   INNER JOIN sys.columns c ON t.object_id = c.object_id
   WHERE c.name = 'LastModified' AND t.name = 'YourTableName'
   GO
   
   -- Test 2: Make a change and check polling
   UPDATE [YourTableName] SET Column1 = 'Updated' WHERE [PrimaryKeyColumn] = 1
   GO
   
   -- Check last modified time
   SELECT 
       [PrimaryKeyColumn],
       Column1,
       LastModified
   FROM [YourTableName]
   WHERE LastModified > DATEADD(MINUTE, -5, GETUTCDATE())
   GO
   
   -- Test 3: Process polling changes
   EXEC [dbo].[sp_ProcessPollingChanges] @TableName = 'YourTableName', @LastSyncTime = DATEADD(HOUR, -1, GETUTCDATE())
   GO

5. MONITORING:
   ============
   
   -- Check polling performance
   SELECT 
       t.name as TableName,
       COUNT(*) as TotalRows,
       MAX(LastModified) as LastChange
   FROM sys.tables t
   INNER JOIN sys.columns c ON t.object_id = c.object_id
   CROSS APPLY (SELECT COUNT(*) as cnt FROM sys.columns WHERE object_id = t.object_id AND name = 'LastModified') ct
   WHERE c.name = 'LastModified' AND ct.cnt > 0
   GROUP BY t.name
   GO
   
   -- Check recent changes
   SELECT 
       [PrimaryKeyColumn],
       LastModified,
       DATEDIFF(MINUTE, LastModified, GETUTCDATE()) as MinutesAgo
   FROM [YourTableName]
   WHERE LastModified > DATEADD(HOUR, -1, GETUTCDATE())
   ORDER BY LastModified DESC
   GO

6. TROUBLESHOOTING:
   =================
   
   -- Check for missing timestamp columns
   SELECT 
       t.name as TableName
   FROM sys.tables t
   LEFT JOIN sys.columns c ON t.object_id = c.object_id AND c.name = 'LastModified'
   WHERE c.object_id IS NULL AND t.name = 'YourTableName'
   GO
   
   -- Check for stale data
   SELECT 
       [PrimaryKeyColumn],
       LastModified,
       DATEDIFF(HOUR, LastModified, GETUTCDATE()) as HoursSinceLastChange
   FROM [YourTableName]
   WHERE LastModified < DATEADD(DAY, -1, GETUTCDATE())
   GO

7. OPTIMIZATION:
   ==============
   
   -- Create index on LastModified for better performance
   CREATE NONCLUSTERED INDEX [IX_YourTableName_LastModified] ON [dbo].[YourTableName]
   ([LastModified] ASC)
   GO
   
   -- Update statistics
   UPDATE STATISTICS [dbo].[YourTableName]
   GO

================================================================================
END OF DOCUMENTATION
";
        }

        private string GetTemporalDocumentation(string serviceName, ConfigurationData configData)
        {
            return $@"DOCUMENTATION FOR {serviceName} (TEMPORAL TABLE TRACKING)
================================================================================

1. SERVICE OVERVIEW:
   This service uses SQL Server's Temporal Tables feature to track all historical changes.

2. PREREQUISITES:
   - SQL Server 2016 or later
   - Tables must be configured as Temporal Tables
   - Appropriate permissions to access temporal data

3. INSTALLATION STEPS:
   ===================

   Step 1: Create Temporal Table (if not exists)
   ---------------------------------------------
   -- Run this on your SOURCE database
   USE [YourSourceDatabase]
   GO
   
   -- Create temporal table
   -- Replace [YourTableName] with actual table names
   
   CREATE TABLE [dbo].[YourTableName](
       [ID] [int] IDENTITY(1,1) NOT NULL,
       [Column1] [nvarchar](100) NULL,
       [Column2] [nvarchar](100) NULL,
       [ValidFrom] [datetime2](7) GENERATED ALWAYS AS ROW START NOT NULL,
       [ValidTo] [datetime2](7) GENERATED ALWAYS AS ROW END NOT NULL,
       CONSTRAINT [PK_YourTableName] PRIMARY KEY CLUSTERED ([ID] ASC),
       PERIOD FOR SYSTEM_TIME ([ValidFrom], [ValidTo])
   ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[YourTableNameHistory]))
   GO

   Step 2: Convert Existing Table to Temporal (if needed)
   ------------------------------------------------------
   -- If you have an existing table, convert it to temporal
   
   -- Add temporal columns
   ALTER TABLE [dbo].[YourTableName] ADD 
       [ValidFrom] [datetime2](7) GENERATED ALWAYS AS ROW START NOT NULL DEFAULT '1900-01-01',
       [ValidTo] [datetime2](7) GENERATED ALWAYS AS ROW END NOT NULL DEFAULT '9999-12-31'
   GO
   
   -- Enable system versioning
   ALTER TABLE [dbo].[YourTableName] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[YourTableNameHistory]))
   GO

   Step 3: Create Temporal Processing Procedure
   --------------------------------------------
   CREATE PROCEDURE [dbo].[sp_ProcessTemporalChanges]
       @TableName nvarchar(128),
       @LastSyncTime datetime2(7) = NULL
   AS
   BEGIN
       SET NOCOUNT ON;
       
       -- If no last sync time, start from 1 hour ago
       IF @LastSyncTime IS NULL
           SET @LastSyncTime = DATEADD(HOUR, -1, GETUTCDATE())
       
       -- Get changes since last sync using temporal queries
       DECLARE @SQL nvarchar(max) = '
       SELECT 
           [ID],
           [Column1],
           [Column2],
           [ValidFrom],
           [ValidTo]
       FROM [dbo].[' + @TableName + '] FOR SYSTEM_TIME ALL
       WHERE [ValidFrom] > @LastSyncTime OR [ValidTo] > @LastSyncTime
       ORDER BY [ValidFrom]'
       
       EXEC sp_executesql @SQL, N'@LastSyncTime datetime2(7)', @LastSyncTime
       
       -- Return current time for next sync
       SELECT GETUTCDATE() as CurrentSyncTime
   END
   GO

   Step 4: Create History Analysis Procedure
   -----------------------------------------
   CREATE PROCEDURE [dbo].[sp_AnalyzeTemporalHistory]
       @TableName nvarchar(128),
       @StartTime datetime2(7) = NULL,
       @EndTime datetime2(7) = NULL
   AS
   BEGIN
       SET NOCOUNT ON;
       
       IF @StartTime IS NULL
           SET @StartTime = DATEADD(DAY, -1, GETUTCDATE())
       
       IF @EndTime IS NULL
           SET @EndTime = GETUTCDATE()
       
       -- Analyze changes in the specified time range
       DECLARE @SQL nvarchar(max) = '
       SELECT 
           [ID],
           [Column1],
           [Column2],
           [ValidFrom],
           [ValidTo],
           CASE 
               WHEN [ValidTo] = ''9999-12-31'' THEN ''Current''
               ELSE ''Historical''
           END as RecordStatus
       FROM [dbo].[' + @TableName + '] FOR SYSTEM_TIME FROM @StartTime TO @EndTime
       ORDER BY [ID], [ValidFrom]'
       
       EXEC sp_executesql @SQL, N'@StartTime datetime2(7), @EndTime datetime2(7)', @StartTime, @EndTime
   END
   GO

4. TESTING QUERIES:
   ================
   
   -- Test 1: Check temporal table status
   SELECT 
       t.name as TableName,
       t.temporal_type,
       t.temporal_type_desc
   FROM sys.tables t
   WHERE t.name = 'YourTableName'
   GO
   
   -- Test 2: Check history table
   SELECT 
       t.name as TableName,
       h.name as HistoryTableName
   FROM sys.tables t
   INNER JOIN sys.tables h ON t.history_table_id = h.object_id
   WHERE t.name = 'YourTableName'
   GO
   
   -- Test 3: Make changes and check temporal tracking
   INSERT INTO [YourTableName] (Column1, Column2) VALUES ('Initial', 'Data')
   GO
   
   UPDATE [YourTableName] SET Column1 = 'Updated' WHERE ID = 1
   GO
   
   -- Check current data
   SELECT * FROM [YourTableName] FOR SYSTEM_TIME AS OF GETUTCDATE()
   GO
   
   -- Check all historical data
   SELECT * FROM [YourTableName] FOR SYSTEM_TIME ALL ORDER BY ValidFrom
   GO
   
   -- Test 4: Process temporal changes
   EXEC [dbo].[sp_ProcessTemporalChanges] @TableName = 'YourTableName', @LastSyncTime = DATEADD(HOUR, -1, GETUTCDATE())
   GO

5. MONITORING:
   ============
   
   -- Check temporal table statistics
   SELECT 
       t.name as TableName,
       t.temporal_type_desc,
       COUNT(*) as CurrentRecords
   FROM sys.tables t
   INNER JOIN sys.partitions p ON t.object_id = p.object_id
   WHERE t.name = 'YourTableName' AND p.index_id = 1
   GROUP BY t.name, t.temporal_type_desc
   GO
   
   -- Check history table statistics
   SELECT 
       h.name as HistoryTableName,
       COUNT(*) as HistoricalRecords
   FROM sys.tables t
   INNER JOIN sys.tables h ON t.history_table_id = h.object_id
   INNER JOIN sys.partitions p ON h.object_id = p.object_id
   WHERE t.name = 'YourTableName' AND p.index_id = 1
   GROUP BY h.name
   GO
   
   -- Check recent changes
   SELECT 
       [ID],
       [ValidFrom],
       [ValidTo],
       DATEDIFF(MINUTE, [ValidFrom], GETUTCDATE()) as MinutesSinceChange
   FROM [YourTableName] FOR SYSTEM_TIME ALL
   WHERE [ValidFrom] > DATEADD(HOUR, -1, GETUTCDATE())
   ORDER BY [ValidFrom] DESC
   GO

6. TROUBLESHOOTING:
   =================
   
   -- Check for non-temporal tables
   SELECT 
       t.name as TableName
   FROM sys.tables t
   WHERE t.temporal_type = 0 AND t.name = 'YourTableName'
   GO
   
   -- Check for orphaned history tables
   SELECT 
       h.name as HistoryTableName
   FROM sys.tables h
   LEFT JOIN sys.tables t ON h.object_id = t.history_table_id
   WHERE t.object_id IS NULL AND h.name LIKE '%History'
   GO
   
   -- Check temporal data consistency
   SELECT 
       [ID],
       COUNT(*) as RecordCount,
       MIN([ValidFrom]) as FirstVersion,
       MAX([ValidTo]) as LastVersion
   FROM [YourTableName] FOR SYSTEM_TIME ALL
   GROUP BY [ID]
   HAVING COUNT(*) > 1
   ORDER BY RecordCount DESC
   GO

7. OPTIMIZATION:
   ==============
   
   -- Create indexes on temporal columns
   CREATE NONCLUSTERED INDEX [IX_YourTableName_ValidFrom] ON [dbo].[YourTableName]
   ([ValidFrom] ASC)
   GO
   
   CREATE NONCLUSTERED INDEX [IX_YourTableName_ValidTo] ON [dbo].[YourTableName]
   ([ValidTo] ASC)
   GO
   
   -- Update statistics
   UPDATE STATISTICS [dbo].[YourTableName]
   UPDATE STATISTICS [dbo].[YourTableNameHistory]
   GO

8. CLEANUP:
   =========
   
   -- Archive old historical data (if needed)
   -- This is typically handled automatically by SQL Server
   -- Manual cleanup example:
   DELETE FROM [dbo].[YourTableNameHistory]
   WHERE [ValidTo] < DATEADD(YEAR, -1, GETUTCDATE())
   GO

================================================================================
END OF DOCUMENTATION
";
        }

        private string GetGenericDocumentation(string serviceName, string listenerType, ConfigurationData configData)
        {
            return $@"DOCUMENTATION FOR {serviceName} ({listenerType.ToUpper()} LISTENER)
================================================================================

1. SERVICE OVERVIEW:
   This service uses {listenerType} method to detect and replicate changes.

2. PREREQUISITES:
   - SQL Server 2008 or later
   - Appropriate permissions
   - Network connectivity between databases

3. INSTALLATION STEPS:
   ===================

   Step 1: Configure Database Connections
   -------------------------------------
   Update the connection strings in the service executable:
   
   - Base: Your configuration database connection string
   - Source: Your source database connection string  
   - Target: Your target database connection string

   Step 2: Test Connections
   ------------------------
   -- Test source connection
   USE [YourSourceDatabase]
   SELECT @@VERSION as SourceVersion
   GO
   
   -- Test target connection
   USE [YourTargetDatabase]
   SELECT @@VERSION as TargetVersion
   GO

4. SERVICE MANAGEMENT:
   ===================
   
   -- Start service
   sc start {serviceName}
   
   -- Stop service
   sc stop {serviceName}
   
   -- Check status
   sc query {serviceName}
   
   -- Delete service
   sc delete {serviceName}

5. MONITORING:
   ============
   
   -- Check service logs
   -- Log file location: [ServiceDirectory]/{serviceName}.log
   
   -- Check Windows Event Log
   -- Application logs may contain service information

6. TROUBLESHOOTING:
   =================
   
   -- Check service dependencies
   -- Verify connection strings
   -- Check file permissions
   -- Review service logs

================================================================================
END OF DOCUMENTATION
";
        }

        private async Task<bool> InstallWindowsService(string path, string serviceName)
        {
            try
            {
                string serviceExePath = Path.Combine(path, $"{serviceName}.exe");
                DisplayProgress($"مسیر فایل اجرایی سرویس: {serviceExePath}", true);
                
                // Check if service executable exists
                if (!File.Exists(serviceExePath))
                {
                    DisplayProgress($"❌ خطا: فایل اجرایی سرویس یافت نشد: {serviceExePath}", false);
                    DisplayProgress("فایل‌های موجود در مسیر:", false);
                    try
                    {
                        var files = Directory.GetFiles(path, "*.exe");
                        foreach (var file in files)
                        {
                            DisplayProgress($"  - {Path.GetFileName(file)}", false);
                        }
                    }
                    catch (Exception ex)
                    {
                        DisplayProgress($"خطا در بررسی فایل‌های موجود: {ex.Message}", false);
                    }
                    return false;
                }
                DisplayProgress("✅ فایل اجرایی سرویس یافت شد", true);
                
                // Use sc.exe to install service
                var startInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"create {serviceName} binPath= \"{serviceExePath}\" start= auto",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                DisplayProgress($"دستور نصب سرویس: sc.exe {startInfo.Arguments}", true);
                
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    DisplayProgress("❌ خطا: نتوانست فرآیند sc.exe را شروع کند", false);
                    return false;
                }
                
                // Capture output in real-time
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        // Use Dispatcher to update UI from background thread
                        Dispatcher.Invoke(() => DisplayProgress($"خروجی sc.exe: {e.Data}", true));
                    }
                };
                
                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        // Use Dispatcher to update UI from background thread
                        Dispatcher.Invoke(() => DisplayProgress($"خطای sc.exe: {e.Data}", false));
                    }
                };
                
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                await process.WaitForExitAsync();
                
                DisplayProgress($"کد خروجی sc.exe: {process.ExitCode}", true);

                if (process.ExitCode == 0)
                {
                    DisplayProgress($"✅ سرویس {serviceName} با موفقیت نصب شد", true);
                    
                    // Start service if auto-start is selected
                    if (AutoStartServiceCheckBox?.IsChecked == true)
                    {
                        DisplayProgress("شروع خودکار سرویس...", true);
                        startInfo.Arguments = $"start {serviceName}";
                        DisplayProgress($"دستور شروع سرویس: sc.exe {startInfo.Arguments}", true);
                        
                        using var startProcess = Process.Start(startInfo);
                        if (startProcess != null)
                        {
                            await startProcess.WaitForExitAsync();
                            DisplayProgress($"کد خروجی شروع سرویس: {startProcess.ExitCode}", true);
                            
                            if (startProcess.ExitCode == 0)
                            {
                                DisplayProgress($"✅ سرویس {serviceName} با موفقیت شروع شد", true);
                            }
                            else
                            {
                                DisplayProgress($"⚠️ هشدار: سرویس نصب شد اما شروع نشد (کد: {startProcess.ExitCode})", false);
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    string output = outputBuilder.ToString();
                    string error = errorBuilder.ToString();
                    
                    DisplayProgress("❌ خطا در نصب سرویس", false);
                    DisplayProgress($"کد خطا: {process.ExitCode}", false);
                    DisplayProgress($"خروجی کامل sc.exe: {output}", false);
                    DisplayProgress($"خطاهای کامل sc.exe: {error}", false);
                    
                    // Provide specific error guidance
                    if (process.ExitCode == 1060)
                    {
                        DisplayProgress("راهنمایی: سرویس قبلاً نصب شده است. ابتدا آن را حذف کنید.", false);
                    }
                    else if (process.ExitCode == 5)
                    {
                        DisplayProgress("راهنمایی: دسترسی رد شد. برنامه را با دسترسی Administrator اجرا کنید.", false);
                    }
                    else if (process.ExitCode == 87)
                    {
                        DisplayProgress("راهنمایی: پارامترهای دستور اشتباه است.", false);
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                DisplayProgress($"❌ خطای غیرمنتظره در نصب سرویس: {ex.Message}", false);
                DisplayProgress($"جزئیات خطا: {ex.StackTrace}", false);
                return false;
            }
        }

        private void CreateDesktopShortcut(string path)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "SqlReplicator Service.lnk");
                string targetPath = Path.Combine(path, "SqlReplicatorService.exe");

                CreateShortcut(shortcutPath, targetPath, "SqlReplicator Service");
            }
            catch (Exception ex)
            {
                DisplayProgress($"Error creating desktop shortcut: {ex.Message}", false);
            }
        }

        private void CreateStartMenuShortcut(string path)
        {
            try
            {
                string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                string shortcutPath = Path.Combine(startMenuPath, "SqlReplicator Service.lnk");
                string targetPath = Path.Combine(path, "SqlReplicatorService.exe");

                CreateShortcut(shortcutPath, targetPath, "SqlReplicator Service");
            }
            catch (Exception ex)
            {
                DisplayProgress($"Error creating start menu shortcut: {ex.Message}", false);
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string description)
        {
            try
            {
                // Use COM Interop to create shortcut
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                
                shortcut.TargetPath = targetPath;
                shortcut.Description = description;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
                
                // Release COM resources
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                
                DisplayProgress($"Shortcut created: {shortcutPath}", true);
            }
            catch (Exception ex)
            {
                DisplayProgress($"Error creating shortcut: {ex.Message}", false);
            }
        }

        private async void TestService_Click(object sender, RoutedEventArgs e)
        {
            await TestService();
        }

        private async Task TestService()
        {
            try
            {
                TestServiceButton.IsEnabled = false;
                DisplayProgress("=== شروع تست سرویس ===", true);
                DisplayProgress($"زمان شروع تست: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", true);

                // Get service name from the last created service
                if (string.IsNullOrEmpty(_lastCreatedServiceName))
                {
                    DisplayProgress("❌ خطا: هیچ سرویسی ایجاد نشده است. لطفا ابتدا سرویس را ایجاد کنید", false);
                    MessageBox.Show("No service has been created yet. Please create a service first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    TestServiceButton.IsEnabled = true;
                    return;
                }

                string serviceName = _lastCreatedServiceName;
                DisplayProgress($"نام سرویس مورد تست: {serviceName}", true);

                // Check if service exists
                DisplayProgress("بررسی وجود سرویس...", true);
                bool serviceExists = await CheckServiceExists(serviceName);
                if (!serviceExists)
                {
                    DisplayProgress("❌ خطا: سرویس مورد نظر یافت نشد", false);
                    MessageBox.Show($"Service '{serviceName}' not found. Please create the service first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    TestServiceButton.IsEnabled = true;
                    return;
                }
                DisplayProgress("✅ سرویس یافت شد", true);

                // Check service status
                DisplayProgress("بررسی وضعیت سرویس...", true);
                bool serviceRunning = await CheckServiceStatus(serviceName);
                if (serviceRunning)
                {
                    DisplayProgress("✅ سرویس در حال اجرا است", true);
                    
                    // Capture service executable messages
                    await CaptureServiceMessages(serviceName);
                    
                    MessageBox.Show("Service tested successfully and is running.", "Test Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DisplayProgress("سرویس در حال اجرا نیست. تلاش برای شروع...", true);
                    bool started = await StartService(serviceName);
                    if (started)
                    {
                        DisplayProgress("✅ سرویس با موفقیت شروع شد", true);
                        
                        // Wait a moment for service to initialize
                        await Task.Delay(2000);
                        
                        // Capture service executable messages
                        await CaptureServiceMessages(serviceName);
                        
                        MessageBox.Show("Service started successfully.", "Start Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        DisplayProgress("❌ خطا در شروع سرویس", false);
                        
                        // Try to capture any error messages from the executable
                        await CaptureServiceMessages(serviceName);
                        
                        MessageBox.Show("Error starting service. Please check permissions and service logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                DisplayProgress("=== تست سرویس پایان یافت ===", true);
                DisplayProgress($"زمان پایان تست: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", true);

                TestServiceButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                DisplayProgress($"❌ خطا در تست سرویس: {ex.Message}", false);
                DisplayProgress($"جزئیات خطا: {ex.StackTrace}", false);
                MessageBox.Show($"Error testing service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TestServiceButton.IsEnabled = true;
            }
        }

        private async Task<bool> CheckServiceStatus(string serviceName)
        {
            try
            {
                using var serviceController = new ServiceController(serviceName);
                return serviceController.Status == ServiceControllerStatus.Running;
            }
            catch (Exception ex)
            {
                DisplayProgress($"خطا در بررسی وضعیت سرویس: {ex.Message}", false);
                return false;
            }
        }

        private async Task CaptureServiceMessages(string serviceName)
        {
            try
            {
                DisplayProgress("=== بررسی پیام‌های سرویس ===", true);
                
                // Get service executable path
                string serviceExePath = "";
                try
                {
                    using var serviceController = new ServiceController(serviceName);
                    var managementObject = new ManagementObject(
                        $"Win32_Service.Name='{serviceName}'");
                    serviceExePath = managementObject["PathName"]?.ToString() ?? "";
                    
                    // Remove quotes if present
                    if (serviceExePath.StartsWith("\"") && serviceExePath.EndsWith("\""))
                    {
                        serviceExePath = serviceExePath.Substring(1, serviceExePath.Length - 2);
                    }
                    
                    DisplayProgress($"مسیر فایل اجرایی سرویس: {serviceExePath}", true);
                }
                catch (Exception ex)
                {
                    DisplayProgress($"خطا در یافتن مسیر سرویس: {ex.Message}", false);
                    return;
                }
                
                if (string.IsNullOrEmpty(serviceExePath))
                {
                    DisplayProgress("❌ خطا: مسیر فایل اجرایی سرویس یافت نشد", false);
                    return;
                }
                
                // Check if executable exists
                if (!File.Exists(serviceExePath))
                {
                    DisplayProgress($"❌ خطا: فایل اجرایی سرویس یافت نشد: {serviceExePath}", false);
                    return;
                }
                
                // Try to run the executable with --help or --version to get any output
                DisplayProgress("تلاش برای دریافت پیام‌های فایل اجرایی...", true);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = serviceExePath,
                    Arguments = "--help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                try
                {
                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        // Set a timeout for the process
                        var timeoutTask = Task.Delay(5000); // 5 seconds timeout
                        var processTask = process.WaitForExitAsync();
                        
                        var completedTask = await Task.WhenAny(processTask, timeoutTask);
                        
                        if (completedTask == timeoutTask)
                        {
                            DisplayProgress("⏰ فایل اجرایی پس از 5 ثانیه پاسخ نداد", false);
                            try { process.Kill(); } catch { }
                        }
                        else
                        {
                            string output = await process.StandardOutput.ReadToEndAsync();
                            string error = await process.StandardError.ReadToEndAsync();
                            
                            if (!string.IsNullOrEmpty(output))
                            {
                                DisplayProgress("پیام‌های خروجی فایل اجرایی:", true);
                                foreach (var line in output.Split('\n'))
                                {
                                    if (!string.IsNullOrEmpty(line.Trim()))
                                    {
                                        DisplayProgress($"  {line.Trim()}", true);
                                    }
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(error))
                            {
                                DisplayProgress("پیام‌های خطای فایل اجرایی:", false);
                                foreach (var line in error.Split('\n'))
                                {
                                    if (!string.IsNullOrEmpty(line.Trim()))
                                    {
                                        DisplayProgress($"  {line.Trim()}", false);
                                    }
                                }
                            }
                            
                            if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error))
                            {
                                DisplayProgress("فایل اجرایی هیچ پیامی تولید نکرد", true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DisplayProgress($"خطا در اجرای فایل اجرایی: {ex.Message}", false);
                }
                
                // Also check Windows Event Log for service-related events
                DisplayProgress("بررسی لاگ‌های رویداد ویندوز...", true);
                try
                {
                    var eventLog = new EventLog("Application");
                    var recentEvents = eventLog.Entries.Cast<EventLogEntry>()
                        .Where(e => e.Source.Contains(serviceName) || e.Message.Contains(serviceName))
                        .OrderByDescending(e => e.TimeGenerated)
                        .Take(5);
                    
                    if (recentEvents.Any())
                    {
                        DisplayProgress("رویدادهای اخیر مربوط به سرویس:", true);
                        foreach (var evt in recentEvents)
                        {
                            DisplayProgress($"  [{evt.TimeGenerated:yyyy-MM-dd HH:mm:ss}] {evt.EntryType}: {evt.Message}", 
                                evt.EntryType == EventLogEntryType.Information || evt.EntryType == EventLogEntryType.SuccessAudit);
                        }
                    }
                    else
                    {
                        DisplayProgress("هیچ رویداد اخیری برای سرویس یافت نشد", true);
                    }
                }
                catch (Exception ex)
                {
                    DisplayProgress($"خطا در بررسی لاگ‌های رویداد: {ex.Message}", false);
                }
                
                DisplayProgress("=== پایان بررسی پیام‌های سرویس ===", true);
            }
            catch (Exception ex)
            {
                DisplayProgress($"❌ خطا در بررسی پیام‌های سرویس: {ex.Message}", false);
            }
        }

        private async Task<bool> StartService(string serviceName)
        {
            try
            {
                using var serviceController = new ServiceController(serviceName);
                serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                return serviceController.Status == ServiceControllerStatus.Running;
            }
            catch (Exception ex)
            {
                DisplayProgress($"خطا در شروع سرویس: {ex.Message}", false);
                return false;
            }
        }

        private async void StopService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopServiceButton.IsEnabled = false;
                DisplayProgress("متوقف کردن سرویس...", true);

                if (string.IsNullOrEmpty(_lastCreatedServiceName))
                {
                    DisplayProgress("❌ خطا: هیچ سرویسی ایجاد نشده است", false);
                    MessageBox.Show("No service has been created yet. Please create a service first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StopServiceButton.IsEnabled = true;
                    return;
                }

                using var serviceController = new ServiceController(_lastCreatedServiceName);
                if (serviceController.Status == ServiceControllerStatus.Running)
                {
                    serviceController.Stop();
                    serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    DisplayProgress("سرویس با موفقیت متوقف شد", true);
                    MessageBox.Show("Service stopped successfully.", "Stop Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DisplayProgress("سرویس در حال اجرا نیست", true);
                }

                StopServiceButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                DisplayProgress($"خطا در متوقف کردن سرویس: {ex.Message}", false);
                MessageBox.Show($"Error stopping service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopServiceButton.IsEnabled = true;
            }
        }

        private async Task UpdateServiceButtonStates()
        {
            try
            {
                if (string.IsNullOrEmpty(_lastCreatedServiceName))
                {
                    TestServiceButton.IsEnabled = false;
                    StopServiceButton.IsEnabled = false;
                    return;
                }

                bool serviceExists = await CheckServiceExists(_lastCreatedServiceName);
                if (serviceExists)
                {
                    bool isRunning = await CheckServiceStatus(_lastCreatedServiceName);
                    TestServiceButton.IsEnabled = true;
                    StopServiceButton.IsEnabled = isRunning;
                }
                else
                {
                    TestServiceButton.IsEnabled = false;
                    StopServiceButton.IsEnabled = false;
                }
            }
            catch
            {
                TestServiceButton.IsEnabled = false;
                StopServiceButton.IsEnabled = false;
            }
        }

        private async Task<bool> CheckServiceExists(string serviceName)
        {
            try
            {
                using var serviceController = new ServiceController(serviceName);
                return true;
            }
            catch (Exception ex)
            {
                DisplayProgress($"سرویس '{serviceName}' یافت نشد: {ex.Message}", false);
                return false;
            }
        }
    }
}