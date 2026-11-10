// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Instance state across runtimes: an instance persisted by .NET Framework 4.8 resumes on workflowForCore, and the
// reverse. The state is what a persistence service stores (WorkflowPersistenceService.GetDefaultSerializedForm, i.e.
// Activity.Save with WF3's BinaryFormatter, compressed), of OrderWorkflow waiting in its Listen for an event.
//
// FrameworkData/instance-net48.bin was written by the net48 build of these tests on CI's Windows runner, and
// FrameworkData/instance-net10.bin by the net10.0 build: each build writes its own state to
// $WORKFLOWFORCORE_INSTANCES when that is set (CI uploads them as the "instances" artifact, to commit), and resumes
// both committed files.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Workflow.Activities;
using System.Workflow.Runtime;
using Xunit;

namespace WorkflowForCore.Tests
{
    public class InteropTests
    {
        static readonly Guid InstanceId = new Guid("6f9c1d2e-3b4a-4c5d-8e7f-0a1b2c3d4e5f");

#if NETFRAMEWORK48
        const string Side = "net48";
#else
        const string Side = "net10";
#endif

        public InteropTests() => Trace.Reset();

        static string FrameworkData(string name) => Path.Combine(AppContext.BaseDirectory, "FrameworkData", name);

        // This runtime's state of a waiting OrderWorkflow, with a fixed instance id.
        static byte[] Write()
        {
            var persistence = new MemoryPersistenceService();
            var exchange = new ExternalDataExchangeService();
            using (var host = new Host(false, persistence, exchange))
            {
                exchange.AddService(new OrderService());
                WorkflowInstance instance = host.Runtime.CreateWorkflow(typeof(OrderWorkflow), new Dictionary<string, object> { { "Amount", 21.5m } }, InstanceId);
                instance.Start();
                host.Scheduler.RunWorkflow(InstanceId);
                instance.Unload();
                return persistence.Instances[InstanceId];
            }
        }

        // Loads the state in this runtime, raises Approved, and checks that the instance finishes as it would have.
        static void Resume(byte[] state)
        {
            var persistence = new MemoryPersistenceService();
            persistence.Instances[InstanceId] = state;
            var exchange = new ExternalDataExchangeService();
            using (var host = new Host(false, persistence, exchange))
            {
                var orders = new OrderService();
                exchange.AddService(orders);
                host.Runtime.StartRuntime();
                WorkflowInstance instance = host.Runtime.GetWorkflow(InstanceId);
                orders.Approve(InstanceId, 8);
                host.Scheduler.RunWorkflow(InstanceId);
                Assert.Equal("Completed", host.Events.Last());
                Assert.Equal(new[] { "approvedEvent 8" }, Trace.Steps);
                // The amount the instance had before it was persisted (21.5) was replaced by the event's.
                Assert.Equal(new[] { "finished 8" }, orders.Notifications);
                Assert.Equal("approvedEvent 8", host.Completed.OutputParameters["Outcome"]);
            }
        }

        [Fact]
        public void This_runtimes_state_resumes_here()
        {
            byte[] state = Write();
            string folder = Environment.GetEnvironmentVariable("WORKFLOWFORCORE_INSTANCES");
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "instance-" + Side + ".bin"), state);
            }
            Resume(state);
        }

        [Fact]
        public void An_instance_persisted_by_NET_Framework_4_8_resumes()
        {
            Resume(File.ReadAllBytes(FrameworkData("instance-net48.bin")));
        }

        [Fact]
        public void An_instance_persisted_by_workflowForCore_resumes()
        {
            Resume(File.ReadAllBytes(FrameworkData("instance-net10.bin")));
        }
    }
}
