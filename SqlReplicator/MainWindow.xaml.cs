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

namespace SqlReplicator
{
    public partial class MainWindow : MetroWindow
    {
        private int currentStep = 1;
        private readonly Dictionary<string, string> connectionStrings = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();
            _ = LoadSqlServerInstances();
        }

        private async Task LoadSqlServerInstances()
        {
            StatusLabel.Text = "Refreshing SQL Server instances...";
            try
            {
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
                            var serverName = row["ServerName"].ToString();
                            var instanceName = row["InstanceName"].ToString();

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
                });
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Warning: Could not enumerate SQL Server instances: {ex.Message}";
            }
            StatusLabel.Text = "SQL Server instances refreshed";
        }

        private async void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Text = "Refreshing SQL Server instances...";
            await LoadSqlServerInstances();
            StatusLabel.Text = "SQL Server instances refreshed";
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
            button.Content = "Testing...";
            StatusLabel.Text = $"Testing {step.ToLower()} database connection...";

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
                button.Content = "Test";
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

        private void CompleteSetup_Click(object sender, RoutedEventArgs e)
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

            var result = $"Setup Complete!\n\n" +
                        $"Base Database Connection:\n{baseConnectionString}\n\n" +
                        $"Source Database Connection:\n{sourceConnectionString}\n\n" +
                        $"Target Database Connection:\n{targetConnectionString}";

            MessageBox.Show(result, "Configuration Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            // Here you can save the connection strings or proceed to next phase
            StatusLabel.Text = "Configuration completed successfully!";
        }
    }
}