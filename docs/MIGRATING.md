# Moving a WF3 application to workflowForCore

Reference the `workflowForCore` package (README.md) instead of .NET Framework's System.Workflow.* assemblies. Code
compiled against .NET Framework's binds to workflowForCore's: the names, version (4.0.0.0) and public key token are the
same. What differs from .NET Framework 4.8 is listed here.

## Not available

- **Visual Studio's workflow designer and debugger**, and design-time types in general
  (`System.Workflow.ComponentModel.Design.ActivityDesigner` and the rest of the designer surface, the rule editor's
  dialogs). Attributes on WF3's types still name them, as on .NET Framework. A library whose own activities have
  designers deriving from WF3's can't be compiled against workflowForCore with them.
- **WF4 interop** (`System.Activities.Statements.Interop`).
- **For now (later phases):** XOML workflows compiled at run time (`WorkflowCompiler` is there, but code-beside
  generation isn't), the ASMX activities and `WorkflowWebHostingModule`, `WebWorkflowRole`, WF 3.5's WorkflowServices.

## Different

- **No COM+ transaction flow.** Atomic scopes use System.Transactions only.
- **Performance counters** exist on Windows only.
- **BinaryFormatter:** WF3 persists instances with webForCore's port of .NET Framework's BinaryFormatter
  (`webForCore.Serialization.Formatters`), wire-compatible with .NET Framework's. Passing .NET's `BinaryFormatter` to
  `Activity.Save`/`Load` works: WF3 uses webForCore's in its place, with the same settings. Code that calls
  `BinaryFormatter.Serialize` itself should use `WebFormsForCore.Serialization.Formatters.Binary.BinaryFormatter`.
- **No usage telemetry** is sent.
