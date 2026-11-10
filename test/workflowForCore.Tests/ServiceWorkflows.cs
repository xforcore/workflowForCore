// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Workflows and services for ExternalDataExchange, transactions, compensation and persistence, written as Visual
// Studio's designer writes them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Transactions;
using System.Workflow.Activities;
using System.Workflow.ComponentModel;
using System.Workflow.Runtime;
using System.Workflow.Runtime.Hosting;

namespace WorkflowForCore.Tests
{
    [Serializable]
    public class OrderEventArgs : ExternalDataEventArgs
    {
        public OrderEventArgs(Guid instanceId, decimal amount) : base(instanceId)
        {
            Amount = amount;
        }

        public decimal Amount { get; set; }
    }

    [ExternalDataExchange]
    public interface IOrderService
    {
        event EventHandler<OrderEventArgs> Approved;
        event EventHandler<OrderEventArgs> Rejected;
        void Notify(string message, decimal amount);
    }

    public sealed class OrderService : IOrderService
    {
        public readonly List<string> Notifications = new List<string>();

        public event EventHandler<OrderEventArgs> Approved;
        public event EventHandler<OrderEventArgs> Rejected;

        public void Notify(string message, decimal amount)
        {
            lock (Notifications)
                Notifications.Add(message + " " + amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public void Approve(Guid instanceId, decimal amount) => Approved(null, new OrderEventArgs(instanceId, amount));

        public void Reject(Guid instanceId) => Rejected(null, new OrderEventArgs(instanceId, 0));
    }

    // Calls Notify, then waits for Approved or Rejected (a Listen), then calls Notify again.
    public sealed class OrderWorkflow : SequentialWorkflowActivity
    {
        public decimal Amount { get; set; } = 12.5m;
        public string Outcome { get; set; }

        public OrderWorkflow()
        {
            Activities.Add(Call("notifyStart", "started"));

            var listen = new ListenActivity("listen");
            listen.Activities.Add(Branch("approved", "Approved"));
            listen.Activities.Add(Branch("rejected", "Rejected"));
            Activities.Add(listen);

            Activities.Add(Call("notifyEnd", "finished"));
        }

        CallExternalMethodActivity Call(string name, string message)
        {
            var call = new CallExternalMethodActivity(name) { InterfaceType = typeof(IOrderService), MethodName = "Notify" };
            var text = new WorkflowParameterBinding("message");
            text.Value = message;
            call.ParameterBindings.Add(text);
            var amount = new WorkflowParameterBinding("amount");
            amount.SetBinding(WorkflowParameterBinding.ValueProperty, new ActivityBind("OrderWorkflow", "Amount"));
            call.ParameterBindings.Add(amount);
            return call;
        }

        EventDrivenActivity Branch(string name, string eventName)
        {
            var branch = new EventDrivenActivity(name);
            var handle = new HandleExternalEventActivity(name + "Event") { InterfaceType = typeof(IOrderService), EventName = eventName };
            if (eventName == "Approved") handle.Invoked += OnApproved; else handle.Invoked += OnRejected;
            branch.Activities.Add(handle);
            return branch;
        }

        // The sender is the event's sender (null here), not the activity.
        void OnApproved(object sender, ExternalDataEventArgs e) => OnInvoked("approvedEvent", sender, e);
        void OnRejected(object sender, ExternalDataEventArgs e) => OnInvoked("rejectedEvent", sender, e);

        void OnInvoked(string name, object sender, ExternalDataEventArgs e)
        {
            var args = (OrderEventArgs)e;
            Outcome = name + " " + args.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + (sender == null ? "" : " from " + sender);
            Amount = args.Amount;
            Trace.Add(Outcome);
        }
    }

    // A TransactionScopeActivity whose code runs in its ambient transaction.
    public sealed class TransactionWorkflow : SequentialWorkflowActivity
    {
        public string TransactionState { get; set; }
        public IsolationLevel Isolation { get; set; }

        public TransactionWorkflow()
        {
            var scope = new TransactionScopeActivity("atomic");
            scope.TransactionOptions.IsolationLevel = IsolationLevel.ReadCommitted;
            var code = new CodeActivity("inTransaction");
            code.ExecuteCode += OnCode;
            scope.Activities.Add(code);
            Activities.Add(scope);
            var after = new CodeActivity("afterTransaction");
            after.ExecuteCode += OnAfter;
            Activities.Add(after);
        }

        void OnCode(object sender, EventArgs e)
        {
            Transaction t = Transaction.Current;
            TransactionState = t == null ? "none" : t.TransactionInformation.Status.ToString();
            Isolation = t == null ? IsolationLevel.Unspecified : t.IsolationLevel;
            Trace.Add("in " + TransactionState);
        }

        void OnAfter(object sender, EventArgs e) => Trace.Add("after " + (Transaction.Current == null ? "none" : "transaction"));
    }

    // Two compensatable sequences, then a fault: WF3 compensates the completed ones, the last first.
    public sealed class CompensationWorkflow : SequentialWorkflowActivity
    {
        public bool HandleFault { get; set; }

        public CompensationWorkflow()
        {
            Activities.Add(Compensatable("first"));
            Activities.Add(Compensatable("second"));
            var fail = new CodeActivity("fail");
            fail.ExecuteCode += OnFail;
            Activities.Add(fail);

            var handlers = new FaultHandlersActivity("faultHandlers");
            var handler = new FaultHandlerActivity("handler") { FaultType = typeof(ApplicationException) };
            handler.Activities.Add(new CompensateActivity("compensateFirst") { TargetActivityName = "first" });
            handlers.Activities.Add(handler);
            Activities.Add(handlers);
        }

        CompensatableSequenceActivity Compensatable(string name)
        {
            var sequence = new CompensatableSequenceActivity(name);
            var work = new CodeActivity(name + "Work");
            work.ExecuteCode += OnStep;
            sequence.Activities.Add(work);
            var compensation = new CompensationHandlerActivity(name + "Compensation");
            var undo = new CodeActivity(name + "Undo");
            undo.ExecuteCode += OnStep;
            compensation.Activities.Add(undo);
            sequence.Activities.Add(compensation);
            return sequence;
        }

        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
        void OnFail(object sender, EventArgs e) { Trace.Add("fail"); throw new ApplicationException("failed after both"); }
    }

    // The same without a fault handler: the fault isn't handled, and WF3 compensates both before terminating.
    public sealed class UnhandledCompensationWorkflow : SequentialWorkflowActivity
    {
        public UnhandledCompensationWorkflow()
        {
            foreach (string name in new[] { "first", "second" })
            {
                var sequence = new CompensatableSequenceActivity(name);
                var work = new CodeActivity(name + "Work");
                work.ExecuteCode += OnStep;
                sequence.Activities.Add(work);
                var compensation = new CompensationHandlerActivity(name + "Compensation");
                var undo = new CodeActivity(name + "Undo");
                undo.ExecuteCode += OnStep;
                compensation.Activities.Add(undo);
                sequence.Activities.Add(compensation);
                Activities.Add(sequence);
            }
            var fail = new CodeActivity("fail");
            fail.ExecuteCode += OnFail;
            Activities.Add(fail);
        }

        void OnStep(object sender, EventArgs e) => Trace.Add(((Activity)sender).Name);
        void OnFail(object sender, EventArgs e) { Trace.Add("fail"); throw new ApplicationException("unhandled"); }
    }

    // A persistence service that keeps instances in memory, with WF3's own serialized form (Activity.Save through
    // GetDefaultSerializedForm), as hosts write their own.
    public sealed class MemoryPersistenceService : WorkflowPersistenceService
    {
        public readonly Dictionary<Guid, byte[]> Instances = new Dictionary<Guid, byte[]>();
        public readonly Dictionary<string, byte[]> Scopes = new Dictionary<string, byte[]>();
        public int Saves;
        readonly bool unloadOnIdle;

        public MemoryPersistenceService(bool unloadOnIdle = false)
        {
            this.unloadOnIdle = unloadOnIdle;
        }

        protected override void SaveWorkflowInstanceState(Activity rootActivity, bool unlock)
        {
            lock (Instances)
            {
                Instances[WorkflowEnvironment.WorkflowInstanceId] = GetDefaultSerializedForm(rootActivity);
                Saves++;
            }
        }

        protected override Activity LoadWorkflowInstanceState(Guid instanceId)
        {
            byte[] state;
            lock (Instances)
            {
                if (!Instances.TryGetValue(instanceId, out state))
                    throw new PersistenceException("no instance " + instanceId);
            }
            return RestoreFromDefaultSerializedForm(state, null);
        }

        protected override void UnlockWorkflowInstanceState(Activity rootActivity)
        {
        }

        protected override void SaveCompletedContextActivity(Activity activity)
        {
            lock (Instances)
                Scopes[WorkflowEnvironment.WorkflowInstanceId + "/" + activity.QualifiedName] = GetDefaultSerializedForm(activity);
        }

        protected override Activity LoadCompletedContextActivity(Guid scopeId, Activity outerActivity)
        {
            lock (Instances)
            {
                foreach (KeyValuePair<string, byte[]> scope in Scopes)
                {
                    Activity activity = RestoreFromDefaultSerializedForm(scope.Value, outerActivity);
                    if ((Guid)activity.GetValue(Activity.ActivityContextGuidProperty) == scopeId)
                        return activity;
                }
            }
            throw new PersistenceException("no scope " + scopeId);
        }

        protected override bool UnloadOnIdle(Activity activity) => unloadOnIdle;
    }
}
