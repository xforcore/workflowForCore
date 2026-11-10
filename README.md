# workflowForCore

**Windows Workflow Foundation 3 on .NET 10, on Linux as well as Windows: Microsoft's System.Workflow.ComponentModel,
System.Workflow.Runtime and System.Workflow.Activities from the .NET Framework 4.8 Reference Source.**

.NET Framework 3.0 shipped Windows Workflow Foundation (WF3, `System.Workflow.*`); .NET Framework 4 rebuilt it as
4.0.0.0 and added WF4 (`System.Activities`) beside it. .NET has neither. Microsoft's CoreWF ports WF4, and Mono's
`System.Workflow.*` assemblies are empty placeholders, so an application whose workflows are WF3 activities (Composite
C1's console, for one, whose actions are WF3 state machines) has nothing to run them on.

workflowForCore keeps Microsoft's own WF3: the runtime, the activities, state machines, local services, persistence,
tracking and rules, compiled for .NET 10 with the changes .NET needs (no designers, no debugger, no remoting or COM+).
The assemblies have .NET Framework's names, versions (4.0.0.0) and public key token (31bf3856ad364e35), so libraries
built against .NET Framework, and workflow state .NET Framework persisted, bind to them unchanged.

It is the WF3 counterpart of [drawingForCore](https://github.com/xforcore/drawingForCore) (System.Drawing) and
[messagingForCore](https://github.com/xforcore/messagingForCore) (System.Messaging), separate from
[webForCore](https://github.com/xforcore/webForCore), which runs ASP.NET 4.x applications on .NET 10: an application
that needs WF3 references these packages. The design is [issue #1](https://github.com/xforcore/workflowForCore/issues/1).

## Status

| | |
| --- | --- |
| Status | In development (0.1.0, not released). Phase 1 of issue #1: code-defined workflows |
| Target | .NET 10 LTS (`net10.0`). Linux x64 and Windows x64, both built and tested in CI |
| Tests | workflowForCore's own, written from scratch (Microsoft published none for WF3), run on .NET 10 on Linux and Windows and on .NET Framework 4.8 on Windows with the same expected results ([TESTING.md](docs/TESTING.md)) |
| Packages | `workflowForCore`, which brings `workflowForCore.ComponentModel`, `workflowForCore.Runtime` and `workflowForCore.Activities`, one per assembly as .NET Framework's GAC had them. Not on nuget.org |

## Using it

Add the feeds to your `nuget.config`: workflowForCore's, and webForCore's for `webForCore.Serialization.Formatters`
(webForCore's port of BinaryFormatter, which WF3 persists instances with):

```xml
<configuration>
  <packageSources>
    <add key="workflowForCore" value="https://xforcore.github.io/workflowForCore/index.json" />
    <add key="webForCore" value="https://xforcore.github.io/webForCore/index.json" />
  </packageSources>
</configuration>
```

Then reference the package:

```xml
<PackageReference Include="workflowForCore" Version="0.1.0" />
```

WF3's types are marked `[Obsolete]` ("use System.Activities"), as on .NET Framework: suppress CS0618 in projects that
use them.

## Scope

Phase 1 runs **code-defined workflows**: `WorkflowRuntime` and `WorkflowInstance`, the default and manual schedulers,
sequential and state-machine workflows defined in code, the base activities, `HandleExternalEvent` and
`CallExternalMethod` with `ExternalDataExchangeService`, `TransactionScopeActivity` and compensation, persistence
(`Activity.Save`/`Load`, `SqlWorkflowPersistenceService`), tracking (`SqlTrackingService`) and rules. Later phases:
XOML workflows compiled at run time, WF 3.5's `WorkflowServices`, the ASMX activities. Not supported: Visual Studio's
designer and debugger, and COM+ transaction flow. [MIGRATING.md](docs/MIGRATING.md) lists what differs.

## How it's made

Microsoft's code comes from the `external/referencesource` submodule: a fork of microsoft/referencesource under
[xforcore](https://github.com/xforcore), cleared down to WF3's three folders at a fixed commit, with each of
workflowForCore's changes as a commit on the `workflowforcore` branch ([MODIFICATIONS.md](docs/MODIFICATIONS.md),
[LIFT.md](docs/LIFT.md)). Code comes only from the Reference Source, never from dotnet/runtime. What the Reference
Source doesn't include (the string resources, the SQL scripts, a few classes) is read from .NET Framework 4.8 as data
or written here, as those documents say.

## Contributing and licences

[CONTRIBUTING.md](CONTRIBUTING.md) has the principles and how a change is proven. workflowForCore's own code is MIT,
and Microsoft's code keeps its MIT licence ([NOTICE](NOTICE), [LICENSING.md](docs/LICENSING.md)).
