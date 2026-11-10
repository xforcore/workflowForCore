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
