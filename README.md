# SiliconDraft

SiliconDraft generates synthetic CP, FT, and WAT datasets with known injected properties, so a yield management system can be validated against ground truth.

## Status

A versioned JSON configuration flows through the Core `GenerationPipeline` facade. `silicondraft generate` writes a ground-truth manifest, one Wafer Map CSV and one STDF V4 CP file per lot; `silicondraft preview` prints the effective wafer geometry, Die Map, and CP summary without writing files.

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

This writes `out/_truth/manifest.json`, `out/LOT-0001_wafermap.csv`, and `out/LOT-0001_CP.stdf`. The manifest records the Resolved Config (including the effective wafer geometry and CP settings), the seed, the Dataset identity, Gross/Partial Die counts and Net Die per wafer, lot, and Dataset, plus CP yield and Bin counts per lot and Dataset. Net Die is the number of valid die that pass CP. The Wafer Map CSV lists every exported die with its Die Index coordinates, physical center, and valid/partial status. The STDF V4 CP file contains FAR, MIR, one PIR/PTR/PRR group per tested die, and MRR. The same configuration and seed always produce the same files.

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

## CLI

```text
silicondraft generate -c <config> [-o <directory>] [--overwrite]
silicondraft preview -c <config>
```

- `-c`, `--config` — versioned JSON configuration file (required).
- `-o`, `--output` — override the configured output directory (generate only).
- `--overwrite` — replace existing output files (generate only).
- `preview` — print the effective wafer geometry and Gross/Net/Partial Die counts without writing files.
- `-h`, `--help` — show usage.

Exit codes: `0` success, `1` runtime error, `2` invalid configuration or arguments, `3` verification failure (reserved).

Relative output directories in the configuration are resolved against the configuration file. The manifest records the configuration, not the runtime destination, so changing `-o` does not change its contents.

## Layout

- `src/SiliconDraft.Core` — UI-free domain library; the `GenerationPipeline` facade (`Validate`, `Generate`, `Export`) is the only primary seam.
- `src/SiliconDraft.Cli` — thin CLI adapter over the facade.
- `tests/` — xUnit seam-level tests.
- `examples/` — sample configurations.
- `GLOSSARY.md`, `docs/adr/` — domain vocabulary and recorded decisions.
