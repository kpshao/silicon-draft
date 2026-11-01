# Exports

`GenerationPipeline.Export` is the only primary seam. `DatasetWriter.Write` plans every output path, applies overwrite protection, and writes the per-lot and per-wafer files.

## File map

| Export | Writer | Path |
| --- | --- | --- |
| Wafer Map | `WaferMapCsvWriter` | `<lotId>_wafermap.csv` |
| STDF CP | `StdfCpWriter` | `<lotId>_CP.stdf` |
| STDF FT | `StdfFtWriter` | `<lotId>_FT.stdf` |
| WAT TXT | `WatTxtWriter` | `<stem>_WAT_<template>.txt` |
| CP/FT CSV | `TestDataCsvWriter` | `csv/<waferId>_{CP,FT}.csv` |
| Manifest | `DatasetWriter` | `_truth/manifest.json` |

Supporting files:

- `ExportResult` carries every path array returned to the CLI and tests.
- `CliApplication` prints one line per export; a high-cardinality export prints a summary, not one path per file.
- `tests/SiliconDraft.Core.Tests/*ExportTests.cs` own format assertions; `GoldenExportTests` locks the full output.
- Golden files live under `tests/SiliconDraft.Core.Tests/Golden/` and are copied by the test csproj. Regenerate them deliberately with `SILICONDRAFT_UPDATE_GOLDEN=1 dotnet test`.

## Adding or changing an export

1. Write the format in a focused writer and put its file-name helper beside it.
2. Add the path array to `ExportResult` and thread it through `ExportResult.Exported` and `Failed`.
3. Plan the paths in `DatasetWriter.Write`, include them in the overwrite check, and write them in the same loop.
4. Add a CLI summary line; do not print one path per file for a per-wafer or per-die export.
5. Add or extend a `*ExportTests` seam test and a `GoldenExportTests` assertion.
6. Update README, GLOSSARY, and an ADR when the format is a contract; keep both languages in sync.
7. Run `dotnet build`, `dotnet format --verify-no-changes --no-restore`, and `dotnet test`.

## Search

Use `rg --files` or `git ls-files` to find source files. `bin/`, `obj/`, `out/`, and the STDF goldens are ignored or binary; use `rg` for content and leave them out of broad searches.

## Gotcha

Changing `ft.assemblyLoss` or `ft.escapeRate` changes the FT Units that feed the latent calibration, so CP results are not invariant across those changes. A test that varies either setting must derive expected counts from the generated `Dataset`, not from a probe run with a different setting. See ADR-0013.
