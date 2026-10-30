# SiliconDraft

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 生成带有已知注入属性的合成 CP、FT 和 WAT 数据集，使良率管理系统可以对照 ground truth 进行验证。

## 状态

版本化 JSON 配置经由 Core 的 `GenerationPipeline` facade 流转。`silicondraft generate` 默认写出一个 Ground-Truth Manifest、每个 Lot 一个 Wafer Map CSV、一个 STDF V4 CP 文件和一个 STDF V4 FT 文件，以及每个 Lot 一个 WAT TXT 文件；`silicondraft preview` 打印生效的晶圆几何、Die Map、CP 与 FT 汇总以及 WAT 布局，不写任何文件。

## 要求

- .NET 10 SDK

## 构建与测试

```bash
dotnet build
dotnet test
```

## 生成数据集

```bash
dotnet run --project src/SiliconDraft.Cli -- generate -c examples/minimal.json -o out
```

这会写出 `out/_truth/manifest.json`、`out/LOT-0001_wafermap.csv`、`out/LOT-0001_CP.stdf`、`out/LOT-0001_FT.stdf` 和 `out/LOT-0001_WAT_long.txt`。manifest 记录 Resolved Config（包含生效的晶圆几何、CP 设置、FT 设置和 WAT sites）、seed、Dataset 身份、每个 wafer、lot 和 Dataset 的 Gross/Partial Die 计数与 Net Die，以及每个 lot 和 Dataset 的 CP 与 FT 良率和 Bin 计数。它还记录 FT catalog 版本、每个 Dataset、lot 和 wafer 的 FT Unit 与 Escape 计数，以及每个 Dataset 和 lot 的 Assembly Loss，还有 WAT catalog 版本、site 与 test item 计数，以及每个 Dataset、lot 和 wafer 的测量计数。Net Die 是通过 CP 的有效 die 数。Wafer Map CSV 列出每个导出的 die 及其 Die Index 坐标、物理中心和 valid/partial 状态。STDF V4 CP 文件包含 FAR、MIR、每个受测 die 一组 PIR/PTR/PRR，以及 MRR。STDF V4 FT 文件包含 FAR、每个 FT Unit 一条 ATR、MIR、每个 FT Unit 一组 PIR/PTR/PRR，以及 MRR。WAT TXT 文件每个 PCM Site 和 Test Item 一行。相同配置与 seed 总是产生相同的文件。

配置解析是严格的：未知属性、重复键、注释和尾随逗号都会被拒绝，每个错误都会指出出错的 JSON 路径。

## 晶圆几何

可选的 `wafer` 段默认使用 300 mm 晶圆、3 mm Edge Exclusion、5×5 mm die、0.08 mm scribe lane、notch 位于 270°、无 Die Grid Offset、旋转或镜像、`center` Die Index Origin，并排除 Partial Die。

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

- `die.pitch` 可选，默认为 die 尺寸加 scribe。
- `grid.rotationDeg` 为 0、90、180 或 270；`grid.mirrorX`/`mirrorY` 在旋转前镜像网格。
- `grid.indexOrigin` 为 `center`、`bottomLeft`、`topLeft`、`topRight` 或 `bottomRight`。
- `partialDiePolicy` 为 `exclude` 或 `retain`；保留的 Partial Die 以未测试的 `partial` 行出现，不计入 Gross Die。
- 几何在生成前校验：非法值、没有任何有效 die 的网格，以及过密的网格都会以退出码 2 拒绝。

参见 [`examples/geometry.json`](examples/geometry.json)（一个保留 Partial Die 的旋转偏移配置）以及 [ADR-0007](docs/adr/0007-wafer-geometry-conventions.md) 了解坐标约定。

## CP catalog 与 bins

内置的最小 CP catalog（`cp-catalog-v1`）包含六个 Numeric Test Item（Vt、Idsat、Iddq、RdsOn、Leakage、Vf）和两个 Functional Test Item（Scan、Bist）。每个 item 声明单位、默认分布、spec limits、test limits 和默认失败 Soft Bin。每个有效 Die 对每个 item 得到一个结果；Partial Die 不测试。

- 当结果落在 test limits 之外时 Test Item 失败；spec limits 与 test limits 一起导出。两个范围不做交叉校验，因此配置可以刻意设置更宽的 test limits。
- 第一个失败的 item 决定结果：Numeric 失败映射到 Parametric Fail Hard Bin，Functional 失败映射到 Functional Fail Hard Bin，item 的失败 Soft Bin 标识失败内容。通过的 die 得到 Pass Hard 与 Soft Bin。
- 可选的 `cp` 段覆盖 limits 和 Bin 编号。未知 item、非有限或倒置的 limits、越界的 Bin 编号，以及重复的 Soft Bin 都会以退出码 2 拒绝。
- manifest 记录 catalog 版本、良率，以及每个 Dataset 和 Lot 的 Hard/Soft Bin 计数；这些计数与 STDF 文件中的 PRR 记录对账。

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

STDF 设计记录在 [ADR-0008](docs/adr/0008-minimal-stdf-cp-export.md)。

## FT 与 CP-FT 追溯

内置的最小 FT catalog（`ft-catalog-v1`）包含六个 Numeric Test Item（Icc、Voh、Vol、Ioh、Iol、Tr）和两个 Functional Test Item（ScanFT、BistFT）。每个 FT Unit 都要经过全部 item 测试，并得到与 CP 相同的 Bin 处理：第一个失败的 item 决定结果。

- 默认情况下每个 CP 通过的 Die 恰好成为一个 FT Unit。`ft.assemblyLoss` 从 Dataset 中移除相应数量的 CP 通过 Die；`ft.escapeRate` 按该比例接纳 CP 失败的 Die 作为 Escape。因此 FT Unit 数 = CP 通过 Die − Assembly Loss + Escape。若 Dataset 的 CP 通过 Die 少于 `assemblyLoss`，生成失败。
- FT Unit 的稳定 id 为 `<waferId>:<x>,<y>`，与其 CP PART_ID 是同一字符串，因此 CP 与 FT 记录按来源 Die join。每个 Lot 的 Assembly Lot 为 `ALOT-<LOTID>`。
- FT 复用生效的 CP Bin 编号：pass Hard/Soft Bin 以及 parametric/functional 失败 Hard Bin；FT Test Item 携带自己的失败 Soft Bin。Failure Soft Bin 是 step-specific 的，因此同一个编号在 CP 和 FT 可能表示不同的失败。与 FT 失败 Soft Bin 冲突的 `cp.bins.passSoftBin` 会被拒绝。
- `assemblyLoss` 默认 0，必须大于等于 0；`escapeRate` 默认 0，必须在 0 与 1 之间。未知属性和非整数 `assemblyLoss` 会被拒绝。

```json
"ft": {
  "assemblyLoss": 5,
  "escapeRate": 0.02
}
```

STDF FT 文件（`<LOTID>_FT.stdf`）包含 FAR、每个 FT Unit 一条 ATR、`TEST_COD` 为 `FT` 的 MIR、每个 FT Unit 一组 PIR/PTR/PRR，以及 MRR。ATR 位于 FAR 与 MIR 之间，也就是 STDF V4 期望 audit 记录的位置。PRR 携带来源 Die X/Y，并以 FT Unit id 作为 PART_ID。每条 ATR 的 CMD_LINE 以 `KEY=VALUE` 对形式携带 `UNIT_ID`（与 PRR PART_ID 一致）、`SOURCE_WAFER_ID`、`SOURCE_DIE_X`、`SOURCE_DIE_Y`、`ASSEMBLY_LOT` 和 `CP_RESULT`，因此导入后记录可以 join，Escape 也可识别。manifest 中的 FT Units、Assembly Loss 和 Escapes 与文件对账。参见 [ADR-0010](docs/adr/0010-ft-traceability-and-stdf-export.md) 和 [`examples/ft.json`](examples/ft.json)。

## WAT catalog 与 TXT 导出

内置的最小 WAT catalog（`wat-catalog-v1`）包含八个 Numeric Test Item：Vtn、Vtp、IdsatN、IdsatP、Tox、Rs、Rc 和 Vbd。每个 Wafer 在每个 PCM Site 都进行测量：每个 site 上每个 Test Item 一个值，从 catalog 的默认分布采样。每个值由 Dataset seed、Lot、Wafer、Site 和 Test Item 播种，因此相同配置与 seed 可复现文件。

- 默认布局为九个 PCM Site：晶圆中心，加上晶圆中心到边缘中点处的八个点，按 45° 递增。坐标是相对晶圆中心的毫米值。
- `wat.sites` 替换默认布局。每个 site 需要 `id`、`xMm` 和 `yMm`；id 必须唯一，且每个 site 必须位于晶圆内。
- `wat.template` 为 `long`（默认）或 `wide`。
- `wat.organization` 为 `perLot`（默认）或 `perWafer`。

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

Long TXT（`<stem>_WAT_long.txt`）在每个制表符分隔行上重复 site、Test Item、值、单位和 limits：`lotId`、`waferId`、`site`、`siteXmm`、`siteYmm`、`testItem`、`testNumber`、`value`、`unit`、`lowSpec`、`highSpec`、`lowTest`、`highTest`。Wide TXT（`<stem>_WAT_wide.txt`）每个 wafer 每个 site 一行，将每个 Test Item 的值放入独立列，并在开头的 `#` 定义块中记录每个 item 的编号、单位、spec limits 和 test limits。两种格式都以 `# catalogVersion=<version>` 开头。

文件 stem 默认是 Lot id（`LOT-0001_WAT_long.txt`），或在 `perWafer` 时是 Wafer id（`LOT-0001-W01_WAT_long.txt`）。manifest 记录 WAT catalog 版本、site 与 test item 计数以及测量计数；导出字段始终与该 catalog 版本一致。格式记录在 [ADR-0009](docs/adr/0009-wat-txt-export.md)。宽表、按 Wafer 的配置参见 [`examples/wat.json`](examples/wat.json)。

## CLI

```text
silicondraft generate -c <config> [-o <directory>] [--overwrite]
silicondraft preview -c <config>
```

- `-c`、`--config` — 版本化 JSON 配置文件（必填）。
- `-o`、`--output` — 覆盖配置的输出目录（仅 generate）。
- `--overwrite` — 替换已有输出文件（仅 generate）。
- `preview` — 打印生效的晶圆几何、Gross/Net/Partial Die 计数、FT 汇总和 WAT 布局，不写文件。
- `-h`、`--help` — 显示用法。

退出码：`0` 成功，`1` 运行时错误，`2` 非法配置或参数，`3` 校验失败（预留）。

配置中的相对输出目录相对于配置文件解析。manifest 记录的是配置，而不是运行时目标，因此更改 `-o` 不会改变其内容。

## 目录结构

- `src/SiliconDraft.Core` — 无 UI 的领域库；`GenerationPipeline` facade（`Validate`、`Generate`、`Export`）是唯一的 primary seam。
- `src/SiliconDraft.Cli` — 位于 facade 之上的薄 CLI 适配器。
- `tests/` — xUnit seam 级测试。
- `examples/` — 示例配置。
- `GLOSSARY.md`、`docs/adr/` — 领域词汇与决策记录。

<details>
<summary>English</summary>

# SiliconDraft

SiliconDraft generates synthetic CP, FT, and WAT datasets with known injected properties, so a yield management system can be validated against ground truth.

## Status

A versioned JSON configuration flows through the Core `GenerationPipeline` facade. `silicondraft generate` writes a ground-truth manifest, one Wafer Map CSV, one STDF V4 CP file, and one STDF V4 FT file per lot, and one WAT TXT file per lot by default; `silicondraft preview` prints the effective wafer geometry, Die Map, CP and FT summaries, and WAT layout without writing files.

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

This writes `out/_truth/manifest.json`, `out/LOT-0001_wafermap.csv`, `out/LOT-0001_CP.stdf`, `out/LOT-0001_FT.stdf`, and `out/LOT-0001_WAT_long.txt`. The manifest records the Resolved Config (including the effective wafer geometry, CP settings, FT settings, and WAT sites), the seed, the Dataset identity, Gross/Partial Die counts and Net Die per wafer, lot, and Dataset, plus CP and FT yield and Bin counts per lot and Dataset. It also records the FT catalog version, the FT Unit and Escape counts per Dataset, lot, and wafer, and the Assembly Loss per Dataset and lot, plus the WAT catalog version, site and test item counts, and measurement counts per Dataset, lot, and wafer. Net Die is the number of valid die that pass CP. The Wafer Map CSV lists every exported die with its Die Index coordinates, physical center, and valid/partial status. The STDF V4 CP file contains FAR, MIR, one PIR/PTR/PRR group per tested die, and MRR. The STDF V4 FT file contains FAR, one ATR per FT Unit, MIR, one PIR/PTR/PRR group per FT Unit, and MRR. The WAT TXT file contains one row per PCM Site and Test Item. The same configuration and seed always produce the same files.

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

## FT and CP-FT traceability

The built-in minimal FT catalog (`ft-catalog-v1`) has six Numeric Test Items (Icc, Voh, Vol, Ioh, Iol, Tr) and two Functional Test Items (ScanFT, BistFT). Every FT Unit is tested against all of them and gets the same Bins treatment as CP: the first failing item decides the outcome.

- By default every CP-passing Die becomes exactly one FT Unit. `ft.assemblyLoss` removes that many CP-passing Die from the Dataset; `ft.escapeRate` admits that fraction of CP-failing Die as Escapes. The FT Unit count is therefore CP-passing Die minus Assembly Loss plus Escapes. A Dataset with fewer CP-passing Die than `assemblyLoss` fails generation.
- An FT Unit's stable id is `<waferId>:<x>,<y>`, the same string as its CP PART_ID, so CP and FT records join on the source Die. Each Lot's Assembly Lot is `ALOT-<LOTID>`.
- FT reuses the effective CP Bin numbering for the pass Hard/Soft Bin and the parametric/functional failure Hard Bins; FT Test Items carry their own failure Soft Bins. Failure Soft Bins are step-specific, so the same number may denote a different failure at CP and FT. A `cp.bins.passSoftBin` that collides with an FT failure Soft Bin is rejected.
- `assemblyLoss` defaults to 0 and must be 0 or greater; `escapeRate` defaults to 0 and must be between 0 and 1. Unknown properties and non-integer `assemblyLoss` are rejected.

```json
"ft": {
  "assemblyLoss": 5,
  "escapeRate": 0.02
}
```

The STDF FT file (`<LOTID>_FT.stdf`) contains FAR, one ATR per FT Unit, MIR with `TEST_COD` `FT`, one PIR/PTR/PRR group per FT Unit, and MRR. The ATRs sit between FAR and MIR, where STDF V4 expects audit records. PRR carries the source Die X/Y and the FT Unit id as PART_ID. Each ATR's CMD_LINE carries `UNIT_ID` (matching the PRR PART_ID), `SOURCE_WAFER_ID`, `SOURCE_DIE_X`, `SOURCE_DIE_Y`, `ASSEMBLY_LOT`, and `CP_RESULT` as `KEY=VALUE` pairs, so records join after import and Escapes are identifiable. The manifest's FT Units, Assembly Loss, and Escapes reconcile with the file. See [ADR-0010](docs/adr/0010-ft-traceability-and-stdf-export.md) and [`examples/ft.json`](examples/ft.json).

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

Long TXT (`<stem>_WAT_long.txt`) repeats site, Test Item, value, unit, and limits on every tab-separated row: `lotId`, `waferId`, `site`, `siteXmm`, `siteYmm`, `testItem`, `testNumber`, `value`, `unit`, `lowSpec`, `highSpec`, `lowTest`, `highTest`. Wide TXT (`<stem>_WAT_wide.txt`) writes one row per site per wafer, puts each Test Item value in its own column, and records each item's number, unit, spec limits, and test limits in a leading `#` definition block. Both formats start with `# catalogVersion=<version>`.

The file stem is the Lot id by default (`LOT-0001_WAT_long.txt`) or the Wafer id with `perWafer` (`LOT-0001-W01_WAT_long.txt`). The manifest records the WAT catalog version, site and test item counts, and measurement counts; the exported fields always match that catalog version. The format is recorded in [ADR-0009](docs/adr/0009-wat-txt-export.md). See [`examples/wat.json`](examples/wat.json) for a wide, per-Wafer configuration.

## CLI

```text
silicondraft generate -c <config> [-o <directory>] [--overwrite]
silicondraft preview -c <config>
```

- `-c`, `--config` — versioned JSON configuration file (required).
- `-o`, `--output` — override the configured output directory (generate only).
- `--overwrite` — replace existing output files (generate only).
- `preview` — print the effective wafer geometry, Gross/Net/Partial Die counts, FT summary, and WAT layout without writing files.
- `-h`, `--help` — show usage.

Exit codes: `0` success, `1` runtime error, `2` invalid configuration or arguments, `3` verification failure (reserved).

Relative output directories in the configuration are resolved against the configuration file. The manifest records the configuration, not the runtime destination, so changing `-o` does not change its contents.

## Layout

- `src/SiliconDraft.Core` — UI-free domain library; the `GenerationPipeline` facade (`Validate`, `Generate`, `Export`) is the only primary seam.
- `src/SiliconDraft.Cli` — thin CLI adapter over the facade.
- `tests/` — xUnit seam-level tests.
- `examples/` — sample configurations.
- `GLOSSARY.md`, `docs/adr/` — domain vocabulary and recorded decisions.

</details>
