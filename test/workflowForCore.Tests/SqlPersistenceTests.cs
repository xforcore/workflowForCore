// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// SqlWorkflowPersistenceService on System.Data.SqlClient, in a database made with .NET Framework 4.8's own scripts
// (SqlPersistenceService_Schema.sql and _Logic.sql). The same on .NET Framework 4.8.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Workflow.Activities;
using System.Workflow.Runtime;
using System.Workflow.Runtime.Hosting;
using Xunit;

namespace WorkflowForCore.Tests
{
    public class SqlPersistenceTests
    {
        public SqlPersistenceTests() => Trace.Reset();

        static readonly string[] Scripts = { "SqlPersistenceService_Schema.sql", "SqlPersistenceService_Logic.sql" };

        static (Host Host, OrderService Orders) OrderHost(string connectionString)
        {
            var persistence = new SqlWorkflowPersistenceService(connectionString, true, TimeSpan.MaxValue, TimeSpan.Zero);
            var exchange = new ExternalDataExchangeService();
            var host = new Host(false, persistence, exchange);
            var orders = new OrderService();
            exchange.AddService(orders);
            return (host, orders);
        }

        [SqlFact]
        public void An_idle_instance_is_stored_in_the_database_and_removed_when_it_completes()
        {
            using (var sql = new SqlServer(Scripts))
            {
                var (host, orders) = OrderHost(sql.ConnectionString);
                using (host)
                {
                    WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                    Assert.Equal(new[] { "Created", "Started", "Idled", "Persisted", "Unloaded" }, host.Events);
                    Assert.Equal(1, sql.Scalar("SELECT COUNT(*) FROM InstanceState"));
                    Assert.Equal(instance.InstanceId, sql.Scalar("SELECT uidInstanceID FROM InstanceState"));

                    orders.Approve(instance.InstanceId, 4);
                    host.Scheduler.RunWorkflow(instance.InstanceId);
                    Assert.Equal("Completed", host.Events.Last());
                    Assert.Equal(new[] { "started 12.5", "finished 4" }, orders.Notifications);
                    Assert.Equal(0, sql.Scalar("SELECT COUNT(*) FROM InstanceState"));
                }
            }
        }

        [SqlFact]
        public void Another_runtime_loads_and_finishes_the_instance()
        {
            using (var sql = new SqlServer(Scripts))
            {
                Guid id;
                var (first, _) = OrderHost(sql.ConnectionString);
                using (first)
                    id = first.Run(typeof(OrderWorkflow)).InstanceId;

                var (second, orders) = OrderHost(sql.ConnectionString);
                using (second)
                {
                    second.Runtime.StartRuntime();
                    var persistence = second.Runtime.GetService<SqlWorkflowPersistenceService>();
                    SqlPersistenceWorkflowInstanceDescription description = persistence.GetAllWorkflows().Single();
                    Assert.Equal(id, description.WorkflowInstanceId);
                    Assert.Equal(WorkflowStatus.Running, description.Status);
                    // Blocked: idle, waiting for an event (no timer due).
                    Assert.True(description.IsBlocked);

                    orders.Reject(id);
                    second.Scheduler.RunWorkflow(id);
                    Assert.Equal("Completed", second.Events.Last());
                    Assert.Equal(new[] { "rejectedEvent 0" }, Trace.Steps);
                    Assert.Empty(persistence.GetAllWorkflows());
                }
            }
        }

        [SqlFact]
        public void A_terminated_instance_is_removed()
        {
            using (var sql = new SqlServer(Scripts))
            {
                var (host, _) = OrderHost(sql.ConnectionString);
                using (host)
                {
                    WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                    host.Runtime.GetWorkflow(instance.InstanceId).Terminate("stop");
                    Assert.StartsWith("Terminated", host.Events.Last());
                    Assert.Equal(0, sql.Scalar("SELECT COUNT(*) FROM InstanceState"));
                }
            }
        }

        [SqlFact]
        public void An_atomic_scope_is_persisted_and_committed_with_the_database()
        {
            using (var sql = new SqlServer(Scripts))
            {
                var persistence = new SqlWorkflowPersistenceService(sql.ConnectionString);
                using (var host = new Host(false, persistence))
                {
                    host.Run(typeof(TransactionWorkflow));
                    Assert.Equal("Completed", host.Events.Last());
                    Assert.Contains("Persisted", host.Events);
                    Assert.Equal(new[] { "in Active", "after none" }, Trace.Steps);
                }
            }
        }

#if !NETFRAMEWORK48
        [SqlFact]
        public void Workflowforcore_sql_creates_the_persistence_database()
        {
            using (var sql = new SqlServer())
            {
                WorkflowForCore.Sql.SqlScripts.Run(sql.ConnectionString, WorkflowForCore.Sql.SqlScripts.Persistence);
                Assert.Equal(0, sql.Scalar("SELECT COUNT(*) FROM InstanceState"));
                Assert.Equal(1, sql.Scalar("SELECT COUNT(*) FROM sys.procedures WHERE name = 'InsertInstanceState'"));
            }
        }
#endif
    }
}
