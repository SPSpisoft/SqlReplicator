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
                                            CheckServiceStatusButton.IsEnabled = true;

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
                    LoadAvailableListeners();
                    PopulateListenerSelectionPanel();
                    UpdateListenerSelection();
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

        private async void ManageServiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Disable the button to prevent multiple clicks
            ((Button)sender).IsEnabled = false;
            ProgressStackPanel.Children.Clear(); // Clear previous steps

            // Ensure the application is running with Administrator privileges
            if (!IsRunningAsAdministrator())
            {
                DisplayProgress("Please run the application as Administrator.", false);
                MessageBox.Show("To manage the Windows service, the application must be run with Administrator privileges.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ((Button)sender).IsEnabled = true;
                return;
            }

            // Get the base database connection string
            string baseConnectionString = connectionStrings["Base"];

            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                DisplayProgress("Base database connection string cannot be empty.", false);
                MessageBox.Show("Please enter the base database connection string.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                ((Button)sender).IsEnabled = true;
                return;
            }

            // Get selected listeners
            var selectedListeners = GetSelectedListeners();
            
            if (selectedListeners.Count == 0)
            {
                DisplayProgress("No listeners selected. Please select at least one listener.", false);
                MessageBox.Show("Please select at least one listener for change detection.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                ((Button)sender).IsEnabled = true;
                return;
            }

            // Check compatibility
            var compatibilityWarnings = ListenerCompatibilityChecker.CheckCompatibility(selectedListeners);
            if (compatibilityWarnings.Count > 0)
            {
                var warningMessage = string.Join("\n", compatibilityWarnings);
                var result = MessageBox.Show(
                    $"The following warnings were detected:\n\n{warningMessage}\n\nDo you want to continue anyway?",
                    "Listener Compatibility Warnings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    ((Button)sender).IsEnabled = true;
                    return;
                }
            }

            // Save listener configuration
            try
            {
                DisplayProgress("Saving listener configuration...", true);
                await SaveListenerConfiguration(baseConnectionString, selectedListeners);
                DisplayProgress("Listener configuration saved successfully.", true);
            }
            catch (Exception ex)
            {
                DisplayProgress($"Failed to save listener configuration: {ex.Message}", false);
                MessageBox.Show($"Failed to save listener configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ((Button)sender).IsEnabled = true;
                return;
            }

            // Create an IProgress object to report status to the UI
            var progress = new Progress<Tuple<string, bool>>(report =>
            {
                // Report status to the UI thread
                Dispatcher.Invoke(() => DisplayProgress(report.Item1, report.Item2));
            });

            try
            {
                bool success = await ServiceInstallerManager.ManageReplicationService(baseConnectionString, progress);

                if (success)
                {
                    MessageBox.Show("Sync service created and started successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Refresh configuration status
                    await LoadConfigurationStatus();
                }
                else
                {
                    MessageBox.Show("Service creation failed. Please check the application logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DisplayProgress($"Unexpected error: {ex.Message}", false);
                MessageBox.Show($"Unexpected error while creating the service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ((Button)sender).IsEnabled = true; // Re-enable the button
            }
        }

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

        private void PopulateListenerSelectionPanel()
        {
            ListenerSelectionPanel.Children.Clear();

            if (_availableListeners == null) return;

            foreach (var listener in _availableListeners.OrderBy(l => l.Priority))
            {
                var listenerPanel = CreateListenerPanel(listener);
                ListenerSelectionPanel.Children.Add(listenerPanel);
            }
        }

        private UIElement CreateListenerPanel(ListenerConfiguration listener)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 5),
                Background = Brushes.White
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Checkbox
            var checkbox = new CheckBox
            {
                IsChecked = listener.IsEnabled,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 8, 0)
            };
            checkbox.Checked += OnListenerSelectionChanged;
            checkbox.Unchecked += OnListenerSelectionChanged;
            Grid.SetColumn(checkbox, 0);

            // Content panel
            var contentPanel = new StackPanel();
            Grid.SetColumn(contentPanel, 1);

            // Name and priority
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var nameText = new TextBlock
            {
                Text = listener.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };
            var priorityText = new TextBlock
            {
                Text = $" (Priority: {listener.Priority})",
                Foreground = Brushes.Gray,
                FontSize = 12
            };
            namePanel.Children.Add(nameText);
            namePanel.Children.Add(priorityText);

            // Description
            var descriptionText = new TextBlock
            {
                Text = listener.Description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4),
                FontSize = 11
            };

            // Performance and reliability indicators
            var indicatorsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            
            var performanceText = new TextBlock
            {
                Text = $"Performance: {listener.PerformanceImpact}",
                Foreground = GetPerformanceColor(listener.PerformanceImpact),
                FontSize = 10,
                Margin = new Thickness(0, 0, 15, 0)
            };
            
            var reliabilityText = new TextBlock
            {
                Text = $"Reliability: {listener.ReliabilityLevel}",
                Foreground = GetReliabilityColor(listener.ReliabilityLevel),
                FontSize = 10
            };

            indicatorsPanel.Children.Add(performanceText);
            indicatorsPanel.Children.Add(reliabilityText);

            // Interval (for polling-based listeners)
            if (listener.Interval.HasValue)
            {
                var intervalText = new TextBlock
                {
                    Text = $"Interval: {listener.Interval.Value.TotalMinutes} minutes",
                    Foreground = Brushes.Blue,
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                contentPanel.Children.Add(intervalText);
            }

            contentPanel.Children.Add(namePanel);
            contentPanel.Children.Add(descriptionText);
            contentPanel.Children.Add(indicatorsPanel);

            grid.Children.Add(checkbox);
            grid.Children.Add(contentPanel);
            border.Child = grid;

            // Store the listener configuration in the checkbox's Tag property
            checkbox.Tag = listener;

            return border;
        }

        private Brush GetPerformanceColor(string performance)
        {
            return performance switch
            {
                "Very Low" => Brushes.DarkGreen,
                "Low" => Brushes.Green,
                "Medium" => Brushes.Orange,
                "High" => Brushes.Red,
                _ => Brushes.Gray
            };
        }

        private Brush GetReliabilityColor(string reliability)
        {
            return reliability switch
            {
                "Very High" => Brushes.DarkGreen,
                "High" => Brushes.Green,
                "Medium" => Brushes.Orange,
                "Low" => Brushes.Red,
                _ => Brushes.Gray
            };
        }

        private void OnListenerSelectionChanged(object sender, RoutedEventArgs e)
        {
            var selectedListeners = GetSelectedListeners();
            var warnings = ListenerCompatibilityChecker.CheckCompatibility(selectedListeners);

            if (warnings.Count > 0)
            {
                CompatibilityWarningBorder.Visibility = Visibility.Visible;
                CompatibilityWarningText.Text = string.Join("\n", warnings);
            }
            else
            {
                CompatibilityWarningBorder.Visibility = Visibility.Collapsed;
            }
        }

        private List<ListenerConfiguration> GetSelectedListeners()
        {
            var selectedListeners = new List<ListenerConfiguration>();

            foreach (var child in ListenerSelectionPanel.Children)
            {
                if (child is Border border && border.Child is Grid grid)
                {
                    var checkbox = grid.Children.OfType<CheckBox>().FirstOrDefault();
                    if (checkbox?.IsChecked == true && checkbox.Tag is ListenerConfiguration listener)
                    {
                        selectedListeners.Add(listener);
                    }
                }
            }

            return selectedListeners;
        }

        private void UpdateListenerSelection()
        {
            if (_currentConfigStatus?.SelectedListeners == null) return;

            foreach (var child in ListenerSelectionPanel.Children)
            {
                if (child is Border border && border.Child is Grid grid)
                {
                    var checkbox = grid.Children.OfType<CheckBox>().FirstOrDefault();
                    if (checkbox?.Tag is ListenerConfiguration listener)
                    {
                        checkbox.IsChecked = _currentConfigStatus.SelectedListeners.Any(l => l.Type == listener.Type);
                    }
                }
            }
        }



        private async Task SaveListenerConfiguration(string baseConnectionString, List<ListenerConfiguration> selectedListeners)
        {
            using (var connection = new SqlConnection(baseConnectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = @"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ReplicationConfig') AND name = 'SelectedListeners')
                        BEGIN
                            ALTER TABLE ReplicationConfig ADD SelectedListeners NVARCHAR(MAX)
                        END

                        UPDATE ReplicationConfig 
                        SET SelectedListeners = @SelectedListeners 
                        WHERE Id = (SELECT TOP 1 Id FROM ReplicationConfig ORDER BY Id DESC)";

                    var listenerTypes = string.Join(",", selectedListeners.Select(l => l.Type.ToString()));
                    command.Parameters.AddWithValue("@SelectedListeners", listenerTypes);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

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
    }
}