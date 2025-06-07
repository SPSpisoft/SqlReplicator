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

namespace SqlReplicator
{
    public partial class MainWindow : MetroWindow
    {
        private int currentStep = 1;
        private readonly Dictionary<string, string> connectionStrings = new Dictionary<string, string>();
        private CancellationTokenSource? _refreshCancellation;

        public MainWindow()
        {
            InitializeComponent();
            RefreshServersButton.IsEnabled = false;
            StartRefreshAnimation();
            StatusLabel.Text = "Please wait while loading SQL Server instances...";
            _ = LoadSqlServerInstances();
        }

        private async Task LoadSqlServerInstances()
        {
            try
            {
                _refreshCancellation = new CancellationTokenSource();
                DisableFormControls();

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
                    EnableFormControls();
                    if (_refreshCancellation != null && !_refreshCancellation.Token.IsCancellationRequested)
                    {
                        StatusLabel.Text = "SQL Server instances loaded successfully. Please configure your connection settings.";
                    }
                });
            }
        }

        private void DisableFormControls()
        {
            BaseServerCombo.IsEnabled = false;
            BaseUsernameBox.IsEnabled = false;
            BasePasswordBox.IsEnabled = false;
            BaseDatabaseCombo.IsEnabled = false;
            BaseTestButton.IsEnabled = false;
            BaseNextButton.IsEnabled = false;

            SourceServerCombo.IsEnabled = false;
            SourceUsernameBox.IsEnabled = false;
            SourcePasswordBox.IsEnabled = false;
            SourceDatabaseCombo.IsEnabled = false;
            SourceTestButton.IsEnabled = false;
            SourceNextButton.IsEnabled = false;

            TargetServerCombo.IsEnabled = false;
            TargetUsernameBox.IsEnabled = false;
            TargetPasswordBox.IsEnabled = false;
            TargetDatabaseCombo.IsEnabled = false;
            TargetTestButton.IsEnabled = false;
            TargetCompleteButton.IsEnabled = false;
        }

        private void EnableFormControls()
        {
            BaseServerCombo.IsEnabled = true;
            BaseUsernameBox.IsEnabled = true;
            BasePasswordBox.IsEnabled = true;
            BaseDatabaseCombo.IsEnabled = true;
            BaseTestButton.IsEnabled = true;
            if (BaseStatusIcon.Visibility == Visibility.Visible)
                BaseNextButton.IsEnabled = true;

            SourceServerCombo.IsEnabled = true;
            SourceUsernameBox.IsEnabled = true;
            SourcePasswordBox.IsEnabled = true;
            SourceDatabaseCombo.IsEnabled = true;
            SourceTestButton.IsEnabled = true;
            if (SourceStatusIcon.Visibility == Visibility.Visible)
                SourceNextButton.IsEnabled = true;

            TargetServerCombo.IsEnabled = true;
            TargetUsernameBox.IsEnabled = true;
            TargetPasswordBox.IsEnabled = true;
            TargetDatabaseCombo.IsEnabled = true;
            TargetTestButton.IsEnabled = true;
            if (TargetStatusIcon.Visibility == Visibility.Visible)
                TargetCompleteButton.IsEnabled = true;
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
                    return;
                }

                button.IsEnabled = false;
                StartRefreshAnimation();
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
                            BaseStatusIcon.Visibility = Visibility.Visible;
                            BaseNextButton.IsEnabled = true;
                            break;
                        case "Source":
                            SourceStatusIcon.Visibility = Visibility.Visible;
                            SourceNextButton.IsEnabled = true;
                            break;
                        case "Target":
                            TargetStatusIcon.Visibility = Visibility.Visible;
                            TargetCompleteButton.IsEnabled = true;
                            break;
                    }

                    StatusLabel.Text = $"{step} database connection successful!";
                    MessageBox.Show($"Connection to {step.ToLower()} database successful!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
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
                var baseConnectionString = connectionStrings["Base"];
                if (string.IsNullOrEmpty(baseConnectionString) || string.IsNullOrEmpty(BaseDatabaseCombo.Text))
                {
                    ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                    StatusLabel.Text = "Please select a database.";
                    return;
                }

                baseConnectionString += $"Database={BaseDatabaseCombo.Text};";

                using (var connection = new SqlConnection(baseConnectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT COUNT(*) FROM ReplicationConfig", connection))
                    {
                        var configCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                        if (configCount > 0)
                        {
                            ConfigButtonsPanel.Visibility = Visibility.Visible;
                            StatusLabel.Text = "Existing configuration found. You can view or delete it.";
                        }
                        else
                        {
                            ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                            StatusLabel.Text = "No existing configuration found. Please complete the setup.";
                        }
                    }
                }
            }
            catch
            {
                // If table doesn't exist or any other error, hide the buttons
                ConfigButtonsPanel.Visibility = Visibility.Collapsed;
                StatusLabel.Text = "Please complete the setup.";
            }
        }

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep < 3)
            {
                currentStep++;
                UpdateStepVisibility();
                UpdateStepButtons();
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
        }

        private void UpdateStepButtons()
        {
            Step1Button.IsEnabled = currentStep != 1;
            Step2Button.IsEnabled = currentStep != 2;
            Step3Button.IsEnabled = currentStep != 3;

            switch (currentStep)
            {
                case 1:
                    StatusLabel.Text = "Please configure your base database connection";
                    break;
                case 2:
                    StatusLabel.Text = "Please configure your source database connection";
                    break;
                case 3:
                    StatusLabel.Text = "Please configure your target database connection";
                    break;
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

                MessageBox.Show("Connection strings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusLabel.Text = "Configuration completed successfully!";

                var configWindow = new ConfigurationWindow(baseConnectionString, sourceConnectionString, targetConnectionString);
                configWindow.Owner = this;
                configWindow.ShowDialog();
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

                                var deleteTableSelections = new SqlCommand(
                                    "DELETE FROM TableSelections", connection, transaction);
                                await deleteTableSelections.ExecuteNonQueryAsync();

                                // Delete from main config table
                                var deleteConfigs = new SqlCommand(
                                    "DELETE FROM ReplicationConfig", connection, transaction);
                                await deleteConfigs.ExecuteNonQueryAsync();

                                transaction.Commit();
                                MessageBox.Show("All configurations have been deleted successfully.", "Success",
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                                // Hide config buttons after successful deletion
                                ConfigButtonsPanel.Visibility = Visibility.Collapsed;
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

        private void Step1Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Step1Button.IsEnabled) return;
            ShowStep(1);
        }

        private void Step2Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Step2Button.IsEnabled) return;
            ShowStep(2);
        }

        private void Step3Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Step3Button.IsEnabled) return;
            ShowStep(3);
        }

        private void ConfigStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfigStepButton.IsEnabled) return;
            GoToConfig_Click(sender, e);
        }

        private void ShowStep(int stepNumber)
        {
            Step1Panel.Visibility = stepNumber == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = stepNumber == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = stepNumber == 3 ? Visibility.Visible : Visibility.Collapsed;

            Step1Button.IsEnabled = stepNumber != 1;
            Step2Button.IsEnabled = stepNumber != 2;
            Step3Button.IsEnabled = stepNumber != 3;
        }

        private void UpdateConfigButtonState()
        {
            ConfigStepButton.IsEnabled = ConfigButtonsPanel.Visibility == Visibility.Visible;
        }
    }
}