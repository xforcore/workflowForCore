// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// The base activities in sequential and state-machine workflows defined in code: Code, IfElse, While, Parallel,
// Listen, Delay, EventDriven, Throw and fault handlers, State, StateInitialization, StateFinalization and SetState.
// Each test gives the same result on .NET Framework 4.8.
using System;
using System.Linq;
using System.Workflow.Activities;
using System.Workflow.Runtime;
using Xunit;

namespace WorkflowForCore.Tests
{
    public class ActivityTests
    {
        public ActivityTests() => Trace.Reset();

        [Fact]
        public void IfElse_While_and_Parallel_run_in_WF3s_order()
        {
            using (var host = new Host())
            {
                host.Run(typeof(ControlFlowWorkflow));
                Assert.Equal("Completed", host.Events.Last());
                // Parallel starts each branch in turn, and each branch's next activity after the other branches' have run.
                Assert.Equal(new[] { "bigStep", "loop 1", "loop 2", "loop 3", "a1", "b1", "a2", "b2" }, Trace.Steps);
                Assert.Equal(3, host.Completed.OutputParameters["Count"]);
            }
        }

        [Fact]
        public void IfElse_takes_the_branch_without_a_condition_when_the_others_are_false()
        {
            using (var host = new Host())
            {
                host.Run(typeof(ControlFlowWorkflow), new System.Collections.Generic.Dictionary<string, object> { { "Limit", 1 } });
                Assert.Equal(new[] { "smallStep", "loop 1", "a1", "b1", "a2", "b2" }, Trace.Steps);
            }
        }

        [Fact]
        public void A_fault_handler_catches_the_exception_and_the_workflow_continues()
        {
            using (var host = new Host())
            {
                host.Run(typeof(FaultHandlingWorkflow));
                Assert.Equal("Completed", host.Events.Last());
                Assert.Equal(new[] { "fail", "handle", "after" }, Trace.Steps);
                Assert.Equal("ArgumentException: bad argument", host.Completed.OutputParameters["Caught"]);
            }
        }

        [Fact]
        public void ThrowActivity_throws_its_fault()
        {
            using (var host = new Host())
            {
                host.Run(typeof(ThrowActivityWorkflow));
                Assert.Equal("Terminated: System.NotSupportedException: thrown by ThrowActivity", host.Events.Last());
            }
        }

        [Fact]
        public void Delay_idles_the_instance_until_its_timer()
        {
            using (var host = new Host(activeTimers: true))
            {
                host.Run(typeof(DelayWorkflow));
                Assert.Equal("Idled", host.Events.Last());
                Assert.Empty(Trace.Steps);
                Assert.True(host.Done.Wait(TimeSpan.FromSeconds(30)));
                Assert.Equal(new[] { "after" }, Trace.Steps);
                Assert.Equal("Completed", host.Events.Last());
            }
        }

        [Fact]
        public void A_delay_waits_for_the_manual_scheduler_without_active_timers()
        {
            using (var host = new Host(activeTimers: false))
            {
                WorkflowInstance instance = host.Run(typeof(DelayWorkflow));
                Assert.Equal("Idled", host.Events.Last());
                System.Threading.Thread.Sleep(500);
                Assert.Empty(Trace.Steps);
                // The timer is due: running the instance again delivers it.
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { "after" }, Trace.Steps);
            }
        }

        [Fact]
        public void Listen_takes_the_first_branch_whose_event_comes()
        {
            using (var host = new Host(activeTimers: true))
            {
                host.Run(typeof(ListenWorkflow));
                Assert.True(host.Done.Wait(TimeSpan.FromSeconds(30)));
                Assert.Equal(new[] { "fastCode" }, Trace.Steps);
                Assert.Equal("Completed", host.Events.Last());
            }
        }

        [Fact]
        public void A_state_machine_moves_through_its_states()
        {
            using (var host = new Host(activeTimers: true))
            {
                WorkflowInstance instance = host.Run(typeof(StateMachine));
                var machine = new StateMachineWorkflowInstance(host.Runtime, instance.InstanceId);
                Assert.Equal("Working", machine.CurrentStateName);
                Assert.Equal(new[] { "Done" }, machine.PossibleStateTransitions.Cast<string>());
                Assert.Equal(new[] { "enterWaiting", "enterWorking" }, Trace.Steps);
                Assert.True(host.Done.Wait(TimeSpan.FromSeconds(30)));
                Assert.Equal(new[] { "enterWaiting", "enterWorking", "leaveWorking" }, Trace.Steps);
                Assert.Equal("Completed", host.Events.Last());
            }
        }

        [Fact]
        public void The_host_sets_a_state_machines_state()
        {
            using (var host = new Host())
            {
                WorkflowInstance instance = host.Run(typeof(StateMachine));
                var machine = new StateMachineWorkflowInstance(host.Runtime, instance.InstanceId);
                machine.SetState("Done");
                host.Scheduler.RunWorkflow(instance.InstanceId);
                Assert.Equal(new[] { "enterWaiting", "enterWorking", "leaveWorking" }, Trace.Steps);
                Assert.Equal("Completed", host.Events.Last());
            }
        }
    }
}
