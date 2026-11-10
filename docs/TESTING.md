# Tests

Microsoft published no tests for Windows Workflow Foundation 3, and Mono has none (its `System.Workflow.*` assemblies
are placeholders), so workflowForCore's tests are its own, written from scratch, one feature at a time, test first.
Each test also runs on **.NET Framework 4.8** with Microsoft's own System.Workflow.* (CI's Windows runner), and must
give the same result there: that is what makes a test a statement about WF3 rather than about workflowForCore.

## Running the tests

```bash
dotnet build workflowForCore.slnx -c Release
tools/ci/run-tests.sh            # every suite; .trx files and summary.md in ./TestResults
dotnet run tools/ci/check_code_rules.cs   # CONTRIBUTING.md rules 8 and 9 on workflowForCore's own code
```

On Windows, `run-tests.sh` runs each suite that also targets `net48` a second time on .NET Framework 4.8.

## CI

`.github/workflows/ci.yml` runs on Ubuntu 24.04 and Windows Server 2025 (under Git Bash) for every push and pull
request:

- **build-and-test** (Linux) and **build-windows**: the code rules, the build, the assemblies' identities against
  .NET Framework 4.8's (`tools/ci/check_assembly_identities.cs`: the reference assemblies on both, and the GAC on
  Windows), the tests (and on Windows the tests on .NET Framework 4.8), the packages and the package smoke test (an
  application that references only the `workflowForCore` package).
- **framework-data** (Windows, not a required check): reads what workflowForCore takes from .NET Framework 4.8 as data
  from the runner's .NET Framework again, the strings (`tools/framework_data.cs`), the configuration keys
  (`tools/framework_settings.cs`) and the SQL scripts, and fails if the committed copies differ. Its artifact has
  them, to commit when something is added.

## The suites

| Suite | Tests | What it exercises |
| --- | ---: | --- |
| workflowForCore.Tests | 27 | `RuntimeTests` (18): the runtime's start and stop, creating instances (ids, definitions), input and output parameters (and that `Name`, a metadata property of Activity, isn't one), the names CompositeActivity gives unnamed activities, unhandled exceptions and `TerminateActivity`, terminating, suspending and resuming from the host and with `SuspendActivity`, validation failures, `GetWorkflow` and the loaded instances, the manual scheduler (the caller's thread, nothing until asked) and the default one (thread pool, ten instances). `ActivityTests` (9): IfElse, While and Parallel (in WF3's order), fault handlers and `ThrowActivity`, Delay with and without the manual scheduler's active timers, Listen, a state machine through its states (`StateMachineWorkflowInstance`, state initialization and finalization) and `SetState` from the host |

**Latest run (10 Nov 2026, Linux x64, .NET 10, Release): 27 tests, 27 pass.** Written test first: before
workflowForCore's delegate serialization, 22 passed; every While iteration and state terminated the instance with
"Serializing delegates is not supported on this platform".
