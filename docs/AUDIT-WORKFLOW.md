# XboxJellyfish Audit Workflow

The **Audit Xbox AppX** GitHub Actions workflow is the pre-release gate for the package. It runs automatically for pull requests targeting `main` or `master`, and it can also be started manually from **Actions → Audit Xbox AppX → Run workflow**.

## What it checks

- Restores the solution with the Windows/UWP toolchain.
- Runs the `JellyfinClient.Tests` test project.
- Builds the x64 Release AppX/MSIX package with the same packaging path used by the normal build.
- Verifies that the package contains an x64 manifest and the self-contained .NET runtime.
- Verifies that the matching signing certificate is included.
- Writes `audit-report.md` and `SHA256SUMS.txt` into the `jellyfinxbox-audit` artifact.

The audit workflow does **not** publish to a store, deploy to an Xbox, or claim that Xbox hardware validation has passed.

## After a successful audit

Download the `jellyfinxbox-audit` artifact, install that exact package on the test Xbox, and complete [`XBOX-MANUAL-TEST-MATRIX.md`](XBOX-MANUAL-TEST-MATRIX.md). Record the commit and package hash with the manual results. A successful GitHub audit is necessary, but it is not a replacement for the Xbox gate.
