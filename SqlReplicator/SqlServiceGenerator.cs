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
        private readonly List<TableInfo> _selectedTables;

        public SqlServiceGenerator(string sourceConnectionString, string targetConnectionString, List<TableInfo> selectedTables)
        {
            _sourceConnectionString = sourceConnectionString ?? throw new ArgumentNullException(nameof(sourceConnectionString));
            _targetConnectionString = targetConnectionString ?? throw new ArgumentNullException(nameof(targetConnectionString));
            _selectedTables = selectedTables ?? throw new ArgumentNullException(nameof(selectedTables));
        }

        public async Task GenerateServices()
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

                        // Generate triggers for each table
                        foreach (var table in _selectedTables)
                        {
                            await GenerateTableTriggers(connection, transaction, table);
                        }

                        // Generate extraction procedures
                        await GenerateExtractionProcedures(connection, transaction);

                        // Generate application procedures
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

        private async Task GenerateTableTriggers(SqlConnection connection, SqlTransaction transaction, TableInfo table)
        {
            // Get primary key field
            var primaryKeyField = table.Fields.FirstOrDefault(f => f.IsPrimaryKey);
            if (primaryKeyField == null)
                throw new InvalidOperationException($"Table {table.TableName} does not have a primary key.");

            // Create trigger for INSERT
            var insertTrigger = new SqlCommand($@"
                CREATE OR ALTER TRIGGER [dbo].[TR_{table.TableName}_Insert]
                ON [dbo].[{table.TableName}]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    INSERT INTO ChangeTracking (TableName, OperationType, PrimaryKeyValue, ChangeData)
                    SELECT 
                        '{table.TableName}',
                        'I',
                        CAST(i.[{primaryKeyField.FieldName}] AS NVARCHAR(MAX)),
                        (
                            SELECT 
                                {string.Join(", ", table.Fields.Select(f => $"i.[{f.FieldName}] AS [{f.FieldName}]"))}
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        )
                    FROM inserted i;
                END", connection, transaction);

            // Create trigger for UPDATE
            var updateTrigger = new SqlCommand($@"
                CREATE OR ALTER TRIGGER [dbo].[TR_{table.TableName}_Update]
                ON [dbo].[{table.TableName}]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    INSERT INTO ChangeTracking (TableName, OperationType, PrimaryKeyValue, ChangeData)
                    SELECT 
                        '{table.TableName}',
                        'U',
                        CAST(i.[{primaryKeyField.FieldName}] AS NVARCHAR(MAX)),
                        (
                            SELECT 
                                {string.Join(", ", table.Fields.Select(f => $"i.[{f.FieldName}] AS [{f.FieldName}]"))}
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        )
                    FROM inserted i;
                END", connection, transaction);

            // Create trigger for DELETE
            var deleteTrigger = new SqlCommand($@"
                CREATE OR ALTER TRIGGER [dbo].[TR_{table.TableName}_Delete]
                ON [dbo].[{table.TableName}]
                AFTER DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    INSERT INTO ChangeTracking (TableName, OperationType, PrimaryKeyValue, ChangeData)
                    SELECT 
                        '{table.TableName}',
                        'D',
                        CAST(d.[{primaryKeyField.FieldName}] AS NVARCHAR(MAX)),
                        (
                            SELECT 
                                {string.Join(", ", table.Fields.Select(f => $"d.[{f.FieldName}] AS [{f.FieldName}]"))}
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