# Development Log

This log records user-visible feature work, packaging changes, and release-facing project decisions.

## 2026-05-25

### Added Development Log Policy

- Added this root-level development log as the canonical place to record feature additions and release-facing changes.
- Updated `brightnessControl/AGENTS.md` so future feature work includes a matching development log entry.

### Added Packaging Artifact Ignore Rule

- Added `artifacts/` to `.gitignore` so generated publish outputs and zip files are not committed.
- Verified a Windows x64 self-contained publish output can be produced under `artifacts/brightnessControl-win-x64`.
- Created a portable zip package named `brightnessControl-win-x64.zip` for manual GitHub release upload.

### Clarified MIT Licensing

- Added project metadata for the MIT license.
- Documented the project license in the README.
- Added third-party notices for self-contained .NET runtime distribution and optional NVAPI driver usage.
- Included license, README, and third-party notices in publish outputs and release artifacts.
