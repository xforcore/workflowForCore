#!/usr/bin/env bash
# Runs workflowForCore's tests in Release after a build of workflowForCore.slnx (Release), as CI does (docs/TESTING.md).
# Every test project under test/ runs on .NET 10; on Windows, the ones that also target net48 run a second time on
# .NET Framework 4.8 itself, with Microsoft's System.Workflow.* from the GAC, and must give the same results.
# Usage: tools/ci/run-tests.sh [results-directory]
set -uo pipefail
cd "$(dirname "$0")/../.."
# The absolute path (realpath -m is GNU only, and macOS has BSD tools).
mkdir -p "${1:-TestResults}"
OUT=$(cd "${1:-TestResults}" && pwd)

status=0
trx=()
for project in test/*/*.csproj; do
  [ -f "$project" ] || continue
  name=$(basename "$project" .csproj)
  name=${name#workflowForCore.}
  frameworks=(net10.0)
  if [[ "${OS:-}" == "Windows_NT" ]] && grep -q 'net48' "$project"; then frameworks+=(net48); fi
  for tfm in "${frameworks[@]}"; do
    suffix=$([ "$tfm" = net48 ] && echo ".net48" || echo "")
    # --blame: if the test host crashes, the log names the test that was running.
    dotnet test "$project" -c Release --no-build -f "$tfm" --blame \
      --logger "trx;LogFileName=$name$suffix.trx" --results-directory "$OUT" || status=1
    trx+=("$OUT/$name$suffix.trx")
  done
done
if [ ${#trx[@]} -eq 0 ]; then
  echo "No test projects yet." | tee "$OUT/summary.md"
  exit 0
fi
dotnet run tools/ci/trx_summary.cs -- "${trx[@]}" > "$OUT/summary.md" || true
exit $status
