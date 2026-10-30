# SiliconDraft

SiliconDraft generates synthetic CP, FT, and WAT datasets with known injected properties, so a yield management system can be validated against ground truth.

## Status

A versioned JSON configuration flows through the Core `GenerationPipeline` facade. `silicondraft generate` writes a ground-truth manifest, one Wafer Map CSV, one STDF V4 CP file, and one WAT TXT file per lot; `silicondraft preview` prints the effective wafer geometry, Die Map, CP summary, and WAT layout without writing files.

## Requirements

- .NET 10 SDK

## Build and test

```bash
dotnet build
dotnet test
```

## Generate a dataset

```bash
dotnet run --project src/SiliconDraft.Cli -- generate -c examples/minimal.json -o out
```

This writes `out/_truth/manifest.json`, `out/LOT-0001_wafermap.csv`, `out/LOT-0001_CP.stdf`, and `out/LOT-0001_WAT_long.txt`. The manifest records the Resolved Config (including the effective wafer geometry, CP settings, and WAT sites), the seed, the Dataset identity, Gross/Partial Die counts and Net Die per wafer, lot, and Dataset, plus CP yield and Bin counts per lot and Dataset. It also records the WAT catalog version, site and test item counts, and measurement counts per Dataset, lot, and wafer. Net Die is the number of valid die that pass CP. The Wafer Map CSV lists every exported die with its Die Index coordinates, physical center, and valid/partial status. The STDF V4 CP file contains FAR, MIR, one PIR/PTR/PRR group per tested die, and MRR. The WAT TXT file contains one row per PCM Site and Test Item. The same configuration and seed always produce the same files.

Configuration parsing is strict: unknown properties, duplicate keys, comments, and trailing commas are rejected, and every error names the offending JSON path.

## Wafer geometry

The optional `wafer` section defaults to a 300 mm wafer with a 3 mm Edge Exclusion, a 5×5 mm die, a 0.08 mm scribe lane, the notch at 270°, no Die Grid Offset, rotation, or mirroring, a `center` Die Index Origin, and Partial Die excluded.

```json
"wafer": {
  "diameterMm": 300,
  "edgeExclusionMm": 3,
  "notchAngleDeg": 270,
  "die": {
    "widthMm": 5,
    "heightMm": 5,
    "scribeMm": 0.08,
    "pitch": { "widthMm": 5.08, "heightMm": 5.08 }
  },
  "grid": {
    "offsetMm": { "x": 0, "y": 0 },
    "rotationDeg": 0,
    "mirrorX": false,
    "mirrorY": false,
    "indexOrigin": "center"
  },
  "partialDiePolicy": "exclude"
}
```

- `die.pitch` is optional and defaults to die size plus scribe.
- `grid.rotationDeg` is 0, 90, 180, or 270; `grid.mirrorX`/`mirrorY` reflect the grid before rotation.
- `grid.indexOrigin` is `center`, `bottomLeft`, `topLeft`, `topRight`, or `bottomRight`.
- `partialDiePolicy` is `exclude` or `retain`; retained Partial Die appear as untested `partial` rows and do not count toward Gross Die.
- Geometry is validated before generation: invalid values, a grid with no valid die, and excessively dense grids are rejected with exit code 2.

See [`examples/geometry.json`](examples/geometry.json) for a rotated, offset configuration that retains Partial Die, and [ADR-0007](docs/adr/0007-wafer-geometry-conventions.md) for the coordinate conventions.

## CP catalog and bins

The built-in minimal CP catalog (`cp-catalog-v1`) has six Numeric Test Items (Vt, Idsat, Iddq, RdsOn, Leakage, Vf) and two Functional Test Items (Scan, Bist). Each item declares a unit, a default distribution, spec limits, test limits, and a default failure Soft Bin. Every valid Die receives one result per item; Partial Die are not tested.

- A Test Item fails when its result falls outside its test limits; spec limits are exported alongside them. The two ranges are not cross-validated, so a configuration can deliberately set wider test limits.
- The first failing item decides the outcome: a Numeric failure maps to the Parametric Fail Hard Bin, a Functional failure to the Functional Fail Hard Bin, and the item's failure Soft Bin identifies what failed. Passing die get the Pass Hard and Soft Bins.
- The optional `cp` section overrides limits and Bin numbers. Unknown items, non-finite or inverted limits, out-of-range Bin numbers, and duplicate Soft Bins are rejected with exit code 2.
- The manifest records the catalog version, yield, and Hard/Soft Bin counts per Dataset and Lot; those counts reconcile with the PRR records in the STDF file.

```json
"cp": {
  "bins": {
    "passHardBin": 1,
    "passSoftBin": 1,
    "parametricFailHardBin": 2,
    "functionalFailHardBin": 3
  },
  "testItems": [
    {
      "name": "Vt",
      "specLimits": { "low": 0.39, "high": 0.51 },
      "testLimits": { "low": 0.4, "high": 0.5 },
      "failSoftBin": 100
    }
  ]
}
```

The STDF design is recorded in [ADR-0008](docs/adr/0008-minimal-stdf-cp-export.md).

## WAT catalog and TXT export

The built-in minimal WAT catalog (`wat-catalog-v1`) has eight Numeric Test Items: Vtn, Vtp, IdsatN, IdsatP, Tox, Rs, Rc, and Vbd. Every Wafer is measured at every PCM Site: one value per Test Item at each site, sampled from the catalog's default distribution. Each value is seeded from the Dataset seed, Lot, Wafer, Site, and Test Item, so the same configuration and seed reproduce the files.

- The default layout is nine PCM Sites: the wafer center plus eight points halfway between the center and the wafer edge, at 45° increments. Coordinates are millimeters from the wafer center.
- `wat.sites` replaces the default layout. Each site needs an `id`, `xMm`, and `yMm`; ids must be unique and every site must lie within the wafer.
- `wat.template` is `long` (default) or `wide`.
- `wat.organization` is `perLot` (default) or `perWafer`.

```json
"wat": {
  "sites": [
    { "id": "C", "xMm": 0, "yMm": 0 },
    { "id": "N", "xMm": 0, "yMm": 75 },
    { "id": "E", "xMm": 75, "yMm": 0 }
  ],
  "template": "wide",
  "organization": "perWafer"
}
```

Long TXT (`<stem>_WAT_long.txt`) repeats site, Test Item, value, unit, and limits on every tab-separated row: `lotId`, `waferId`, `site`, `siteXmm`, `siteYmm`, `testItem`, `testNumber`, `value`, `unit`, `lowSpec`, `highSpec`, `lowTest`, `highTest`. Wide TXT (`<stem>_WAT_wide.txt`) writes one row per site, puts each Test Item value in its own column, and records each item's number, unit, spec limits, and test limits in a leading `#` definition block. Both formats start with `# catalogVersion=<version>`.

The file stem is the Lot id by default (`LOT-0001_WAT_long.txt`) or the Wafer id with `perWafer` (`LOT-0001-W01_WAT_long.txt`). The manifest records the WAT catalog version, site and test item counts, and measurement counts; the exported fields always match that catalog version. The format is recorded in [ADR-0009](docs/adr/0009-wat-txt-export.md). See [`examples/wat.json`](examples/wat.json) for a wide, per-Wafer configuration.

## CLI

```text
silicondraft generate -c <config> [-o <directory>] [--overwrite]
silicondraft preview -c <config>
```

- `-c`, `--config` — versioned JSON configuration file (required).
- `-o`, `--output` — override the configured output directory (generate only).
- `--overwrite` — replace existing output files (generate only).
- `preview` — print the effective wafer geometry, Gross/Net/Partial Die counts, and WAT layout without writing files.
- `-h`, `--help` — show usage.

Exit codes: `0` success, `1` runtime error, `2` invalid configuration or arguments, `3` verification failure (reserved).

Relative output directories in the configuration are resolved against the configuration file. The manifest records the configuration, not the runtime destination, so changing `-o` does not change its contents.

## Layout

- `src/SiliconDraft.Core` — UI-free domain library; the `GenerationPipeline` facade (`Validate`, `Generate`, `Export`) is the only primary seam.
- `src/SiliconDraft.Cli` — thin CLI adapter over the facade.
- `tests/` — xUnit seam-level tests.
- `examples/` — sample configurations.
- `GLOSSARY.md`, `docs/adr/` — domain vocabulary and recorded decisions.
