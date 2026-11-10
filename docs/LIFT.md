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
