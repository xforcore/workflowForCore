# Contributing to workflowForCore

workflowForCore runs Microsoft's Windows Workflow Foundation 3 (System.Workflow.*) on .NET 10, on Linux as well as
Windows. A change is welcome when it brings WF3 on .NET closer to what it does on .NET Framework 4.8, and when it can
prove it with a test.

## The principles

1. **.NET Framework 4.8 is the measure.** WF3 on workflowForCore should behave as on .NET Framework 4.8: the same
   workflows give the same outcomes, the same tracking records, the same exceptions and messages, and state persisted
   by one resumes on the other. Microsoft published no tests for WF3, so workflowForCore's tests are its own, and each
   also runs on .NET Framework 4.8 (CI's Windows runner) with the same expected results ([docs/TESTING.md](docs/TESTING.md)).
   A test that only passes on workflowForCore is wrong, or documents a difference listed in
   [docs/MIGRATING.md](docs/MIGRATING.md).

2. **Every change comes with a test**, written first: say in the pull request that it failed before the change, on
   workflowForCore, and that it passes on .NET Framework 4.8.

3. **Avoid changing Microsoft's code.** WF3 comes unchanged, apart from a short list of changes
   ([docs/MODIFICATIONS.md](docs/MODIFICATIONS.md)), from the `external/referencesource` submodule. Its value is that
   it *is* Microsoft's code. Code is lifted only from microsoft/referencesource, never from dotnet/runtime: where .NET
   has no counterpart for something WF3 calls and the Reference Source doesn't have it either, workflowForCore writes
   its own, small and documented, in `src/` (not in the submodule).

4. **When Microsoft's code must change, keep the record.** Keep the change as small as it can be and:
   - make it a commit on the fork's `workflowforcore` branch (xforcore/referencesource), one problem per commit, with
     a `// workflowForCore:` comment and a message that say why. Code that .NET can't run is left in place under
     `#if !WORKFLOWFORCORE` rather than deleted, so the file still reads as Microsoft's;
   - push the branch (fast-forward only, never force) and move the submodule to it in a workflowForCore pull request,
     with the test;
   - record it in [docs/MODIFICATIONS.md](docs/MODIFICATIONS.md), and in [docs/LIFT.md](docs/LIFT.md) if it changes
     what is compiled.

5. **The project files are workflowForCore's.** They compile the submodule's files through `Link` items at their old
   paths, next to workflowForCore's own files, and are edited by hand. What isn't compiled is listed in them with the
   reason.

6. **Report numbers.** A pull request says what the tests gave before and after: tests run, passed, failed and
   skipped, on Linux, on Windows and on .NET Framework 4.8.

7. **Every change names what needs it.** Say which application hit the problem (the
   [webForCore smoke test](https://github.com/xforcore/webForCore-smoke-test)'s applications are a good source:
   Composite C1 runs its console on WF3), or what .NET Framework's WF3 does.

## How workflowForCore's own code is written

These are webForCore's rules for its own code (its CONTRIBUTING.md, 6–9), applied here to `src/Shared`, each
assembly's `workflowForCore/` folder, the tests and the tools. They don't apply to Microsoft's code in the submodule
(principle 3 keeps that as it is). The aim is the same: code a reviewer can see is correct, that keeps working when
.NET is patched, and that fails safely.

8. **No reflection into internals.** Don't read, write or call non-public members of the runtime or of Microsoft's
   types, and don't depend on their private names or layout. Use public APIs. If a behaviour needs something that
   isn't exposed, document the difference with a test instead.

9. **No unsafe code, no P/Invoke and no GC tricks:** no `unsafe`, `Unsafe`/`MemoryMarshal`, `DllImport`, `GCHandle`,
   `GC.Collect` or weak references.

10. **Slow, secure and stable beats fast.** Don't optimise before an application shows a problem with a measurement.

11. **Programs and tools are .NET.** Everything this repository builds, and every tool it uses (CI helpers, checks),
    is written in C# as a project or as a file-based app run with `dotnet run tool.cs`. No Python, Node.js, Ruby, Perl
    or other general-purpose language. Shell scripts (bash, PowerShell) and CI YAML are fine as glue that calls
    dotnet, git, gh and curl. Anything that parses data or makes decisions belongs in C#.

`tools/ci/check_code_rules.cs` checks rules 8 and 9; CI runs it.

## Pull requests

- One problem per commit, with `Fixes #<issue>` in the message where there is one.
- The pull request lists, for each change: the problem, what needs it (principle 7), the fix, the test and the
  numbers (principle 6), and whether Microsoft's code changed (and where that is documented).
- CI (`.github/workflows/ci.yml`) builds, runs the tests, packs the packages and runs the package smoke test, on
  Linux and on Windows, and on Windows runs the tests on .NET Framework 4.8 too; all must pass. Its
  `framework-data` job reads what workflowForCore takes from .NET Framework as data again and fails if it changed.

## Releases

1. Set `<Version>` in `Directory.Build.props` and write `docs/release-notes/<version>.md`.
2. Update the test numbers in README.md and docs/TESTING.md.
3. Tag the merged commit `v<version>`. CI builds and tests the tag on both platforms, creates the GitHub release with
   the packages, and rebuilds the NuGet feed.

Licences: workflowForCore's own code is MIT, and Microsoft's code keeps its MIT licence
([`docs/LICENSING.md`](docs/LICENSING.md)). Contributions are accepted under the MIT License.
