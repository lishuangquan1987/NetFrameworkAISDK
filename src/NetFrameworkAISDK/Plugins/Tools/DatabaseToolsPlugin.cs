using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Text;

namespace NetFrameworkAISDK.Plugins.Tools
{
    /// <summary>
    /// 数据库工具插件，提供常用的数据库操作工具
    /// </summary>
    [Plugin("NetFrameworkAISDK.Plugins.Tools.DatabaseTools", "1.0.0")]
    [ToolProviderPlugin("Database")]
    public class DatabaseToolsPlugin : IToolProviderPlugin
    {
        public string Id { get { return "NetFrameworkAISDK.Plugins.Tools.DatabaseTools"; } }
        public string Name { get { return "Database Tools"; } }
        public string Version { get { return "1.0.0"; } }
        public string Description { get { return "Provides database query and manipulation tools"; } }
        public string Author { get { return "NetFrameworkAISDK"; } }
        public string Website { get { return ""; } }
        public string[] Dependencies { get { return new string[0]; } }
        public string ToolCategory { get { return "Database"; } }

        private string _connectionString;
        private string _providerName;

        public void Initialize(PluginConfig config)
        {
            if (config != null && config.Settings != null)
            {
                if (config.Settings.ContainsKey("connectionString"))
                {
                    _connectionString = config.Settings["connectionString"] as string;
                }
                if (config.Settings.ContainsKey("providerName"))
                {
                    _providerName = config.Settings["providerName"] as string;
                }
            }
        }

        public PluginValidationResult Validate()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                return PluginValidationResult.Failure("Connection string is not configured");
            }

            return PluginValidationResult.Success();
        }

        public IEnumerable<AIFunction> GetTools()
        {
            return new List<AIFunction>
            {
                CreateQueryTool(),
                CreateExecuteTool(),
                CreateGetTablesTool()
            };
        }

        public int GetToolCount()
        {
            return 3;
        }

        private AIFunction CreateQueryTool()
        {
            var method = typeof(DatabaseToolsPlugin).GetMethod("Query");
            return AIFunctionFactory.Create(method, this);
        }

        private AIFunction CreateExecuteTool()
        {
            var method = typeof(DatabaseToolsPlugin).GetMethod("ExecuteNonQuery");
            return AIFunctionFactory.Create(method, this);
        }

        private AIFunction CreateGetTablesTool()
        {
            var method = typeof(DatabaseToolsPlugin).GetMethod("GetTables");
            return AIFunctionFactory.Create(method, this);
        }

        private DbConnection GetConnection()
        {
            if (string.IsNullOrEmpty(_providerName))
            {
                _providerName = "System.Data.SqlClient";
            }

            var factory = DbProviderFactories.GetFactory(_providerName);
            var connection = factory.CreateConnection();
            if (connection != null)
            {
                connection.ConnectionString = _connectionString;
            }
            return connection;
        }

        [Description("Execute a SELECT query and return results as JSON")]
        public string Query(
            [Description("SQL SELECT query to execute")] string sql)
        {
            if (string.IsNullOrEmpty(sql))
            {
                return "Error: SQL query is required.";
            }

            if (!sql.Trim().ToUpper().StartsWith("SELECT"))
            {
                return "Error: Only SELECT queries are allowed.";
            }

            try
            {
                using (var connection = GetConnection())
                {
                    if (connection == null)
                    {
                        return "Error: Failed to create database connection.";
                    }

                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        command.CommandTimeout = 30;

                        using (var reader = command.ExecuteReader())
                        {
                            var results = new List<Dictionary<string, object>>();
                            var columns = new List<string>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columns.Add(reader.GetName(i));
                            }

                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var value = reader.GetValue(i);
                                    row[columns[i]] = value == DBNull.Value ? null : value;
                                }
                                results.Add(row);
                            }

                            return JsonHelper.Serialize(results);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "Error executing query: " + ex.Message;
            }
        }

        [Description("Execute INSERT, UPDATE, or DELETE statements")]
        public string ExecuteNonQuery(
            [Description("SQL INSERT/UPDATE/DELETE statement to execute")] string sql)
        {
            if (string.IsNullOrEmpty(sql))
            {
                return "Error: SQL statement is required.";
            }

            var upperSql = sql.Trim().ToUpper();
            if (!upperSql.StartsWith("INSERT") && 
                !upperSql.StartsWith("UPDATE") && 
                !upperSql.StartsWith("DELETE"))
            {
                return "Error: Only INSERT, UPDATE, and DELETE statements are allowed.";
            }

            try
            {
                using (var connection = GetConnection())
                {
                    if (connection == null)
                    {
                        return "Error: Failed to create database connection.";
                    }

                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        command.CommandTimeout = 30;

                        int affectedRows = command.ExecuteNonQuery();

                        return "Success. Affected rows: " + affectedRows;
                    }
                }
            }
            catch (Exception ex)
            {
                return "Error executing statement: " + ex.Message;
            }
        }

        [Description("Get list of all tables in the database")]
        public string GetTables(
            [Description("Database name (optional)")] string databaseName = null)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    if (connection == null)
                    {
                        return "Error: Failed to create database connection.";
                    }

                    connection.Open();

                    DataTable schema = connection.GetSchema("Tables");
                    var tables = new List<Dictionary<string, string>>();

                    foreach (DataRow row in schema.Rows)
                    {
                        tables.Add(new Dictionary<string, string>
                        {
                            { "TABLE_CATALOG", row["TABLE_CATALOG"]?.ToString() },
                            { "TABLE_SCHEMA", row["TABLE_SCHEMA"]?.ToString() },
                            { "TABLE_NAME", row["TABLE_NAME"]?.ToString() },
                            { "TABLE_TYPE", row["TABLE_TYPE"]?.ToString() }
                        });
                    }

                    return JsonHelper.Serialize(tables);
                }
            }
            catch (Exception ex)
            {
                return "Error getting tables: " + ex.Message;
            }
        }
    }
}
