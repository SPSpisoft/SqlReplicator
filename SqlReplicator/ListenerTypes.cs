using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq; // Added for .Any() and .Where()

namespace SqlReplicator
{
    public enum ListenerType
    {
        [Description("Trigger-based Change Detection")]
        Trigger,
        
        [Description("Change Data Capture (CDC)")]
        CDC,
        
        [Description("SQL Server Change Tracking")]
        ChangeTracking,
        
        [Description("Polling-based Monitoring")]
        Polling,
        
        [Description("Temporal Table Tracking")]
        Temporal
    }

    public class ListenerConfiguration
    {
        public ListenerType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int Priority { get; set; } // 1 = Highest, 5 = Lowest
        public TimeSpan? Interval { get; set; } // For polling-based listeners
        public bool IsCompatibleWith { get; set; } = true;
        public List<ListenerType> IncompatibleTypes { get; set; } = new List<ListenerType>();
        public string PerformanceImpact { get; set; } = "Low";
        public string ReliabilityLevel { get; set; } = "High";
        
        public static List<ListenerConfiguration> GetAvailableListeners()
        {
            return new List<ListenerConfiguration>
            {
                new ListenerConfiguration
                {
                    Type = ListenerType.Trigger,
                    Name = "Database Triggers",
                    Description = "Uses SQL Server triggers to capture INSERT, UPDATE, and DELETE operations in real-time. Provides immediate change detection with minimal latency.",
                    IsEnabled = true,
                    Priority = 1,
                    PerformanceImpact = "Medium",
                    ReliabilityLevel = "Very High",
                    IncompatibleTypes = new List<ListenerType> { ListenerType.CDC }
                },
                
                new ListenerConfiguration
                {
                    Type = ListenerType.CDC,
                    Name = "Change Data Capture",
                    Description = "SQL Server's built-in change tracking mechanism. Captures all changes to tracked tables and stores them in system tables. Requires CDC to be enabled on the database.",
                    IsEnabled = false,
                    Priority = 2,
                    PerformanceImpact = "Low",
                    ReliabilityLevel = "High",
                    IncompatibleTypes = new List<ListenerType> { ListenerType.Trigger }
                },
                
                new ListenerConfiguration
                {
                    Type = ListenerType.ChangeTracking,
                    Name = "Change Tracking",
                    Description = "Lightweight change tracking mechanism that tracks which rows have changed. Requires change tracking to be enabled on the database.",
                    IsEnabled = false,
                    Priority = 3,
                    PerformanceImpact = "Very Low",
                    ReliabilityLevel = "High",
                    IncompatibleTypes = new List<ListenerType> { }
                },
                
                new ListenerConfiguration
                {
                    Type = ListenerType.Polling,
                    Name = "Scheduled Polling",
                    Description = "Periodically checks for changes by comparing data snapshots or using timestamp columns. Configurable intervals for different performance requirements.",
                    IsEnabled = false,
                    Priority = 4,
                    Interval = TimeSpan.FromMinutes(5),
                    PerformanceImpact = "Medium",
                    ReliabilityLevel = "Medium",
                    IncompatibleTypes = new List<ListenerType> { }
                },
                
                new ListenerConfiguration
                {
                    Type = ListenerType.Temporal,
                    Name = "Temporal Table Tracking",
                    Description = "Uses SQL Server's temporal tables feature to track all historical changes. Provides complete audit trail and change history.",
                    IsEnabled = false,
                    Priority = 5,
                    PerformanceImpact = "Low",
                    ReliabilityLevel = "Very High",
                    IncompatibleTypes = new List<ListenerType> { }
                }
            };
        }
    }

    public class ListenerCompatibilityChecker
    {
        public static List<string> CheckCompatibility(List<ListenerConfiguration> selectedListeners)
        {
            var warnings = new List<string>();
            
            // Check for incompatible combinations
            foreach (var listener in selectedListeners)
            {
                foreach (var incompatibleType in listener.IncompatibleTypes)
                {
                    if (selectedListeners.Any(l => l.Type == incompatibleType))
                    {
                        warnings.Add($"⚠️ {listener.Name} is incompatible with {GetListenerName(incompatibleType)}. Consider using only one of them.");
                    }
                }
            }
            
            // Check for performance impact
            var highImpactListeners = selectedListeners.Where(l => l.PerformanceImpact == "High").ToList();
            if (highImpactListeners.Count > 2)
            {
                warnings.Add("⚠️ Multiple high-performance-impact listeners selected. This may affect system performance.");
            }
            
            // Check for redundancy
            if (selectedListeners.Count(l => l.Type == ListenerType.Trigger || l.Type == ListenerType.CDC) > 1)
            {
                warnings.Add("⚠️ Multiple real-time listeners detected. This may cause duplicate processing.");
            }
            
            return warnings;
        }
        
        private static string GetListenerName(ListenerType type)
        {
            return type switch
            {
                ListenerType.Trigger => "Database Triggers",
                ListenerType.CDC => "Change Data Capture",
                ListenerType.ChangeTracking => "Change Tracking",
                ListenerType.Polling => "Scheduled Polling",
                ListenerType.Temporal => "Temporal Table Tracking",
                _ => type.ToString()
            };
        }
    }
} 