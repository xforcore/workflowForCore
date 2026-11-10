// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WorkflowForCore.Tests
{
    // Tests that need SQL Server: $WORKFLOWFORCORE_SQLSERVER is a connection string to a server where the tests may
    // create and drop databases (CI starts one on Linux and on Windows, docs/TESTING.md). Without it they are skipped.
    public sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute()
        {
            if (string.IsNullOrEmpty(SqlServer.Server))
                Skip = "WORKFLOWFORCORE_SQLSERVER isn't set";
        }
    }

    // A database of the test's own, with .NET Framework 4.8's scripts run in it as sqlcmd runs them.
    public sealed class SqlServer : IDisposable
    {
        public static string Server => Environment.GetEnvironmentVariable("WORKFLOWFORCORE_SQLSERVER");

        public readonly string Database = "workflowForCore_" + Guid.NewGuid().ToString("N");
        public readonly string ConnectionString;

        public SqlServer(params string[] scripts)
        {
            Execute(Server, "CREATE DATABASE [" + Database + "]");
            ConnectionString = new SqlConnectionStringBuilder(Server) { InitialCatalog = Database }.ConnectionString;
            foreach (string script in scripts)
            {
                string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sql", script), Encoding.Unicode);
                foreach (string batch in Regex.Split(text, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (batch.Trim().Length > 0)
                        Execute(ConnectionString, batch);
                }
            }
        }

        public object Scalar(string sql)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                    return command.ExecuteScalar();
            }
        }

        static void Execute(string connectionString, string sql)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection) { CommandTimeout = 300 })
                    command.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            SqlConnection.ClearAllPools();
            Execute(Server, "ALTER DATABASE [" + Database + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + Database + "]");
        }
    }
}
