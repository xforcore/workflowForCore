// Checks the three assemblies' identities, as .NET Framework 4.8 has them, from the packages, and runs a sequential
// workflow defined in code on the default scheduler.
using System.Reflection;
using System.Workflow.Activities;
using System.Workflow.Runtime;

string[] names = ["System.Workflow.ComponentModel", "System.Workflow.Runtime", "System.Workflow.Activities"];
int failures = 0;
foreach (var name in names)
{
    var n = Assembly.Load(name).GetName();
    string token = Convert.ToHexString(n.GetPublicKeyToken() ?? []).ToLowerInvariant();
    bool ok = n.Version == new Version(4, 0, 0, 0) && token == "31bf3856ad364e35";
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {n.FullName}");
}

using var runtime = new WorkflowRuntime();
var done = new ManualResetEventSlim();
string outcome = "timed out";
runtime.WorkflowCompleted += (_, e) => { outcome = "completed: " + e.OutputParameters["Message"]; done.Set(); };
runtime.WorkflowTerminated += (_, e) => { outcome = "terminated: " + e.Exception; done.Set(); };
runtime.CreateWorkflow(typeof(Hello)).Start();
done.Wait(TimeSpan.FromSeconds(30));
bool completed = outcome == "completed: Hello from WF3";
if (!completed) failures++;
Console.WriteLine($"{(completed ? "ok  " : "FAIL")} workflow {outcome}");
return failures == 0 ? 0 : 1;

public sealed class Hello : SequentialWorkflowActivity
{
    public Hello()
    {
        var code = new CodeActivity("say");
        code.ExecuteCode += (_, _) => Message = "Hello from WF3";
        Activities.Add(code);
    }

    public string Message { get; set; } = "";
}
