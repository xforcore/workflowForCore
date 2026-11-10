// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Workflow.Runtime;
using System.Workflow.Runtime.Hosting;

namespace WorkflowForCore.Tests
{
    // A WorkflowRuntime with the manual scheduler, which runs instances on the test's thread, and what happened to
    // each instance, in order: the runtime's events as text ("Started", "Completed", "Terminated: <message>", ...).
    public sealed class Host : IDisposable
    {
        public readonly WorkflowRuntime Runtime;
        public readonly ManualWorkflowSchedulerService Scheduler;
        public readonly List<string> Events = new List<string>();
        public WorkflowCompletedEventArgs Completed;
        public WorkflowTerminatedEventArgs Terminated;
        public readonly ManualResetEventSlim Done = new ManualResetEventSlim();

        public Host(bool activeTimers = false, params object[] services)
        {
            Runtime = new WorkflowRuntime();
            Scheduler = new ManualWorkflowSchedulerService(activeTimers);
            Runtime.AddService(Scheduler);
            foreach (object service in services)
                Runtime.AddService(service);
            Runtime.WorkflowCreated += (s, e) => Add("Created");
            Runtime.WorkflowStarted += (s, e) => Add("Started");
            Runtime.WorkflowIdled += (s, e) => Add("Idled");
            Runtime.WorkflowSuspended += (s, e) => Add("Suspended: " + e.Error);
            Runtime.WorkflowResumed += (s, e) => Add("Resumed");
            Runtime.WorkflowPersisted += (s, e) => Add("Persisted");
            Runtime.WorkflowUnloaded += (s, e) => Add("Unloaded");
            Runtime.WorkflowLoaded += (s, e) => Add("Loaded");
            Runtime.WorkflowAborted += (s, e) => { Add("Aborted"); Done.Set(); };
            Runtime.WorkflowCompleted += (s, e) => { Completed = e; Add("Completed"); Done.Set(); };
            Runtime.WorkflowTerminated += (s, e) => { Terminated = e; Add("Terminated: " + e.Exception.GetType().FullName + ": " + e.Exception.Message); Done.Set(); };
        }

        void Add(string e)
        {
            lock (Events)
                Events.Add(e);
        }

        // Creates and starts an instance, and runs it on this thread until it is idle or done.
        public WorkflowInstance Run(Type workflowType, Dictionary<string, object> parameters = null)
        {
            WorkflowInstance instance = parameters == null ? Runtime.CreateWorkflow(workflowType) : Runtime.CreateWorkflow(workflowType, parameters);
            instance.Start();
            Scheduler.RunWorkflow(instance.InstanceId);
            return instance;
        }

        public void Dispose()
        {
            if (Runtime.IsStarted)
                Runtime.StopRuntime();
            Runtime.Dispose();
        }
    }

    // Steps the activities record, for tests to compare.
    public static class Trace
    {
        static List<string> steps = new List<string>();

        public static List<string> Steps => steps;

        public static void Reset() { lock (steps) steps.Clear(); }

        public static void Add(string step)
        {
            lock (Steps)
                Steps.Add(step);
        }
    }
}
