// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// WorkflowRuntime and WorkflowInstance: starting and stopping the runtime, creating and running instances, their
// parameters and events, terminating, suspending and resuming, and validation. Each test gives the same result on
// .NET Framework 4.8 (CI's Windows runner runs the net48 build).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Workflow.ComponentModel.Compiler;
using System.Workflow.Runtime;
using System.Workflow.Runtime.Hosting;
using Xunit;

namespace WorkflowForCore.Tests
{
    public class RuntimeTests
    {
        public RuntimeTests() => Trace.Reset();

        [Fact]
        public void Runtime_starts_and_stops_with_its_events()
        {
            var events = new List<string>();
            using (var runtime = new WorkflowRuntime())
            {
                runtime.Started += (s, e) => events.Add("Started " + e.IsStarted);
                runtime.Stopped += (s, e) => events.Add("Stopped " + e.IsStarted);
                Assert.False(runtime.IsStarted);
                runtime.StartRuntime();
                Assert.True(runtime.IsStarted);
                runtime.StopRuntime();
                Assert.False(runtime.IsStarted);
            }
            Assert.Equal(new[] { "Started True", "Stopped False" }, events);
        }

        [Fact]
        public void CreateWorkflow_starts_the_runtime_and_the_instance_has_its_id()
        {
            using (var host = new Host())
            {
                Guid id = Guid.NewGuid();
                WorkflowInstance instance = host.Runtime.CreateWorkflow(typeof(EmptyWorkflow), null, id);
                Assert.True(host.Runtime.IsStarted);
                Assert.Equal(id, instance.InstanceId);
                Assert.Same(host.Runtime, instance.WorkflowRuntime);
                Assert.Equal(typeof(EmptyWorkflow), instance.GetWorkflowDefinition().GetType());
            }
        }

        [Fact]
        public void A_sequential_workflow_runs_its_activities_with_parameters_in_and_out()
        {
            using (var host = new Host())
            {
                host.Run(typeof(GreetingWorkflow), new Dictionary<string, object> { { "Who", "WF3" } });
                Assert.Equal(new[] { "Created", "Started", "Completed" }, host.Events);
                Assert.Equal(new[] { "greet", "shout" }, Trace.Steps);
                Assert.Equal("HELLO, WF3", host.Completed.OutputParameters["Greeting"]);
                Assert.Equal("WF3", host.Completed.OutputParameters["Who"]);
                Assert.Equal(typeof(GreetingWorkflow), host.Completed.WorkflowDefinition.GetType());
            }
        }

        [Fact]
        public void An_unknown_parameter_is_refused()
        {
            using (var host = new Host())
            {
                var e = Assert.Throws<ArgumentException>(() => host.Runtime.CreateWorkflow(typeof(GreetingWorkflow), new Dictionary<string, object> { { "Nope", 1 } }));
                Assert.Contains("Nope", e.Message);
            }
        }

        [Fact]
        public void Activities_added_without_a_name_are_named_by_their_type()
        {
            using (var host = new Host())
            {
                host.Run(typeof(UnnamedWorkflow));
                Assert.Equal(new[] { "codeActivity1", "codeActivity2", "codeActivity3" }, Trace.Steps);
            }
        }

        [Fact]
        public void An_exception_in_an_activity_terminates_the_instance_with_it()
        {
            using (var host = new Host())
            {
                host.Run(typeof(ThrowingWorkflow));
                Assert.Equal("Terminated: System.InvalidOperationException: the code activity failed", host.Events.Last());
                Assert.IsType<InvalidOperationException>(host.Terminated.Exception);
            }
        }

        [Fact]
        public void TerminateActivity_terminates_with_its_error()
        {
            using (var host = new Host())
            {
                host.Run(typeof(TerminatingWorkflow));
                Assert.Equal("Terminated: System.Workflow.ComponentModel.WorkflowTerminatedException: stopped by the workflow", host.Events.Last());
                Assert.Empty(Trace.Steps);
            }
        }

        [Fact]
        public void The_host_terminates_an_instance()
        {
            using (var host = new Host())
            {
                WorkflowInstance instance = host.Runtime.CreateWorkflow(typeof(DelayWorkflow));
                instance.Start();
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal("Idled", host.Events.Last());
                instance.Terminate("the host stopped it");
                Assert.Equal("Terminated: System.Workflow.ComponentModel.WorkflowTerminatedException: the host stopped it", host.Events.Last());
                Assert.Empty(Trace.Steps);
            }
        }

        [Fact]
        public void SuspendActivity_suspends_and_Resume_continues()
        {
            using (var host = new Host())
            {
                WorkflowInstance instance = host.Run(typeof(SuspendingWorkflow));
                Assert.Equal(new[] { "before" }, Trace.Steps);
                Assert.Equal("Suspended: waiting for an operator", host.Events.Last());
                instance.Resume();
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { "before", "after" }, Trace.Steps);
                Assert.Equal(new[] { "Created", "Started", "Suspended: waiting for an operator", "Resumed", "Completed" }, host.Events);
            }
        }

        [Fact]
        public void The_host_suspends_and_resumes_an_idle_instance()
        {
            using (var host = new Host())
            {
                WorkflowInstance instance = host.Runtime.CreateWorkflow(typeof(DelayWorkflow));
                instance.Start();
                host.Scheduler.RunWorkflow(instance.InstanceId);
                instance.Suspend("paused by the host");
                Assert.Equal("Suspended: paused by the host", host.Events.Last());
                instance.Resume();
                Assert.Equal("Resumed", host.Events.Last());
            }
        }

        [Fact]
        public void A_workflow_that_doesnt_validate_isnt_created()
        {
            using (var host = new Host())
            {
                var e = Assert.Throws<WorkflowValidationFailedException>(() => host.Runtime.CreateWorkflow(typeof(InvalidWorkflow)));
                Assert.NotEmpty(e.Errors);
                Assert.Contains(e.Errors.Cast<ValidationError>(), error => !error.IsWarning);
            }
        }

        [Fact]
        public void GetWorkflow_of_an_unknown_instance_throws_without_a_persistence_service()
        {
            using (var host = new Host())
            {
                host.Runtime.StartRuntime();
                Assert.Throws<InvalidOperationException>(() => host.Runtime.GetWorkflow(Guid.NewGuid()));
            }
        }

        [Fact]
        public void Loaded_workflows_are_listed_until_they_complete()
        {
            using (var host = new Host())
            {
                WorkflowInstance instance = host.Runtime.CreateWorkflow(typeof(DelayWorkflow));
                instance.Start();
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { instance.InstanceId }, host.Runtime.GetLoadedWorkflows().Select(w => w.InstanceId));
                Assert.Equal(instance.InstanceId, host.Runtime.GetWorkflow(instance.InstanceId).InstanceId);
                instance.Terminate("done");
                Assert.Empty(host.Runtime.GetLoadedWorkflows());
            }
        }

        [Fact]
        public void The_manual_scheduler_runs_instances_on_the_callers_thread()
        {
            using (var host = new Host())
            {
                int thread = -1;
                host.Runtime.WorkflowCompleted += (s, e) => thread = Thread.CurrentThread.ManagedThreadId;
                host.Run(typeof(GreetingWorkflow), new Dictionary<string, object> { { "Who", "x" } });
                Assert.Equal(Thread.CurrentThread.ManagedThreadId, thread);
            }
        }

        [Fact]
        public void The_manual_scheduler_does_nothing_until_it_is_asked()
        {
            using (var host = new Host())
            {
                WorkflowInstance instance = host.Runtime.CreateWorkflow(typeof(GreetingWorkflow), new Dictionary<string, object> { { "Who", "x" } });
                instance.Start();
                Assert.Empty(Trace.Steps);
                Assert.Equal(new[] { "Created", "Started" }, host.Events);
                Assert.True(host.Scheduler.RunWorkflow(instance.InstanceId));
                Assert.Equal(new[] { "greet", "shout" }, Trace.Steps);
            }
        }

        [Fact]
        public void The_default_scheduler_runs_instances_on_thread_pool_threads()
        {
            using (var runtime = new WorkflowRuntime())
            {
                var done = new ManualResetEventSlim();
                int thread = -1;
                runtime.WorkflowCompleted += (s, e) => { thread = Thread.CurrentThread.ManagedThreadId; done.Set(); };
                runtime.CreateWorkflow(typeof(GreetingWorkflow), new Dictionary<string, object> { { "Who", "x" } }).Start();
                Assert.True(done.Wait(TimeSpan.FromSeconds(30)));
                Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, thread);
                Assert.IsType<DefaultWorkflowSchedulerService>(runtime.GetService<WorkflowSchedulerService>());
            }
        }

        [Fact]
        public void The_default_scheduler_runs_several_instances()
        {
            using (var runtime = new WorkflowRuntime())
            {
                int completed = 0;
                var done = new CountdownEvent(10);
                runtime.WorkflowCompleted += (s, e) => { Interlocked.Increment(ref completed); done.Signal(); };
                for (int i = 0; i < 10; i++)
                    runtime.CreateWorkflow(typeof(GreetingWorkflow), new Dictionary<string, object> { { "Who", "n" + i } }).Start();
                Assert.True(done.Wait(TimeSpan.FromSeconds(30)));
                Assert.Equal(10, completed);
            }
        }

        [Fact]
        public void A_service_can_be_added_only_once_and_not_while_started()
        {
            using (var runtime = new WorkflowRuntime())
            {
                var scheduler = new ManualWorkflowSchedulerService();
                runtime.AddService(scheduler);
                Assert.Throws<InvalidOperationException>(() => runtime.AddService(scheduler));
                Assert.Same(scheduler, runtime.GetService(typeof(ManualWorkflowSchedulerService)));
                runtime.StartRuntime();
                Assert.Throws<InvalidOperationException>(() => runtime.AddService(new DefaultWorkflowSchedulerService()));
            }
        }
    }
}
