// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Workflows defined in code, written as Visual Studio's designer writes them: named activities, and handlers that are
// methods of the workflow class (WF3 binds handlers to the running instance's root activity). They are the same on
// .NET Framework 4.8 and on workflowForCore.
using System;
using System.ComponentModel;
using System.Workflow.Activities;
using System.Workflow.ComponentModel;

namespace WorkflowForCore.Tests
{
    public sealed class EmptyWorkflow : SequentialWorkflowActivity
    {
    }

    // Two code activities, an input property and an output property.
    public sealed class GreetingWorkflow : SequentialWorkflowActivity
    {
        public GreetingWorkflow()
        {
            var greet = new CodeActivity("greet");
            greet.ExecuteCode += OnGreet;
            var shout = new CodeActivity("shout");
            shout.ExecuteCode += OnShout;
            Activities.Add(greet);
            Activities.Add(shout);
        }

        public string Who { get; set; }
        public string Greeting { get; set; }

        void OnGreet(object sender, EventArgs e) { Greeting = "Hello, " + Who; Trace.Add("greet"); }
        void OnShout(object sender, EventArgs e) { Greeting = Greeting.ToUpperInvariant(); Trace.Add("shout"); }
    }

    // Activities added without names are named by CompositeActivity, as on .NET Framework.
    public sealed class UnnamedWorkflow : SequentialWorkflowActivity
    {
        public UnnamedWorkflow()
        {
            for (int i = 0; i < 3; i++)
            {
                var code = new CodeActivity();
                code.ExecuteCode += OnCode;
                Activities.Add(code);
            }
        }

        void OnCode(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
    }

    public sealed class ThrowingWorkflow : SequentialWorkflowActivity
    {
        public ThrowingWorkflow()
        {
            var code = new CodeActivity("fail");
            code.ExecuteCode += OnFail;
            Activities.Add(code);
        }

        void OnFail(object sender, EventArgs e) => throw new InvalidOperationException("the code activity failed");
    }

    public sealed class TerminatingWorkflow : SequentialWorkflowActivity
    {
        public TerminatingWorkflow()
        {
            Activities.Add(new TerminateActivity("terminate") { Error = "stopped by the workflow" });
            var after = new CodeActivity("after");
            after.ExecuteCode += OnAfter;
            Activities.Add(after);
        }

        void OnAfter(object sender, EventArgs e) => Trace.Add("after");
    }

    public sealed class SuspendingWorkflow : SequentialWorkflowActivity
    {
        public SuspendingWorkflow()
        {
            var before = new CodeActivity("before");
            before.ExecuteCode += OnStep;
            Activities.Add(before);
            Activities.Add(new SuspendActivity("suspend") { Error = "waiting for an operator" });
            var after = new CodeActivity("after");
            after.ExecuteCode += OnStep;
            Activities.Add(after);
        }

        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
    }

    // IfElse with code conditions, a While loop, and a Parallel with two sequences.
    public sealed class ControlFlowWorkflow : SequentialWorkflowActivity
    {
        public int Count { get; set; }
        public int Limit { get; set; } = 3;

        public ControlFlowWorkflow()
        {
            var ifElse = new IfElseActivity("ifElse");
            var big = new IfElseBranchActivity("big");
            var bigCondition = new CodeCondition();
            bigCondition.Condition += IsBig;
            big.Condition = bigCondition;
            big.Activities.Add(Step("bigStep"));
            var small = new IfElseBranchActivity("small");
            small.Activities.Add(Step("smallStep"));
            ifElse.Activities.Add(big);
            ifElse.Activities.Add(small);
            Activities.Add(ifElse);

            var loop = new WhileActivity("loop");
            var loopCondition = new CodeCondition();
            loopCondition.Condition += BelowLimit;
            loop.Condition = loopCondition;
            var body = new CodeActivity("body");
            body.ExecuteCode += OnBody;
            loop.Activities.Add(body);
            Activities.Add(loop);

            var parallel = new ParallelActivity("parallel");
            foreach (string branch in new[] { "a", "b" })
            {
                var sequence = new SequenceActivity("branch_" + branch);
                sequence.Activities.Add(Step(branch + "1"));
                sequence.Activities.Add(Step(branch + "2"));
                parallel.Activities.Add(sequence);
            }
            Activities.Add(parallel);
        }

        CodeActivity Step(string name)
        {
            var code = new CodeActivity(name);
            code.ExecuteCode += OnStep;
            return code;
        }

        void IsBig(object sender, ConditionalEventArgs e) => e.Result = Limit > 2;
        void BelowLimit(object sender, ConditionalEventArgs e) => e.Result = Count < Limit;
        void OnBody(object sender, EventArgs e) { Count++; Trace.Add("loop " + Count); }
        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
    }

    // A fault handler that catches what a code activity throws.
    public sealed class FaultHandlingWorkflow : SequentialWorkflowActivity
    {
        public string Caught { get; set; }

        public FaultHandlingWorkflow()
        {
            var scope = new SequenceActivity("scope");
            var fail = new CodeActivity("fail");
            fail.ExecuteCode += OnFail;
            scope.Activities.Add(fail);
            var never = new CodeActivity("never");
            never.ExecuteCode += OnStep;
            scope.Activities.Add(never);

            var handlers = new FaultHandlersActivity("faultHandlers");
            var handler = new FaultHandlerActivity("argumentHandler") { FaultType = typeof(ArgumentException) };
            var handle = new CodeActivity("handle");
            handle.ExecuteCode += OnHandle;
            handler.Activities.Add(handle);
            handlers.Activities.Add(handler);
            scope.Activities.Add(handlers);
            Activities.Add(scope);

            var after = new CodeActivity("after");
            after.ExecuteCode += OnStep;
            Activities.Add(after);
        }

        void OnFail(object sender, EventArgs e) { Trace.Add("fail"); throw new ArgumentException("bad argument"); }
        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);

        void OnHandle(object sender, EventArgs e)
        {
            var handler = (FaultHandlerActivity)((Activity)sender).Parent;
            Caught = handler.Fault.GetType().Name + ": " + handler.Fault.Message;
            Trace.Add("handle");
        }
    }

    // A ThrowActivity whose fault no handler catches.
    public sealed class ThrowActivityWorkflow : SequentialWorkflowActivity
    {
        public ThrowActivityWorkflow()
        {
            Activities.Add(new ThrowActivity("throw") { FaultType = typeof(NotSupportedException), Fault = new NotSupportedException("thrown by ThrowActivity") });
        }
    }

    // Listen: a branch waiting for a short delay, the other for a long one.
    public sealed class ListenWorkflow : SequentialWorkflowActivity
    {
        public ListenWorkflow()
        {
            var listen = new ListenActivity("listen");
            listen.Activities.Add(Branch("fast", TimeSpan.FromMilliseconds(200)));
            listen.Activities.Add(Branch("slow", TimeSpan.FromSeconds(30)));
            Activities.Add(listen);
        }

        EventDrivenActivity Branch(string name, TimeSpan delay)
        {
            var branch = new EventDrivenActivity(name);
            branch.Activities.Add(new DelayActivity(name + "Delay") { TimeoutDuration = delay });
            var code = new CodeActivity(name + "Code");
            code.ExecuteCode += OnStep;
            branch.Activities.Add(code);
            return branch;
        }

        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
    }

    public sealed class DelayWorkflow : SequentialWorkflowActivity
    {
        public DelayWorkflow()
        {
            Activities.Add(new DelayActivity("delay") { TimeoutDuration = TimeSpan.FromMilliseconds(300) });
            var after = new CodeActivity("after");
            after.ExecuteCode += OnStep;
            Activities.Add(after);
        }

        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
    }

    // A workflow that doesn't validate: an IfElse with no branch.
    public sealed class InvalidWorkflow : SequentialWorkflowActivity
    {
        public InvalidWorkflow()
        {
            Activities.Add(new IfElseActivity("emptyIfElse"));
        }
    }

    // A state machine: Waiting (its initialization moves to Working), Working (a delay moves to Done), Done.
    public sealed class StateMachine : StateMachineWorkflowActivity
    {
        public StateMachine()
        {
            InitialStateName = "Waiting";
            CompletedStateName = "Done";

            var waiting = new StateActivity("Waiting");
            var init = new StateInitializationActivity("WaitingInit");
            init.Activities.Add(Step("enterWaiting"));
            init.Activities.Add(new SetStateActivity("toWorking") { TargetStateName = "Working" });
            waiting.Activities.Add(init);

            var working = new StateActivity("Working");
            var workInit = new StateInitializationActivity("WorkingInit");
            workInit.Activities.Add(Step("enterWorking"));
            working.Activities.Add(workInit);
            var timeout = new EventDrivenActivity("WorkingTimeout");
            timeout.Activities.Add(new DelayActivity("workDelay") { TimeoutDuration = TimeSpan.FromMilliseconds(200) });
            timeout.Activities.Add(new SetStateActivity("toDone") { TargetStateName = "Done" });
            working.Activities.Add(timeout);
            var finalization = new StateFinalizationActivity("WorkingFinal");
            finalization.Activities.Add(Step("leaveWorking"));
            working.Activities.Add(finalization);

            Activities.Add(waiting);
            Activities.Add(working);
            Activities.Add(new StateActivity("Done"));
        }

        CodeActivity Step(string name)
        {
            var code = new CodeActivity(name);
            code.ExecuteCode += OnStep;
            return code;
        }

        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
    }
}
