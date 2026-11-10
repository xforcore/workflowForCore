# Where Microsoft's code comes from

workflowForCore's System.Workflow.* is Microsoft's, from the .NET Framework 4.8 Reference Source, and only from there
(not from dotnet/runtime, which has no WF3). It is a submodule of an xforcore fork; [MODIFICATIONS.md](MODIFICATIONS.md)
lists every change.

## The lift

| | |
| --- | --- |
| Upstream | [microsoft/referencesource](https://github.com/microsoft/referencesource) `ec9fa9a` (main, .NET Framework 4.8) |
| Fork and branch | [xforcore/referencesource](https://github.com/xforcore/referencesource) `workflowforcore` (beside `webforcore` and `messagingforcore`) |
| Submodule | `external/referencesource` (shallow) |
| Kept | `System.Workflow.ComponentModel/` (219 `.cs` files, 89,311 lines), `System.Workflow.Runtime/` (105, 31,919), `System.Workflow.Activities/` (161, 61,352), with the licences |
| Compiled by | `src/workflowForCore.System.Workflow.ComponentModel`, `.Runtime`, `.Activities` |

The branch is the upstream commit, then one commit that removes everything else ("keep only the files workflowForCore
uses"), then workflowForCore's changes, one problem per commit, each marked `// workflowForCore:`. The project files
are workflowForCore's: they compile the submodule's files through `Link` items at their old paths, next to
workflowForCore's own files.

## Changing the source

- **A change to Microsoft's code:** commit it on the fork's `workflowforcore` branch (one problem per commit,
  `// workflowForCore:` comment, the reason in the message), push the branch (fast-forward only), and move the
  submodule in a workflowForCore pull request with the test.
- **A newer upstream:** microsoft/referencesource is frozen at .NET Framework 4.8; there is nothing to follow.

## What is compiled

Of the three folders' 485 `.cs` files (182,582 lines), 332 files (116,125 lines, 4,064 of them under
`#if !WORKFLOWFORCORE`) are compiled, about 112,000 lines; about 70,500 lines (39%) aren't:

| Assembly | Files | Lines | Compiled files | Compiled lines |
| --- | ---: | ---: | ---: | ---: |
| System.Workflow.ComponentModel | 219 | 89,311 | 136 | 43,445 |
| System.Workflow.Runtime | 105 | 31,919 | 90 | 26,367 |
| System.Workflow.Activities | 161 | 61,352 | 106 | 46,313 |

Plus System.Messaging's `MessageQueueErrorCode.cs` (534 lines) in System.Workflow.Runtime.

### Not compiled, and why

| What | Files | Why | Phase |
| --- | --- | --- | --- |
| **Designers**: `ComponentModel/AuthoringOM/Design/**` (but five files, below), `*Designer.cs`, `Activities/Designers/**`, the rule editor `Activities/Rules/Design/**` | 123 | Visual Studio's design surface: Windows Forms, GDI+, System.Design (237 files of WF3 reference it, issue #1); designer types stay named in attributes (MODIFICATIONS.md) | Not supported |
| The CodeDom serializers (`Serializer/*CodeDomSerializer.cs` but `ActivityCodeDomSerializer`, `ActivityCodeDomSerializationManager`, `PropertySegmentSerializ*`) | 8 | Code-beside generation and the designer's component serialization: System.Design's `CodeDomSerializer` isn't on .NET | 2 (XOML compilation) |
| `Compiler/CompileXomlTask.cs` | 1 | The MSBuild task that compiles XOML in Visual Studio projects | 2 |
| **Debugger engine**: `Runtime/DebugEngine/**` but `WorkflowDebuggerSteppingAttribute` and `IWorkflowDebuggerService` | 10 | Visual Studio's workflow debugger: IPC remoting, kernel32 and advapi32 security calls | Not supported |
| WF4 interop: `Runtime/System/Activities/**` | 4 | `System.Activities.Statements.Interop` needs System.Activities (WF4), which isn't on .NET (CoreWF is a separate port) | Not planned |
| `Runtime/Hosting/WorkflowWebHostingModule.cs` | 1 | An ASP.NET `IHttpModule` (System.Web) | 2 |
| ASMX: `InvokeWebService`, `WebServiceReceive`, `WebServiceResponse`, `WebServiceFault`, `Executors/WorkflowWebService` | 5 | ASMX web services (System.Web.Services), which need webForCore | 2 |
| `Activities/Role/WebWorkflowRole.cs` | 1 | ASP.NET roles (System.Web.Security) | 2 |

The five Design files compiled for the runtime's sake, without their designer code: `DesignerInterfaces.cs`,
`DesignerHelpers.cs`, `PropertyDescriptors.cs`, `TypeConverters.cs`, `ActivityDesignerLayoutSerializers.cs`.

### Kept as they are

- **`msi.dll`** (`CompModHelpers.GetInstallDirectory`, called by a static constructor): the call is in a `try` and
  falls back to the assembly's folder when it fails, as it does off Windows; on Windows it finds what .NET Framework
  finds. No change was needed, so none was made (issue #1 proposed compiling it out).
- **`wldp.dll`** (`NativeMethods.IsDynamicCodePolicyEnabled`): nothing compiled calls it.
- **Active Directory roles** (`ActiveDirectoryRole`): compiled, with System.DirectoryServices; Windows only at run
  time, as on .NET Framework.
