# Obligation ledger

| Gate | Commit | Command | Result | Evidence artifact hash | Timestamp |
| --- | --- | --- | --- | --- | --- |
| P0 build | working tree at `cf700c7` plus uncommitted Phase 0 implementation | `dotnet build CSharpAssay.slnx --no-restore -c Release` | passed; 0 warnings, 0 errors | n/a (console gate) | 2026-07-31T07:00:51Z |
| P0 tests | working tree at `cf700c7` plus uncommitted Phase 0 implementation | `dotnet test CSharpAssay.slnx --no-build -c Release` | passed; 30 succeeded, 0 failed, 0 skipped | n/a (MTP console gate) | 2026-07-31T07:00:51Z |
| P0 deterministic JSON | working tree at `cf700c7` plus uncommitted Phase 0 implementation | two independent CLI `check` runs plus byte comparison | passed; JSON and SARIF are byte-identical | JSON `b34ae413565de24c6779179ac759c37b43bd837d38b50174e80ea99278a9097e`; SARIF `689573867dbe7fa9031cf47ae05e1e527c99ae745d510d7fe90776f952dda68f` | 2026-07-31T07:00:51Z |
| P0 native capability | working tree at `cf700c7` plus uncommitted Phase 0 implementation | `cs-assay doctor` | unavailable, correctly reported; stable compatibility lane ready | n/a (console gate) | 2026-07-31T07:00:51Z |
| P1 locked restore | working tree at `cf700c7` plus uncommitted Phase 0–2 implementation | `dotnet restore CSharpAssay.slnx --locked-mode` | passed; all 14 projects resolved from lock files | n/a (console gate) | 2026-07-31T07:36:22Z |
| P1 build | working tree at `cf700c7` plus uncommitted Phase 0–2 implementation | `dotnet build CSharpAssay.slnx --no-restore -c Release` | passed; 0 warnings, 0 errors | n/a (console gate) | 2026-07-31T07:36:22Z |
| P1 tests | working tree at `cf700c7` plus uncommitted Phase 0–2 implementation | `dotnet test CSharpAssay.slnx --no-build --no-restore -c Release` | passed; 49 succeeded, 0 failed, 0 skipped | n/a (MTP console gate) | 2026-07-31T07:36:22Z |
| P1 TypeGym and closure | working tree at `cf700c7` plus uncommitted Phase 0–2 implementation | full solution test run | passed; TG01–TG15 plus rule closure/fault/adjudication tests | n/a (MTP console gate) | 2026-07-31T07:36:22Z |
| P2 null self-assay | working tree at `cf700c7` plus uncommitted Phase 0–2 implementation | two independent `cs-assay check CSharpAssay.slnx` runs | provisional Pass on both runs: 14 projects, 0 findings, 0 missing, 0 failures; JSON and SARIF byte-identical | JSON `eee0a63ce9adc673b2eb23e4870282d5c83894da687d14f34cfa6aac8b7dd592`; SARIF `689573867dbe7fa9031cf47ae05e1e527c99ae745d510d7fe90776f952dda68f` | 2026-07-31T07:41:47Z |

Ledger rows are updated only from executed commands. Planning intent is not a
result.
