# Licensing

A plain-language summary. [NOTICE](../NOTICE) is the authoritative list of components, sources and copyright holders.
This isn't legal advice.

## Can I use workflowForCore in a commercial product?

Yes. Everything in the packages is under the MIT License.

| Part | Where | Licence | Commercial use |
| --- | --- | --- | --- |
| Windows Workflow Foundation 3 (Microsoft's code, .NET Framework 4.8 Reference Source) | `external/referencesource`, built by `src/workflowForCore.System.Workflow.*` | MIT (© Microsoft) | ✅ Yes |
| .NET Framework 4.8's WF3 strings and SQL scripts, read as data | `src/*/FrameworkData`, `src/FrameworkData` | MIT (© Microsoft) | ✅ Yes |
| workflowForCore's own code | `src/Shared`, `src/*/workflowForCore`, `tools/`, `test/` | MIT (© workflowForCore contributors) | ✅ Yes |
| .NET's packages (System.Data.SqlClient, System.CodeDom, ...) from NuGet | restored at build time | MIT (© .NET Foundation) | ✅ Yes |
| webForCore.Serialization.Formatters (from webForCore's feed) | restored at build time | MIT (© .NET Foundation, webForCore contributors) | ✅ Yes |

### What the licence asks of you

When you distribute the code or binaries (including inside a commercial product), include the copyright notice and
the licence text.

## Names and signatures

The assemblies keep Microsoft's names (System.Workflow.ComponentModel, System.Workflow.Runtime,
System.Workflow.Activities), version (4.0.0.0) and public key, so that code compiled against them, and workflow state
persisted by .NET Framework, binds without changes. They are delay signed with that public key, as webForCore's
assemblies with Microsoft's identities are (`keys/35MSSharedLib1024.snk` holds only the public key, token
31bf3856ad364e35); they aren't signed by Microsoft, and workflowForCore isn't affiliated with or endorsed by Microsoft.
.NET doesn't check strong-name signatures, so delay-signed assemblies load as they are.

## Telemetry

.NET Framework's System.Workflow.Runtime sent a usage event to Microsoft's telemetry provider the first time an
application used `WorkflowRuntime`. workflowForCore doesn't compile that code ([MODIFICATIONS.md](MODIFICATIONS.md))
and sends nothing.
