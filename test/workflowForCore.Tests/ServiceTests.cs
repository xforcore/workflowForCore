// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// ExternalDataExchange (CallExternalMethod and HandleExternalEvent through ExternalDataExchangeService),
// TransactionScopeActivity, compensation, and a persistence service of the host's own: unloading and loading instances
// through Activity.Save and Activity.Load. Each test gives the same result on .NET Framework 4.8.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Workflow.Activities;
using System.Workflow.Runtime;
using Xunit;

namespace WorkflowForCore.Tests
{
    public class ServiceTests
    {
        public ServiceTests() => Trace.Reset();

        static (Host Host, OrderService Orders) OrderHost(params object[] services)
        {
            var exchange = new ExternalDataExchangeService();
            var host = new Host(false, services.Concat(new object[] { exchange }).ToArray());
            var orders = new OrderService();
            exchange.AddService(orders);
            return (host, orders);
        }

        [Fact]
        public void CallExternalMethod_calls_the_service_with_its_bound_parameters()
        {
            var (host, orders) = OrderHost();
            using (host)
            {
                WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                Assert.Equal(new[] { "started 12.5" }, orders.Notifications);
                Assert.Equal("Idled", host.Events.Last());
                instance.Terminate("done");
            }
        }

        [Fact]
        public void HandleExternalEvent_receives_the_event_and_its_arguments()
        {
            var (host, orders) = OrderHost();
            using (host)
            {
                WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                orders.Approve(instance.InstanceId, 99.95m);
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { "approvedEvent 99.95" }, Trace.Steps);
                Assert.Equal(new[] { "started 12.5", "finished 99.95" }, orders.Notifications);
                Assert.Equal("Completed", host.Events.Last());
                Assert.Equal("approvedEvent 99.95", host.Completed.OutputParameters["Outcome"]);
            }
        }

        [Fact]
        public void The_other_branch_of_a_Listen_gets_the_other_event()
        {
            var (host, orders) = OrderHost();
            using (host)
            {
                WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                orders.Reject(instance.InstanceId);
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { "rejectedEvent 0" }, Trace.Steps);
            }
        }

        [Fact]
        public void An_event_for_an_unknown_instance_fails_to_be_delivered()
        {
            var (host, orders) = OrderHost();
            using (host)
            {
                host.Runtime.StartRuntime();
                var e = Assert.Throws<EventDeliveryFailedException>(() => orders.Approve(Guid.NewGuid(), 1));
                Assert.NotNull(e.Message);
            }
        }

        [Fact]
        public void The_exchange_service_refuses_a_service_without_an_ExternalDataExchange_interface()
        {
            using (var runtime = new WorkflowRuntime())
            {
                var exchange = new ExternalDataExchangeService();
                runtime.AddService(exchange);
                Assert.Throws<InvalidOperationException>(() => exchange.AddService(new object()));
            }
        }

        [Fact]
        public void TransactionScopeActivity_needs_a_persistence_service()
        {
            using (var host = new Host())
            {
                host.Run(typeof(TransactionWorkflow));
                Assert.StartsWith("Terminated: System.InvalidOperationException: ", host.Events.Last());
            }
        }

        [Fact]
        public void TransactionScopeActivity_runs_its_activities_in_a_transaction_and_commits_it()
        {
            var persistence = new MemoryPersistenceService();
            using (var host = new Host(false, persistence))
            {
                host.Run(typeof(TransactionWorkflow));
                Assert.Equal("Completed", host.Events.Last());
                Assert.Equal(new[] { "in Active", "after none" }, Trace.Steps);
                Assert.Equal(IsolationLevel.ReadCommitted, host.Completed.OutputParameters["Isolation"]);
                // The instance is saved when the atomic scope completes, and when it completes.
                Assert.True(persistence.Saves >= 1);
            }
        }

        [Fact]
        public void A_fault_handler_compensates_a_completed_compensatable_sequence()
        {
            using (var host = new Host())
            {
                host.Run(typeof(CompensationWorkflow));
                Assert.Equal("Completed", host.Events.Last());
                Assert.Equal(new[] { "firstWork", "secondWork", "fail", "firstUndo" }, Trace.Steps);
            }
        }

        [Fact]
        public void An_unhandled_fault_compensates_the_completed_sequences_last_first()
        {
            using (var host = new Host())
            {
                host.Run(typeof(UnhandledCompensationWorkflow));
                Assert.Equal("Terminated: System.ApplicationException: unhandled", host.Events.Last());
                Assert.Equal(new[] { "firstWork", "secondWork", "fail", "secondUndo", "firstUndo" }, Trace.Steps);
            }
        }

        [Fact]
        public void An_idle_instance_is_unloaded_and_loaded_again_by_the_persistence_service()
        {
            var persistence = new MemoryPersistenceService();
            var (host, orders) = OrderHost(persistence);
            using (host)
            {
                WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                instance.Unload();
                Assert.Contains("Unloaded", host.Events);
                Assert.True(persistence.Instances.ContainsKey(instance.InstanceId));
                Assert.Empty(host.Runtime.GetLoadedWorkflows());

                // The event loads it.
                orders.Approve(instance.InstanceId, 7);
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Contains("Loaded", host.Events);
                Assert.Equal("Completed", host.Events.Last());
                Assert.Equal(new[] { "started 12.5", "finished 7" }, orders.Notifications);
            }
        }

        [Fact]
        public void UnloadOnIdle_unloads_an_instance_when_it_waits_and_a_new_runtime_finishes_it()
        {
            var persistence = new MemoryPersistenceService(unloadOnIdle: true);
            Guid id;
            var (first, firstOrders) = OrderHost(persistence);
            using (first)
            {
                id = first.Run(typeof(OrderWorkflow)).InstanceId;
                Assert.Contains("Persisted", first.Events);
                Assert.Equal("Unloaded", first.Events.Last());
            }
            var (second, orders) = OrderHost(persistence);
            using (second)
            {
                second.Runtime.StartRuntime();
                WorkflowInstance instance = second.Runtime.GetWorkflow(id);
                orders.Approve(id, 3);
                second.Scheduler.RunWorkflow(id);
                Assert.Equal("Completed", second.Events.Last());
                Assert.Equal(new[] { "finished 3" }, orders.Notifications);
            }
        }

        [Fact]
        public void An_instance_suspended_and_unloaded_resumes_after_loading()
        {
            var persistence = new MemoryPersistenceService();
            using (var host = new Host(false, persistence))
            {
                WorkflowInstance instance = host.Run(typeof(SuspendingWorkflow));
                instance.Unload();
                WorkflowInstance loaded = host.Runtime.GetWorkflow(instance.InstanceId);
                loaded.Resume();
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { "before", "after" }, Trace.Steps);
                Assert.Equal("Completed", host.Events.Last());
            }
        }

        [Fact]
        public void GetWorkflowDefinition_is_the_definition_not_the_running_state()
        {
            var (host, orders) = OrderHost();
            using (host)
            {
                WorkflowInstance instance = host.Run(typeof(OrderWorkflow));
                orders.Approve(instance.InstanceId, 50);
                var definition = (OrderWorkflow)instance.GetWorkflowDefinition();
                Assert.Equal(12.5m, definition.Amount);
                Assert.Null(definition.Outcome);
                instance.Terminate("done");
            }
        }
    }
}
