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
                Dispatcher.Invoke(() =>
                {
                    StopRefreshAnimation();
                    RefreshServersButton.IsEnabled = true;
                    AbleFormControls(true);
                    UpdateStepButtons();
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


        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep < 4)
            {
                ShowStep(currentStep + 1);
            }
        }

        private void PreviousStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep > 1)
            {
                currentStep--;
                UpdateStepVisibility();
                UpdateStepButtons();
            }
        }

        private void UpdateStepVisibility()
        {
            Step1Panel.Visibility = currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStepButtons()
        {
            //Step1Button.IsEnabled = currentStep != 1;
            //Step2Button.IsEnabled = currentStep != 2;
            //Step3Button.IsEnabled = currentStep != 3;
            //Step4Button.IsEnabled = currentStep != 4;

            // Update button styles based on current step
            var buttons = new[] { Step1Button, Step2Button, Step3Button, Step4Button };
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

                if (currentStep < 4)
                {
                    ShowStep(currentStep + 1);
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

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            int stepNumber = button.Name switch
            {
                "Step1Button" => 1,
                "Step2Button" => 2,
                "Step3Button" => 3,
                "Step4Button" => 4,
                _ => 1
            };

            ShowStep(stepNumber);
        }

        private void ShowStep(int stepNumber)
        {
            // Hide all panels
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Collapsed;
            Step3Panel.Visibility = Visibility.Collapsed;
            Step4Panel.Visibility = Visibility.Collapsed;

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
                    break;
            }

            currentStep = stepNumber;
            UpdateStepButtons();
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

            // Get the base database connection string from the TextBox
            string baseConnectionString = connectionStrings["Base"];

            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                DisplayProgress("Base database connection string cannot be empty.", false);
                MessageBox.Show("Please enter the base database connection string.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Sync service management completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Service management operation failed. Please check the application logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DisplayProgress($"Unexpected error: {ex.Message}", false);
                MessageBox.Show($"Unexpected error while managing the service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

    }
}