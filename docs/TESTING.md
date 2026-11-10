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
