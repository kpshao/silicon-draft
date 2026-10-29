# SiliconDraft

SiliconDraft generates synthetic CP, FT, and WAT datasets with known injected properties, so a yield management system can be validated against ground truth.

## Status

A versioned JSON configuration flows through the Core `GenerationPipeline` facade. `silicondraft generate` writes a ground-truth manifest and one Wafer Map CSV per lot; `silicondraft preview` prints the effective wafer geometry and Die Map summary without writing files.

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

This writes `out/_truth/manifest.json` and `out/LOT-0001_wafermap.csv`. The manifest records the Resolved Config (including the effective wafer geometry), the seed, the Dataset identity, and Gross/Net/Partial Die counts per wafer, lot, and Dataset. The Wafer Map CSV lists every exported die with its Die Index coordinates, physical center, and valid/partial status. The same configuration and seed always produce the same files.

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
