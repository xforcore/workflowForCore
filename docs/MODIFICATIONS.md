# What workflowForCore changed in Microsoft's code

workflowForCore runs Microsoft's own Windows Workflow Foundation 3 rather than a reimplementation of it. Microsoft's
source comes from the `external/referencesource` **submodule**: a fork of microsoft/referencesource, cleared down to
the files workflowForCore uses at a pinned commit, built for .NET 10. This page explains the rules for that code and
what had to change; [LIFT.md](LIFT.md) says what is compiled and what isn't.

- **Every change is a commit** on the fork's `workflowforcore` branch, with its reason in the message:

  | Submodule | Fork branch | Upstream commit | Changes |
  | --- | --- | --- | --- |
  | `external/referencesource` | [xforcore/referencesource `workflowforcore`](https://github.com/xforcore/referencesource/commits/workflowforcore) (beside webForCore's `webforcore` and messagingForCore's `messagingforcore`) | `ec9fa9a` (microsoft/referencesource, .NET Framework 4.8) | WF3's three folders and one System.Messaging file kept; changes listed below |

  The branch starts at the upstream commit, then one commit keeps only the files workflowForCore uses (and the
  licences), then the changes, one problem per commit. `git log` on the submodule lists them; `git revert` undoes one.
- Licences and attributions are in [`NOTICE`](../NOTICE) and [`LICENSE`](../LICENSE).

## Rules for Microsoft's code

1. **Don't rewrite.** Each file is upstream's, with its copyright header, at the pinned commit.
2. **Every change is a commit on the fork,** one problem per commit, each with a `// workflowForCore:` comment that
   gives the reason (CONTRIBUTING.md principle 4). Code .NET can't run stays in the file under `#if !WORKFLOWFORCORE`
   (the projects define `WORKFLOWFORCORE`), so the file still reads as Microsoft's and the change is a few lines.
3. **workflowForCore's own code stays in `src/`**, next to the `Link` items that compile the submodule's files at
   their old paths: `src/Shared` (compiled into each assembly) and each assembly's `workflowForCore/` folder.
4. **Every file is traceable:** the submodule pins the fork's commit, and the fork's history goes back to upstream's.
5. **Identity is kept.** The assemblies keep Microsoft's names, version 4.0.0.0 and public key (token
   31bf3856ad364e35), delay signed with `keys/35MSSharedLib1024.snk` (the public key only), as webForCore's
   assemblies with Microsoft's identities are. CI compares them with .NET Framework 4.8's reference assemblies and,
   on Windows, its GAC (`tools/ci/check_assembly_identities.cs`).
6. **.NET Framework 4.8 is the reference.** Where WF3 on .NET would behave differently from .NET Framework 4.8, a
   change restores 4.8's behaviour, and a test run on both shows it.
7. **Only from the Reference Source.** No code comes from dotnet/runtime. What the Reference Source doesn't have is
   read from .NET Framework 4.8 as data (strings, configuration names, SQL scripts) or written by workflowForCore.

## Library changes, by kind

Each row is one commit (or two) on the fork. The commit's message has the detail; each changed place has a
`// workflowForCore:` comment.

| Kind | Change | Where |
| --- | --- | --- |
| **Designer types by name** | Attributes that name designer-time types with `typeof` (`[Designer]`, `[ToolboxItem]`, `[DesignerSerializer]` for the CodeDom serializers, `[Editor]`, the rule editor's `[TypeConverter]`) name them by assembly-qualified name, which is what those attributes record on .NET Framework too; System.Design's and System.Drawing's base types are named as 4.8 has them. `[ToolboxBitmap]` is removed (no string form; Visual Studio's toolbox only) | 43 files, mostly the activities' declarations |
| **Designer surface not compiled** | The designer folders aren't compiled (LIFT.md). In compiled files, designer code is under `#if !WORKFLOWFORCORE`: unused Windows Forms usings, the designers' GDI and user32 P/Invokes and GDI+'s `Matrix` (both `NativeMethods` copies), Visual Studio's per-user registry key and designer view state (both `CompModHelpers` copies), the designer loader's paths in `BindValidator` and the rules' `Helpers` (never there at run time), `SynchronizationScope`'s property editor, `EventHandlingScope`'s designer and theme, the markup mapping's designer types and the layout serializers. Five Design files are compiled for the types the runtime uses: `DesignerInterfaces` (the interfaces and enums that need no designer), `DesignerHelpers` (`GenerateUniqueIdentifier`, which names an activity added without a name: `codeActivity1`), `PropertyDescriptors` (not the site and the bind editor), `TypeConverters`, `ActivityDesignerLayoutSerializers` (the Color, Size and Point markup serializers). `ActivityCodeDomSerializer` keeps only its `MarkupFileName` attached property, which the markup serializer sets; XOML's code-beside generation (Visual Studio's CodeDom serializers) isn't compiled (phase 2) | 20 files |
| **No debugger** | `WorkflowRuntime` doesn't create `DebugController` (Visual Studio's workflow debugger over an IPC remoting channel, kernel32 and advapi32). `WorkflowDebuggerSteppingAttribute` and `IWorkflowDebuggerService` are compiled | `WorkflowRuntime.cs` |
| **No COM+** | The scheduler's atomic scopes use `EnterpriseServicesInteropOption.None` instead of `Full` (no COM+ to flow to); four unused `System.EnterpriseServices` aliases | `Scheduler.cs` and 4 files |
| **No telemetry** | `WorkflowRuntime`'s static constructor sent a usage event to Microsoft (`TelemetryEventSource`, not in the Reference Source); it isn't sent | `WorkflowRuntime.cs` |
| **Performance counters on Windows only** | Off Windows the runtime and schedulers create none (.NET's `PerformanceCounter` throws there), as on Windows when WF's counter category isn't installed | `PerformanceCounterManager.cs` |
| **webForCore's BinaryFormatter** | The six files that create a `BinaryFormatter` (instance state, tracking data, cloned arguments of local services) name workflowForCore's (`src/Shared/BinaryFormatter.cs`, webForCore's wire-compatible port) through a using alias. `Activity.Save`/`Load(…, IFormatter)` given .NET's `BinaryFormatter` (which throws) use webForCore's with its settings, so a persistence service written for .NET Framework works unchanged | `Activity.cs` and 5 files |
| **No remoting** | `MethodMessage` captured an (empty) `LogicalCallContext` through a `RealProxy`; it starts with an empty one. `IMethodMessage`, `IMessage`, `ILogicalThreadAffinative` and `LogicalCallContext` are workflowForCore's, internal (`Activities/workflowForCore/Remoting.cs`) | `MethodMessage.cs`, `DataExchangeServiceBinder.cs` |
| **No MSBuild** | `WorkflowCompiler`'s multi-targeting lists .NET Framework's targeting packs with MSBuild's `ToolLocationHelper`; there are none on .NET, so the list is empty | `XomlCompilerParameters.cs` |
| **One application domain** | `WorkflowCompiler.Compile` compiles in this domain instead of a new one (phase 2 may use a collectible `AssemblyLoadContext`) | `XomlCompiler.cs` |
| **No ASMX activities** | `InvokeWorkflowActivity`'s validation of an activating `WebServiceInputActivity` (not compiled, phase 2) | `InvokeSchedule.cs` |
| **A shared constant** | `RuleValidation` names `ExtensionAttribute` with `AssemblyRef.SystemCore`, from a shared class of .NET Framework's build that the Reference Source doesn't have; the name is written out | `Rules/RuleValidation.cs` |
| **System.Messaging's error codes** | The queuing service throws `QueueException` with System.Messaging's `MessageQueueErrorCode` and names it in the message. That one file of System.Messaging is kept (unchanged, then made internal, so that it doesn't clash with messagingForCore's) and compiled into System.Workflow.Runtime | `System.Messaging/System/Messaging/MessageQueueErrorCode.cs` |

## Build changes

- **`WORKFLOWFORCORE`** is defined (`src/Directory.Build.props`).
- **No `System.Diagnostics.DiagnosticSource`** in the compilation (`src/Directory.Build.targets`): .NET's
  `System.Diagnostics.Activity` made WF3's own `Activity` ambiguous in files that import both `System.Diagnostics` and
  `System.Workflow.ComponentModel`. .NET Framework 4.8 had no such type and WF3 doesn't use DiagnosticSource, so
  Microsoft's files compile unchanged.
- **Packages for what .NET Framework had in the box:** System.CodeDom, System.Configuration.ConfigurationManager,
  System.Security.Permissions, System.Data.SqlClient (as .NET Framework, not Microsoft.Data.SqlClient),
  System.Data.OleDb, System.Diagnostics.PerformanceCounter and System.DirectoryServices (the last three Windows only
  at run time, as on .NET Framework).
- **Warnings** that Microsoft's code has on .NET (obsolete APIs and the like) aren't fixed in it (`NoWarn`).

## Classes the Reference Source doesn't have

.NET Framework's assemblies have these and WF3's code calls them, but the Reference Source doesn't include them, and
.NET has no counterpart. workflowForCore writes its own, as small as WF3 needs, with .NET Framework's names:

| Class | Why | Where |
| --- | --- | --- |
| String resources and `ExecutionStringManager`, `Rules.Messages` | The strings, read from .NET Framework 4.8's assemblies as data (`tools/framework_data.cs`); the SDK generates the two strongly typed classes from them, as .NET Framework's build did | `src/*/FrameworkData` |
| `AppSettings`, `LocalAppContextSwitches` | WF3's appSettings (`microsoft:WorkflowComponentModel:XOMLAllowXCodeInDisabledActivities`, `...:XOMLMaximumNestedObjectDepth` (300), `...:DisableActivitySurrogateSelectorTypeCheck`, `...:DisableXOMLSerializerTypeChecking`, `...:DisableXOMLSerializerDefaultUnauthorizedTypes`, `...:DisableXOMLCompilerIdentifierValidation`, `microsoft:WorkflowRuntime:FIPSRequired`) and AppContext switches (`Switch.System.Workflow.*.UseLegacyHashFor*`: MD5 instead of SHA-256), read with `ConfigurationManager` and `AppContext`. The names and defaults are the ones `tools/framework_settings.cs` read from .NET Framework 4.8's assemblies (`src/FrameworkData/settings.txt`) | `src/*/workflowForCore/AppSettings.cs` |
| `MD5PInvokeHelper` | The tracking services' MD5 where a FIPS policy is enforced; .NET's MD5 gives the same hash and isn't subject to that policy | `src/workflowForCore.System.Workflow.Runtime/workflowForCore/AppSettings.cs` |
| `DesignerSerializationManager` | Every markup load and save (XOML, `.rules`) creates one; on .NET it is in Windows Forms' design assembly (Windows only), and it isn't in the Reference Source. workflowForCore's is internal, with the behaviour WF3 relies on as .NET Framework documents it: sessions, the context stack, errors, providers, serializers from `[DesignerSerializer]` and `[DefaultSerializationProvider]`, names and services | `src/Shared/DesignerSerializationManager.cs` |
| `BinaryFormatter` | webForCore's port as a `System.Runtime.Serialization.IFormatter` (above) | `src/Shared/BinaryFormatter.cs` |
| Delegate serialization | WF3 keeps activities' event handlers in their instance values, so every `Activity.Save` (each While iteration, state, Replicator child and persisted instance) serializes delegates, which .NET can't and webForCore's BinaryFormatter doesn't (yet). workflowForCore's formatter writes and reads them in .NET Framework's format (`System.DelegateSerializationHolder` and its `DelegateEntry` records, from referencesource's mscorlib), with `Delegate.CreateDelegate` | `src/Shared/DelegateSerialization.cs` |
| Remoting's message shapes | Above | `src/workflowForCore.System.Workflow.Activities/workflowForCore/Remoting.cs` |
