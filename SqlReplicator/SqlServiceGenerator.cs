using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SqlReplicator
{
    public class SqlServiceGenerator
    {
        private readonly string _sourceConnectionString;
        private readonly string _targetConnectionString;
        private readonly List<FieldMapping> _fieldMappings;
        private readonly string _listenerType;

        public SqlServiceGenerator(string sourceConnectionString, string targetConnectionString, List<FieldMapping> fieldMappings, string listenerType)
        {
            _sourceConnectionString = sourceConnectionString ?? throw new ArgumentNullException(nameof(sourceConnectionString));
            _targetConnectionString = targetConnectionString ?? throw new ArgumentNullException(nameof(targetConnectionString));
            _fieldMappings = fieldMappings ?? throw new ArgumentNullException(nameof(fieldMappings));
            _listenerType = listenerType ?? "Trigger";
        }

        public async Task GenerateServices()
        {
            // 1. پاک کردن setup قبلی
            await CleanupPreviousSetup();
            
            // 2. جنریت setup جدید
            if (_listenerType == "Trigger")
            {
                using (var connection = new SqlConnection(_sourceConnectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Create change tracking table
                            await CreateChangeTrackingTable(connection, transaction);
                            // استخراج جداول یکتا از FieldMappings فقط برای فیلدهای انتخاب‌شده
                            var targetTables = _fieldMappings.Select(f => f.SourceTableName).Distinct().ToList();
                            foreach (var tableName in targetTables)
                            {
                                var tableFields = _fieldMappings.Where(f => f.SourceTableName == tableName).ToList();
                                await GenerateTableTriggers(connection, transaction, tableName, tableFields);
                            }
                            await GenerateExtractionProcedures(connection, transaction);
                            await GenerateApplicationProcedures(connection, transaction);
                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                // 3. کپی اولیه داده‌ها
                await PerformInitialDataSync();
            }
            else if (_listenerType == "Polling")
            {
                throw new NotImplementedException("Polling listener is not implemented yet.");
            }
            else if (_listenerType == "CDC" || _listenerType == "Change Tracking")
            {
                throw new NotImplementedException($"{_listenerType} listener is not implemented yet.");
            }
            else
            {
                throw new ArgumentException($"Unknown listener type: {_listenerType}");
            }
        }

        public async Task CleanupPreviousSetup()
        {
            using (var connection = new SqlConnection(_sourceConnectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. حذف Triggerهای قدیمی فقط برای جداول انتخاب‌شده
                        foreach (var table in _fieldMappings.Where(f => f.IsSelected).Select(f => f.TargetTableName).Distinct().ToList())
                        {
                            var tableFields = _fieldMappings.Where(f => f.TargetTableName == table && f.IsSelected).ToList();
                            await DropTableTriggers(connection, transaction, table, tableFields);
                        }

                        // 2. حذف Stored Procedureهای قدیمی
                        await DropStoredProcedures(connection, transaction);

                        // 3. پاک کردن جدول ChangeTracking
                        await CleanChangeTrackingTable(connection, transaction);

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private async Task DropTableTriggers(SqlConnection connection, SqlTransaction transaction, string tableName, List<FieldMapping> tableFields)
        {
            var triggers = new[] { "Insert", "Update", "Delete" };
            foreach (var triggerType in triggers)
            {
                var dropTrigger = new SqlCommand($@"
                    IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_{tableName}_{triggerType}')
                        DROP TRIGGER [dbo].[TR_{tableName}_{triggerType}]", connection, transaction);
                await dropTrigger.ExecuteNonQueryAsync();
            }
        }

        private async Task DropStoredProcedures(SqlConnection connection, SqlTransaction transaction)
        {
            var procedures = new[] { "GetPendingChanges", "ApplyChanges" };
            foreach (var procName in procedures)
            {
                var dropProc = new SqlCommand($@"
                    IF EXISTS (SELECT * FROM sys.procedures WHERE name = '{procName}')
                        DROP PROCEDURE [dbo].[{procName}]", connection, transaction);
                await dropProc.ExecuteNonQueryAsync();
            }
        }

        private async Task CleanChangeTrackingTable(SqlConnection connection, SqlTransaction transaction)
        {
            var cleanTable = new SqlCommand(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ChangeTracking')
                    TRUNCATE TABLE ChangeTracking", connection, transaction);
            await cleanTable.ExecuteNonQueryAsync();
        }

        private async Task PerformInitialDataSync()
        {
            foreach (var table in _fieldMappings.Where(f => f.IsSelected).Select(f => f.TargetTableName).Distinct().ToList())
            {
                var tableFields = _fieldMappings.Where(f => f.TargetTableName == table && f.IsSelected).ToList();
                await CopyTableData(table, tableFields);
            }
        }

        private async Task CopyTableData(string tableName, List<FieldMapping> tableFields)
        {
            // اتصال به دیتابیس مبدا
            using (var sourceConnection = new SqlConnection(_sourceConnectionString))
            {
                await sourceConnection.OpenAsync();
                
                // اتصال به دیتابیس مقصد
                using (var targetConnection = new SqlConnection(_targetConnectionString))
                {
                    await targetConnection.OpenAsync();
                    
                    // پاک کردن داده‌های موجود در جدول مقصد
                    var clearTargetTable = new SqlCommand($"DELETE FROM [{tableName}]", targetConnection);
                    await clearTargetTable.ExecuteNonQueryAsync();
                    
                    // خواندن داده‌ها از مبدا
                    var selectFields = string.Join(", ", tableFields.Select(f => $"[{f.TargetField}]"));
                    var selectCommand = new SqlCommand($"SELECT {selectFields} FROM [{tableName}]", sourceConnection);
                    
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // ساخت INSERT statement برای مقصد
                            var insertFields = string.Join(", ", tableFields.Select(f => $"[{f.TargetField}]"));
                            var insertValues = string.Join(", ", tableFields.Select(f => $"@{f.TargetField}"));
                            var insertCommand = new SqlCommand($"INSERT INTO [{tableName}] ({insertFields}) VALUES ({insertValues})", targetConnection);
                            
                            // اضافه کردن پارامترها
                            for (int i = 0; i < tableFields.Count; i++)
                            {
                                var field = tableFields[i];
                                var value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                                insertCommand.Parameters.AddWithValue($"@{field.TargetField}", value);
                            }
                            
                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        private async Task CreateChangeTrackingTable(SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChangeTracking')
                CREATE TABLE ChangeTracking (
                    ChangeId BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TableName NVARCHAR(128) NOT NULL,
                    OperationType CHAR(1) NOT NULL, -- 'I' for Insert, 'U' for Update, 'D' for Delete
                    PrimaryKeyValue NVARCHAR(MAX) NOT NULL,
                    ChangeData NVARCHAR(MAX) NOT NULL, -- JSON format
                    ChangeDate DATETIME DEFAULT GETDATE(),
                    IsProcessed BIT DEFAULT 0,
                    ProcessedDate DATETIME NULL,
                    ErrorMessage NVARCHAR(MAX) NULL
                )", connection, transaction);

            await command.ExecuteNonQueryAsync();
        }

        private async Task GenerateTableTriggers(SqlConnection connection, SqlTransaction transaction, string tableName, List<FieldMapping> tableFields)
        {
            // Get primary key field
            var primaryKeyMapping = tableFields.FirstOrDefault(f => f.IsPrimaryKey);
            if (primaryKeyMapping == null)
                throw new InvalidOperationException($"Table {tableName} does not have a primary key mapping.");
            var primaryKeyField = primaryKeyMapping.SourceField;
            // Create trigger for INSERT
            var insertTrigger = new SqlCommand($@"
                CREATE OR ALTER TRIGGER [dbo].[TR_{tableName}_Insert]
                ON [dbo].[{tableName}]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    INSERT INTO ChangeTracking (TableName, OperationType, PrimaryKeyValue, ChangeData)
                    SELECT 
                        '{tableName}',
                        'I',
                        CAST(i.[{primaryKeyField}] AS NVARCHAR(MAX)),
                        (
                            SELECT 
                                {string.Join(", ", tableFields.Select(f => $"i.[{f.SourceField}] AS [{f.SourceField}]") )}
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        )
                    FROM inserted i;
                END", connection, transaction);
            // Create trigger for UPDATE
            var updateTrigger = new SqlCommand($@"
                CREATE OR ALTER TRIGGER [dbo].[TR_{tableName}_Update]
                ON [dbo].[{tableName}]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    INSERT INTO ChangeTracking (TableName, OperationType, PrimaryKeyValue, ChangeData)
                    SELECT 
                        '{tableName}',
                        'U',
                        CAST(i.[{primaryKeyField}] AS NVARCHAR(MAX)),
                        (
                            SELECT 
                                {string.Join(", ", tableFields.Select(f => $"i.[{f.SourceField}] AS [{f.SourceField}]") )}
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        )
                    FROM inserted i;
                END", connection, transaction);
            // Create trigger for DELETE
            var deleteTrigger = new SqlCommand($@"
                CREATE OR ALTER TRIGGER [dbo].[TR_{tableName}_Delete]
                ON [dbo].[{tableName}]
                AFTER DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    INSERT INTO ChangeTracking (TableName, OperationType, PrimaryKeyValue, ChangeData)
                    SELECT 
                        '{tableName}',
                        'D',
                        CAST(d.[{primaryKeyField}] AS NVARCHAR(MAX)),
                        (
                            SELECT 
                                {string.Join(", ", tableFields.Select(f => $"d.[{f.SourceField}] AS [{f.SourceField}]") )}
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        )
                    FROM deleted d;
                END", connection, transaction);
            await insertTrigger.ExecuteNonQueryAsync();
            await updateTrigger.ExecuteNonQueryAsync();
            await deleteTrigger.ExecuteNonQueryAsync();
        }

        private async Task GenerateExtractionProcedures(SqlConnection connection, SqlTransaction transaction)
        {
            // Create procedure to get pending changes
            var getPendingChanges = new SqlCommand(@"
                CREATE OR ALTER PROCEDURE [dbo].[GetPendingChanges]
                    @BatchSize INT = 1000
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @Changes TABLE (
                        ChangeId BIGINT,
                        TableName NVARCHAR(128),
                        OperationType CHAR(1),
                        PrimaryKeyValue NVARCHAR(MAX),
                        ChangeData NVARCHAR(MAX)
                    );
                    
                    -- Get pending changes
                    INSERT INTO @Changes
                    SELECT TOP (@BatchSize)
                        ChangeId,
                        TableName,
                        OperationType,
                        PrimaryKeyValue,
                        ChangeData
                    FROM ChangeTracking
                    WHERE IsProcessed = 0
                    ORDER BY ChangeId;
                    
                    -- Mark changes as processed
                    UPDATE ct
                    SET 
                        IsProcessed = 1,
                        ProcessedDate = GETDATE()
                    FROM ChangeTracking ct
                    INNER JOIN @Changes c ON ct.ChangeId = c.ChangeId;
                    
                    -- Return changes
                    SELECT * FROM @Changes;
                END", connection, transaction);

            await getPendingChanges.ExecuteNonQueryAsync();
        }

        private async Task GenerateApplicationProcedures(SqlConnection connection, SqlTransaction transaction)
        {
            // Create procedure to apply changes
            var applyChanges = new SqlCommand(@"
                CREATE OR ALTER PROCEDURE [dbo].[ApplyChanges]
                    @TableName NVARCHAR(128),
                    @OperationType CHAR(1),
                    @PrimaryKeyValue NVARCHAR(MAX),
                    @ChangeData NVARCHAR(MAX)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @SQL NVARCHAR(MAX);
                    
                    IF @OperationType = 'I'
                    BEGIN
                        -- Insert operation
                        SELECT @SQL = 'INSERT INTO ' + @TableName + ' (' +
                            STRING_AGG(QUOTENAME([key]), ',') + ') VALUES (' +
                            STRING_AGG('@' + [key], ',') + ')'
                        FROM OPENJSON(@ChangeData);
                        
                        -- Execute dynamic SQL
                        EXEC sp_executesql @SQL, @ChangeData;
                    END
                    ELSE IF @OperationType = 'U'
                    BEGIN
                        -- Update operation
                        SELECT @SQL = 'UPDATE ' + @TableName + ' SET ' +
                            STRING_AGG(QUOTENAME([key]) + ' = @' + [key], ',')
                        FROM OPENJSON(@ChangeData)
                        WHERE [key] != @PrimaryKeyValue;
                        
                        -- Add WHERE clause
                        SET @SQL = @SQL + ' WHERE ' + @PrimaryKeyValue + ' = @' + @PrimaryKeyValue;
                        
                        -- Execute dynamic SQL
                        EXEC sp_executesql @SQL, @ChangeData;
                    END
                    ELSE IF @OperationType = 'D'
                    BEGIN
                        -- Delete operation
                        SET @SQL = 'DELETE FROM ' + @TableName + 
                                 ' WHERE ' + @PrimaryKeyValue + ' = @' + @PrimaryKeyValue;
                        
                        -- Execute dynamic SQL
                        EXEC sp_executesql @SQL, @ChangeData;
                    END
                END", connection, transaction);

            await applyChanges.ExecuteNonQueryAsync();
        }
    }
} 