// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// Tracking: a tracking service of the host's own with a profile that tracks every workflow event, every activity's
// status and user data (TrackData), and SqlTrackingService in a database made with .NET Framework 4.8's scripts, read
// back with SqlTrackingQuery. The same records, in the same order, on .NET Framework 4.8.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Workflow.Activities;
using System.Workflow.ComponentModel;
using System.Workflow.Runtime;
using System.Workflow.Runtime.Tracking;
using Xunit;

namespace WorkflowForCore.Tests
{
    public sealed class TrackedWorkflow : SequentialWorkflowActivity
    {
        public TrackedWorkflow()
        {
            var first = new CodeActivity("first");
            first.ExecuteCode += OnFirst;
            Activities.Add(first);
            var second = new CodeActivity("second");
            second.ExecuteCode += OnSecond;
            Activities.Add(second);
        }

        void OnFirst(object sender, EventArgs e) => TrackData("note", "first ran");
        void OnSecond(object sender, EventArgs e) => TrackData(42);
    }

    // Records every tracking record as text.
    public sealed class RecordingTrackingService : TrackingService
    {
        public readonly List<string> Records = new List<string>();

        static TrackingProfile Profile()
        {
            var profile = new TrackingProfile { Version = new Version(1, 0) };
            var workflow = new WorkflowTrackPoint();
            foreach (TrackingWorkflowEvent e in Enum.GetValues(typeof(TrackingWorkflowEvent)))
                workflow.MatchingLocation.Events.Add(e);
            profile.WorkflowTrackPoints.Add(workflow);
            var activity = new ActivityTrackPoint();
            var location = new ActivityTrackingLocation(typeof(Activity)) { MatchDerivedTypes = true };
            foreach (ActivityExecutionStatus s in Enum.GetValues(typeof(ActivityExecutionStatus)))
                location.ExecutionStatusEvents.Add(s);
            activity.MatchingLocations.Add(location);
            profile.ActivityTrackPoints.Add(activity);
            var user = new UserTrackPoint();
            user.MatchingLocations.Add(new UserTrackingLocation(typeof(object), typeof(Activity)) { MatchDerivedActivityTypes = true, MatchDerivedArgumentTypes = true });
            profile.UserTrackPoints.Add(user);
            return profile;
        }

        protected override bool TryGetProfile(Type workflowType, out TrackingProfile profile)
        {
            profile = Profile();
            return true;
        }

        protected override TrackingProfile GetProfile(Guid workflowInstanceId) => Profile();

        protected override TrackingProfile GetProfile(Type workflowType, Version profileVersionId) => Profile();

        protected override bool TryReloadProfile(Type workflowType, Guid workflowInstanceId, out TrackingProfile profile)
        {
            profile = null;
            return false;
        }

        protected override TrackingChannel GetTrackingChannel(TrackingParameters parameters) => new Channel(this);

        sealed class Channel : TrackingChannel
        {
            readonly RecordingTrackingService service;

            public Channel(RecordingTrackingService service)
            {
                this.service = service;
            }

            protected override void Send(TrackingRecord record)
            {
                string text;
                if (record is WorkflowTrackingRecord w)
                    text = "workflow " + w.TrackingWorkflowEvent;
                else if (record is ActivityTrackingRecord a)
                    text = "activity " + a.QualifiedName + " " + a.ExecutionStatus;
                else if (record is UserTrackingRecord u)
                    text = "user " + u.QualifiedName + " " + (u.UserDataKey ?? "-") + "=" + u.UserData;
                else
                    text = record.GetType().Name;
                lock (service.Records)
                    service.Records.Add(text);
            }

            protected override void InstanceCompletedOrTerminated()
            {
                lock (service.Records)
                    service.Records.Add("done");
            }
        }
    }

    public class TrackingTests
    {
        [Fact]
        public void A_tracking_service_gets_the_workflow_activity_and_user_records_in_order()
        {
            var tracking = new RecordingTrackingService();
            using (var host = new Host(false, tracking))
            {
                host.Run(typeof(TrackedWorkflow));
                Assert.Equal("Completed", host.Events.Last());
            }
            Assert.Equal(new[]
            {
                "workflow Created",
                "workflow Started",
                "activity TrackedWorkflow Executing",
                "activity first Executing",
                "user first note=first ran",
                "activity first Closed",
                "activity second Executing",
                "user second -=42",
                "activity second Closed",
                "activity TrackedWorkflow Closed",
                "workflow Completed",
                "done",
            }, tracking.Records);
        }

        [Fact]
        public void Suspending_and_terminating_are_tracked()
        {
            var tracking = new RecordingTrackingService();
            using (var host = new Host(false, tracking))
            {
                WorkflowInstance instance = host.Run(typeof(SuspendingWorkflow));
                instance.Terminate("enough");
            }
            Assert.Contains("workflow Suspended", tracking.Records);
            Assert.Contains("workflow Terminated", tracking.Records);
            Assert.Equal("done", tracking.Records.Last());
        }

        [SqlFact]
        public void SqlTrackingService_stores_the_records_and_SqlTrackingQuery_reads_them()
        {
            using (var sql = new SqlServer("Tracking_Schema.sql", "Tracking_Logic.sql"))
            {
                Guid id;
                using (var host = new Host(false, new SqlTrackingService(sql.ConnectionString)))
                {
                    id = host.Run(typeof(TrackedWorkflow)).InstanceId;
                    Assert.Equal("Completed", host.Events.Last());
                }
                var query = new SqlTrackingQuery(sql.ConnectionString);
                Assert.True(query.TryGetWorkflow(id, out SqlTrackingWorkflowInstance instance));
                Assert.Equal(WorkflowStatus.Completed, instance.Status);
                Assert.Equal(typeof(TrackedWorkflow), instance.WorkflowType);
                Assert.Equal(new[] { TrackingWorkflowEvent.Created, TrackingWorkflowEvent.Started, TrackingWorkflowEvent.Completed },
                    instance.WorkflowEvents.Select(e => e.TrackingWorkflowEvent));
                Assert.Equal(new[] { "TrackedWorkflow Executing", "first Executing", "first Closed", "second Executing", "second Closed", "TrackedWorkflow Closed" },
                    instance.ActivityEvents.Select(e => e.QualifiedName + " " + e.ExecutionStatus));
                Assert.Equal(new[] { "first note=first ran", "second =42" },
                    instance.UserEvents.Select(e => e.QualifiedName + " " + e.UserDataKey + "=" + e.UserData));
                Assert.Equal(1, sql.Scalar("SELECT COUNT(*) FROM WorkflowInstance"));
            }
        }
    }
}
