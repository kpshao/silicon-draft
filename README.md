# SiliconDraft

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 生成带有已知注入属性的合成 CP、FT 和 WAT 数据集，使良率管理系统可以对照 ground truth 进行验证。

## 状态

版本化 JSON 配置经由 Core 的 `GenerationPipeline` facade 流转。`silicondraft generate` 默认写出一个 Ground-Truth Manifest、每个 Lot 一个 Wafer Map CSV、一个 STDF V4 CP 文件和一个 STDF V4 FT 文件、每个 Lot 一个 WAT TXT 文件，以及 `csv/` 下每个 Wafer 的 CP CSV 和 FT CSV；`silicondraft preview` 打印生效的晶圆几何、Die Map、CP 与 FT 汇总以及 WAT 布局，不写任何文件。

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

这会写出 `out/_truth/manifest.json`、`out/LOT-0001_wafermap.csv`、`out/LOT-0001_CP.stdf`、`out/LOT-0001_FT.stdf`、`out/LOT-0001_WAT_long.txt`、`out/csv/LOT-0001-W01_CP.csv` 和 `out/csv/LOT-0001-W01_FT.csv`。manifest 记录 Resolved Config（包含生效的晶圆几何、CP 设置、FT 设置和 WAT sites）、seed、Dataset 身份、每个 wafer、lot 和 Dataset 的 Gross/Partial Die 计数与 Net Die，以及每个 lot 和 Dataset 的 CP 与 FT 良率和 Bin 计数。它还记录 FT catalog 版本、每个 Dataset、lot 和 wafer 的 FT Unit 与 Escape 计数，以及每个 Dataset 和 lot 的 Assembly Loss，还有 WAT catalog 版本、site 与 test item 计数，以及每个 Dataset、lot 和 wafer 的测量计数。`dataset.correlations.die` 记录每个 die 级 CP-FT 关联对的目标 r、容差、实际 r 与样本数。Net Die 是通过 CP 的有效 die 数。Wafer Map CSV 列出每个导出的 die 及其 Die Index 坐标、物理中心和 valid/partial 状态。STDF V4 CP 文件包含 FAR、MIR、SDR，每个 Wafer 的 WIR/WCR，每个受测 die 一组 PIR、按 item 类型生成的 PTR 或 FTR、以及 PRR，每个 Wafer 的 WRR，随后是 TSR/HBR/SBR/PCR 汇总，最后由 MRR 收尾。STDF V4 FT 文件包含 FAR、每个 FT Unit 一条 ATR、MIR、SDR，每个 FT Unit 一组 PIR、PTR 或 FTR、以及 PRR，以及相同的 lot 汇总和 MRR；FT 是封装测试，不写 WIR/WCR/WRR。WAT TXT 文件每个 PCM Site 和 Test Item 一行。相同配置与 seed 总是产生相同的文件。

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

内置的完整 CP catalog v1（`cp-catalog-v4`）包含 20 个 Numeric Test Item 和 3 个 Functional Test Item。Numeric item 覆盖阈值电压（Vt、Vtp、Vtlin、Vtsat、Vfb）、驱动电流与跨导（Idsat、IdsatP、Gm）、漏电（Iddq、Leakage、Ioff）、导通与接触电阻（RdsOn、Rs、Rc）、栅氧特性（Cox、Vbd、Ssw、Dibl）、电容（Cin）和正向压降（Vf）；Functional item 为 Scan、Bist 和 Mbist。每个 item 声明单位、默认分布、spec limits、test limits 和默认失败 Soft Bin；Numeric item 还声明 8 个 Tester Site 的默认测量偏移。驱动电流（Idsat、IdsatP）和漏电项使用 lognormal 默认分布，阈值电压与其他参数使用 normal；每个有效 Die 对每个 item 得到一个结果；Partial Die 不测试。

- 当结果落在 test limits 之外时 Test Item 失败；spec limits 与 test limits 一起导出。两个范围不做交叉校验，因此配置可以刻意设置更宽的 test limits。
- 第一个失败的 item 决定结果：Numeric 失败映射到 Parametric Fail Hard Bin，Functional 失败映射到 Functional Fail Hard Bin，item 的失败 Soft Bin 标识失败内容。通过的 die 得到 Pass Hard 与 Soft Bin。
- CP 默认在 8 个 Tester Site 上并行测试。`cp.siteCount` 可配置为 1–254；每个 Die 的 Tester Site 由 seed 与 Die 身份确定性派生，因此与执行顺序无关。
- Numeric Test Item 支持 per-item site offset：`cp.testItems[].siteOffsets` 必须为每个 Tester Site 提供一个有限的加性偏移，单位与 item 相同。未配置时使用 catalog 的默认偏移模式，并按 `siteCount` 循环展开。Functional item 不接受 site offsets。
- 可选的 `cp` 段覆盖 limits、Bin 编号和 site 设置。`cp.testItems[]` 按 `name` 匹配 catalog item 时覆盖它；使用 catalog 中不存在的 `name` 则扩展 Test Program，此时必须提供 `testNumber`、`type`（`numeric` 或 `functional`）和 `failSoftBin`，Numeric item 还需 `unit`、`testLimits` 与 `distribution`（`kind`、`mean`、`sigma`），Functional item 需 `passRate` 并默认使用 P/F limits，其 limits 必须包含 pass 值 1；`specLimits` 可选并默认等于 `testLimits`。覆盖结果与自定义项都记录在 Resolved Config 中。覆盖项提供定义字段、非有限或倒置的 limits、越界或重复的 Bin/test number、长度与 `siteCount` 不符的 offsets 都会以退出码 2 拒绝。
- manifest 记录 catalog 版本、良率，以及每个 Dataset 和 Lot 的 Hard/Soft Bin 计数；这些计数与 STDF 文件中的 PRR 记录对账。STDF 的 SDR、PCR、HBR、SBR 和 TSR 按 Tester Site 给出，因此导入后可以直接做 site-to-site 分析。WIR/WCR/WRR 只出现在 CP 文件。

```json
"cp": {
  "siteCount": 8,
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
      "failSoftBin": 100,
      "siteOffsets": [0, 0.002, -0.0015, 0.001, -0.002, 0.0015, -0.001, 0.0005]
    },
    {
      "name": "Vbr",
      "testNumber": 1021,
      "unit": "V",
      "type": "numeric",
      "testLimits": { "low": 26, "high": 34 },
      "failSoftBin": 120,
      "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }
    }
  ]
}
```

STDF 的完整记录集与 multi-site 模型记录在 [ADR-0011](docs/adr/0011-full-stdf-and-multi-site.md)；最小 CP 闭环的历史决策见 [ADR-0008](docs/adr/0008-minimal-stdf-cp-export.md)；第三方 STDF 库的 build-vs-adopt 评估见 [ADR-0012](docs/adr/0012-stdf-library-build-vs-adopt.md)。STDF V4 没有 `WER` 记录，对应的 Wafer 配置记录是 WCR (2,30)。FTR 在标准中不携带 unit 和 limits，因此 Numeric item 用 PTR 携带单位与 limits，Functional item 用 FTR 携带测试名、Tester Site 与 pass/fail 结果。

## FT 与 CP-FT 追溯

内置的完整 FT catalog v1（`ft-catalog-v2`）包含 15 个 Numeric Test Item 和 3 个 Functional Test Item。Numeric item 覆盖静态电流（Icc）、输出与输入电平（Voh、Vol、Vih、Vil）、输出驱动与输入漏电（Ioh、Iol、Iih、Iil）、时序（Tr、Tf、Tpd、Fmax）、导通电阻（Ron）和输入电容（Cin）；Functional item 为 ScanFT、BistFT 和 MbistFT。每个 FT Unit 都要经过全部 item 测试，并得到与 CP 相同的 Bin 处理：第一个失败的 item 决定结果。

- 默认情况下每个 CP 通过的 Die 恰好成为一个 FT Unit。`ft.assemblyLoss` 从 Dataset 中移除相应数量的 CP 通过 Die；`ft.escapeRate` 按该比例接纳 CP 失败的 Die 作为 Escape。因此 FT Unit 数 = CP 通过 Die − Assembly Loss + Escape。若 Dataset 的 CP 通过 Die 少于 `assemblyLoss`，生成失败。
- FT Unit 的稳定 id 为 `<waferId>:<x>,<y>`，与其 CP PART_ID 是同一字符串，因此 CP 与 FT 记录按来源 Die join。每个 Lot 的 Assembly Lot 为 `ALOT-<LOTID>`。
- FT 复用生效的 CP Bin 编号：pass Hard/Soft Bin 以及 parametric/functional 失败 Hard Bin；FT Test Item 携带自己的失败 Soft Bin。Failure Soft Bin 是 step-specific 的，因此同一个编号在 CP 和 FT 可能表示不同的失败。与 FT 失败 Soft Bin 冲突的 `cp.bins.passSoftBin` 会被拒绝。
- `ft.testItems[]` 按 `name` 覆盖 catalog item 的 `specLimits`、`testLimits` 和 `failSoftBin`；使用 catalog 中不存在的 `name` 时扩展 FT Test Program，字段要求与 CP 相同，但 FT 只使用一个 Tester Site，不接受 `siteOffsets`。覆盖与扩展后的 FT 失败 Soft Bin 必须唯一，且不能与 `cp.bins.passSoftBin` 冲突。
- `assemblyLoss` 默认 0，必须大于等于 0；`escapeRate` 默认 0，必须在 0 与 1 之间。未知属性和非整数 `assemblyLoss` 会被拒绝。

```json
"ft": {
  "assemblyLoss": 5,
  "escapeRate": 0.02
}
```

STDF FT 文件（`<LOTID>_FT.stdf`）包含 FAR、每个 FT Unit 一条 ATR、`TEST_COD` 为 `FT` 的 MIR、SDR（1 个 Tester Site）、每个 FT Unit 一组 PIR、PTR 或 FTR、以及 PRR，随后是 TSR/HBR/SBR/PCR 汇总，最后由 MRR 收尾。FT 是封装测试而不是 wafer sort，因此不写 WIR/WCR/WRR；来源 Wafer 追溯由 ATR 承载。ATR 位于 FAR 与 MIR 之间，也就是 STDF V4 期望 audit 记录的位置。PRR 携带来源 Die X/Y，并以 FT Unit id 作为 PART_ID。每条 ATR 的 CMD_LINE 以 `KEY=VALUE` 对形式携带 `UNIT_ID`（与 PRR PART_ID 一致）、`SOURCE_WAFER_ID`、`SOURCE_DIE_X`、`SOURCE_DIE_Y`、`ASSEMBLY_LOT` 和 `CP_RESULT`，因此导入后记录可以 join，Escape 也可识别。manifest 中的 FT Units、Assembly Loss 和 Escapes 与文件对账。参见 [ADR-0011](docs/adr/0011-full-stdf-and-multi-site.md)、[ADR-0010](docs/adr/0010-ft-traceability-and-stdf-export.md) 和 [`examples/ft.json`](examples/ft.json)。

## Latent-factor 模型与 CP-FT 相关性

每个 Numeric Test Item 的值不再独立采样，而是由共享 Latent Factor 生成：lot 级因子、wafer 级因子、每片晶圆一个 Spatial Field、每个 die 的 die 级因子，以及独立的 measurement noise。Test Item 的模型方差按五个分量分解（`lot`、`wafer`、`spatial`、`die`、`measurement`），五个分量之和必须为 1。Normal item 的均值与 sigma 在值空间；Lognormal item 的均值与 sigma 在 log 空间，因此驱动电流与漏电项默认呈现对数正态分布。lot/wafer/spatial 因子在所有 item 之间共享，因此未被显式配对的 item 在这些层级也共同变异；die 层由关联对因子和独立残差组成。

可选的 `latent` 段：

```json
"latent": {
  "variance": { "lot": 0.03, "wafer": 0.05, "spatial": 0.05, "die": 0.72, "measurement": 0.15 },
  "spatialLoadings": [
    { "step": "cp", "name": "Vt", "loading": 1.5 },
    { "step": "ft", "name": "Fmax", "loading": 1.2 }
  ],
  "diePairs": [
    { "cpItem": "Idsat", "ftItem": "Ioh", "targetR": 0.7, "tolerance": 0.05 },
    { "cpItem": "Vt", "ftItem": "Fmax", "targetR": -0.25, "tolerance": 0.05 }
  ]
}
```

- `latent.variance` 的五个分量都是该 Test Item 模型方差的比例，必须有限、非负且和为 1。默认值为 lot 0.03、wafer 0.05、spatial 0.05、die 0.72、measurement 0.15。
- `latent.spatialLoadings[]` 为单个 Numeric Test Item 设置 Spatial Field 载荷；`step` 为 `cp` 或 `ft`，`name` 是该 step 的 Numeric Test Item，`loading` 必须有限且非负，默认 1。载荷放大该 item 的 spatial 方差占比，其余四层按比例缩放，因此 item 的总模型方差保持为 1；同一名称出现在两个 step 时需要分别配置。重复、未知或 Functional item 会被拒绝。
- `latent.diePairs[]` 定义 CP 与 FT 的 die 级关联对。`cpItem` 与 `ftItem` 必须是 Numeric Test Item；`targetR` 是测量值尺度上的目标 Pearson r；`tolerance` 是允许偏差，默认 0.05。
- 目标 r 通过解析校准转化为 die 层因子相关性。Normal/Normal、Lognormal/Lognormal 与混合分布都有闭式解。由于 FT Unit 只包含 CP 通过的 die，生成器最多进行 4 轮确定性校准：用实际 FT 样本的计算结果修正 die 层相关性，直到每个关联对落入容差或达到轮次上限。目标必须能被配置的方差分量实现：共享的 lot/wafer/spatial 层决定相关性的基线，die 层决定可调范围；同一个 item 参与多个关联对时，其 die 层预算不能超过 1。非法配置以退出码 2 拒绝。
- 省略 `diePairs` 时使用内置物理默认对：Idsat/Ioh 0.70、Vt/Fmax −0.25、Cin/Cin 0.80、Leakage/Iil 0.65、Iddq/Icc 0.60。`"diePairs": []` 关闭所有关联对。
- manifest 的 `dataset.correlations.die` 记录每个关联对的 `cpItem`、`ftItem`、`targetR`、`tolerance`、`achievedR`、`withinTolerance` 与 `sampleCount`。`achievedR` 是在所有同时具有 CP 与 FT 结果的 FT Unit 上计算的 Pearson r；样本少于 2 时 `achievedR` 与 `withinTolerance` 为 `null`。`resolvedConfig.latent` 记录生效的方差分量、spatial loadings 与关联对。
- 相同配置与 seed 生成相同的因子值与统计结果。CP 与 FT 使用相同的 lot/wafer/spatial 因子和关联对因子；measurement noise 与 die 残差按 step 独立，因此 CP-FT 相关性只来自共享因子。

参见 [ADR-0013](docs/adr/0013-latent-factor-sampling-and-calibration.md)、[`examples/latent.json`](examples/latent.json) 与 [ADR-0002](docs/adr/0002-latent-factor-generation-model.md)。

## WAT catalog 与 TXT 导出

内置的完整 WAT catalog v1（`wat-catalog-v3`）包含 12 个 Numeric Test Item：Vtn、Vtp、IdsatN、IdsatP、Tox、Rs、Rc、Vbd、IoffN、IoffP、Ssw 和 Vfb。每个 Wafer 在每个 PCM Site 都进行测量：每个 site 上每个 Test Item 一个值，从 catalog 的默认分布采样（IdsatN/IdsatP 为 lognormal，阈值电压为 normal）。每个值由 Dataset seed、Lot、Wafer、Site 和 Test Item 播种，因此相同配置与 seed 可复现文件。

- 默认布局为九个 PCM Site：晶圆中心，加上晶圆中心到边缘中点处的八个点，按 45° 递增。坐标是相对晶圆中心的毫米值。
- `wat.sites` 替换默认布局。每个 site 需要 `id`、`xMm` 和 `yMm`；id 必须唯一，且每个 site 必须位于晶圆内。
- `wat.template` 为 `long`（默认）或 `wide`。
- `wat.organization` 为 `perLot`（默认）或 `perWafer`。
- `wat.testItems[]` 按 `name` 覆盖 catalog item 的 `specLimits`、`testLimits` 和 `distribution`；使用 catalog 中不存在的 `name` 时扩展 WAT Test Program，此时必须提供 `testNumber`、`unit`、`testLimits` 和 `distribution`，`specLimits` 可选并默认等于 `testLimits`。

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

## CP/FT CSV 导出

`generate` 为每个 Wafer 的 CP 和 FT 结果各写一个便于人工查看的 CSV，放在输出目录的 `csv/` 子目录：

- `<waferId>_CP.csv`：一行一个 tested die。
- `<waferId>_FT.csv`：一行一个 FT Unit（含 Escape）；没有 FT Unit 的 Wafer 仍写出文件，只有元数据与列头。

每个文件以 `#` 开头的元数据块开始，记录 `formatVersion`、`step`、`product`、`lotId`、`waferId`、`seed`、`catalogVersion`、`siteCount`、计数与良率，以及每个 Test Item 的编号、类型、单位、spec/test limits 和失败 Soft Bin，还有 Hard/Soft Bin 定义。FT 文件还记录 `assemblyLot`、`cpPassedDie`、`assemblyLoss`、`escapeUnits`、`ftUnits` 和 `passedUnits`，并与 `cpPassedDie - assemblyLoss + escapeUnits = ftUnits` 对账。元数据块之后是一个空行，然后是标准 CSV 宽表数据块。

- CP 数据列：`dieX`、`dieY`、`site`、`hardBin`、`softBin`、`pass`、`firstFail`，随后每个 Test Item 一列。只包含 tested die，行按 Y→X 排序。
- FT 数据列：`unitId`、`dieX`、`dieY`、`cpResult`、`site`、`hardBin`、`softBin`、`pass`、`firstFail`，随后每个 FT Test Item 一列。`cpResult` 是来源 Die 的 CP 结果，`Fail` 表示 Escape；FT 的 `site` 恒为 1。
- Functional Test Item 的值写作 `1`/`0`；`firstFail` 是第一个失败 item 名，通过时为空。
- 数值使用 invariant culture 的 `0.######`；字段按 RFC 4180 转义，`unitId` 内含逗号因此带引号。
- CSV 用于人工检查，不是 YMS 导入格式；STDF 仍是标准导入路径。

CSV 默认随 `generate` 写出，不新增配置项；`--overwrite` 同样保护既有 CSV。格式决策记录在 [ADR-0014](docs/adr/0014-cp-ft-csv-export.md)。

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

A versioned JSON configuration flows through the Core `GenerationPipeline` facade. `silicondraft generate` writes a ground-truth manifest, one Wafer Map CSV, one STDF V4 CP file, and one STDF V4 FT file per lot, one WAT TXT file per lot, and one CP CSV and FT CSV per Wafer under `csv/` by default; `silicondraft preview` prints the effective wafer geometry, Die Map, CP and FT summaries, and WAT layout without writing files.

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

This writes `out/_truth/manifest.json`, `out/LOT-0001_wafermap.csv`, `out/LOT-0001_CP.stdf`, `out/LOT-0001_FT.stdf`, `out/LOT-0001_WAT_long.txt`, `out/csv/LOT-0001-W01_CP.csv`, and `out/csv/LOT-0001-W01_FT.csv`. The manifest records the Resolved Config (including the effective wafer geometry, CP settings, FT settings, and WAT sites), the seed, the Dataset identity, Gross/Partial Die counts and Net Die per wafer, lot, and Dataset, plus CP and FT yield and Bin counts per lot and Dataset. It also records the FT catalog version, the FT Unit and Escape counts per Dataset, lot, and wafer, and the Assembly Loss per Dataset and lot, plus the WAT catalog version, site and test item counts, and measurement counts per Dataset, lot, and wafer. `dataset.correlations.die` records each die-level CP-FT pair's target r, tolerance, achieved r, and sample count. Net Die is the number of valid die that pass CP. The Wafer Map CSV lists every exported die with its Die Index coordinates, physical center, and valid/partial status. The STDF V4 CP file contains FAR, MIR, SDR, WIR/WCR per Wafer, one PIR, PTR or FTR depending on the item type, and PRR group per tested die, WRR per Wafer, the TSR/HBR/SBR/PCR summaries, and MRR. The STDF V4 FT file contains FAR, one ATR per FT Unit, MIR, SDR, one PIR, PTR or FTR, and PRR group per FT Unit, the same lot summaries, and MRR; FT is package test, so it has no WIR/WCR/WRR. The WAT TXT file contains one row per PCM Site and Test Item. The same configuration and seed always produce the same files.

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

The built-in complete CP catalog v1 (`cp-catalog-v4`) has 20 Numeric Test Items and three Functional Test Items. The Numeric items cover threshold voltage (Vt, Vtp, Vtlin, Vtsat, Vfb), drive current and transconductance (Idsat, IdsatP, Gm), leakage (Iddq, Leakage, Ioff), on-resistance and contact resistance (RdsOn, Rs, Rc), gate-oxide behavior (Cox, Vbd, Ssw, Dibl), capacitance (Cin), and forward voltage (Vf); the Functional items are Scan, Bist, and Mbist. Each item declares a unit, a default distribution, spec limits, test limits, and a default failure Soft Bin; Numeric items also declare default measurement offsets for eight Tester Sites. Drive-current items (Idsat, IdsatP) and leakage items default to a lognormal distribution, while threshold voltages and other parameters default to normal. Every valid Die receives one result per item; Partial Die are not tested.

- A Test Item fails when its result falls outside its test limits; spec limits are exported alongside them. The two ranges are not cross-validated, so a configuration can deliberately set wider test limits.
- The first failing item decides the outcome: a Numeric failure maps to the Parametric Fail Hard Bin, a Functional failure to the Functional Fail Hard Bin, and the item's failure Soft Bin identifies what failed. Passing die get the Pass Hard and Soft Bins.
- CP tests on eight Tester Sites in parallel by default. `cp.siteCount` is configurable from 1 to 254; each Die's Tester Site is derived deterministically from the seed and the Die identity, so it is independent of execution order.
- Numeric Test Items support a per-item site offset: `cp.testItems[].siteOffsets` must supply one finite additive offset per Tester Site, in the item's unit. When omitted, the catalog's default offset pattern is expanded cyclically to the configured `siteCount`. Functional items reject site offsets.
- The optional `cp` section overrides limits, Bin numbers, and site settings. `cp.testItems[]` overrides a catalog item when its `name` matches; a `name` the catalog does not define extends the Test Program, and then `testNumber`, `type` (`numeric` or `functional`), and `failSoftBin` are required, Numeric items also need `unit`, `testLimits`, and `distribution` (`kind`, `mean`, `sigma`), and Functional items need `passRate` and default to the P/F limits, whose limits must include the pass value 1; `specLimits` is optional and defaults to `testLimits`. Overrides and custom items are both recorded in the Resolved Config. Definition fields on an override, non-finite or inverted limits, out-of-range or duplicate Bin/test numbers, and offsets whose length differs from `siteCount` are rejected with exit code 2.
- The manifest records the catalog version, yield, and Hard/Soft Bin counts per Dataset and Lot; those counts reconcile with the PRR records in the STDF file. The STDF SDR, PCR, HBR, SBR, and TSR records are reported per Tester Site, so site-to-site analysis works directly after import. WIR/WCR/WRR appear only in the CP file.

```json
"cp": {
  "siteCount": 8,
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
      "failSoftBin": 100,
      "siteOffsets": [0, 0.002, -0.0015, 0.001, -0.002, 0.0015, -0.001, 0.0005]
    },
    {
      "name": "Vbr",
      "testNumber": 1021,
      "unit": "V",
      "type": "numeric",
      "testLimits": { "low": 26, "high": 34 },
      "failSoftBin": 120,
      "distribution": { "kind": "normal", "mean": 30, "sigma": 1.3 }
    }
  ]
}
```

The full STDF record set and multi-site model are recorded in [ADR-0011](docs/adr/0011-full-stdf-and-multi-site.md); the minimal CP loop's historical decision is in [ADR-0008](docs/adr/0008-minimal-stdf-cp-export.md); the third-party STDF library build-vs-adopt evaluation is in [ADR-0012](docs/adr/0012-stdf-library-build-vs-adopt.md). STDF V4 has no `WER` record; the corresponding Wafer configuration record is WCR (2,30). FTR carries no unit or limit fields in the standard, so Numeric items use PTR for units and limits while Functional items use FTR for the test name, Tester Site, and pass/fail result.

## FT and CP-FT traceability

The built-in complete FT catalog v1 (`ft-catalog-v2`) has 15 Numeric Test Items and three Functional Test Items. The Numeric items cover quiescent current (Icc), output and input levels (Voh, Vol, Vih, Vil), output drive and input leakage (Ioh, Iol, Iih, Iil), timing (Tr, Tf, Tpd, Fmax), on-resistance (Ron), and input capacitance (Cin); the Functional items are ScanFT, BistFT, and MbistFT. Every FT Unit is tested against all of them and gets the same Bins treatment as CP: the first failing item decides the outcome.

- By default every CP-passing Die becomes exactly one FT Unit. `ft.assemblyLoss` removes that many CP-passing Die from the Dataset; `ft.escapeRate` admits that fraction of CP-failing Die as Escapes. The FT Unit count is therefore CP-passing Die minus Assembly Loss plus Escapes. A Dataset with fewer CP-passing Die than `assemblyLoss` fails generation.
- An FT Unit's stable id is `<waferId>:<x>,<y>`, the same string as its CP PART_ID, so CP and FT records join on the source Die. Each Lot's Assembly Lot is `ALOT-<LOTID>`.
- FT reuses the effective CP Bin numbering for the pass Hard/Soft Bin and the parametric/functional failure Hard Bins; FT Test Items carry their own failure Soft Bins. Failure Soft Bins are step-specific, so the same number may denote a different failure at CP and FT. A `cp.bins.passSoftBin` that collides with an FT failure Soft Bin is rejected.
- `ft.testItems[]` overrides a catalog item's `specLimits`, `testLimits`, and `failSoftBin` by `name`; a `name` the catalog does not define extends the FT Test Program with the same required fields as CP, except that FT runs on one Tester Site and rejects `siteOffsets`. Failure Soft Bins after overrides and extensions must be unique and must not collide with `cp.bins.passSoftBin`.
- `assemblyLoss` defaults to 0 and must be 0 or greater; `escapeRate` defaults to 0 and must be between 0 and 1. Unknown properties and non-integer `assemblyLoss` are rejected.

```json
"ft": {
  "assemblyLoss": 5,
  "escapeRate": 0.02
}
```

The STDF FT file (`<LOTID>_FT.stdf`) contains FAR, one ATR per FT Unit, MIR with `TEST_COD` `FT`, SDR (one Tester Site), one PIR, PTR or FTR, and PRR group per FT Unit, the TSR/HBR/SBR/PCR summaries, and MRR. FT is package test rather than wafer sort, so it has no WIR/WCR/WRR; source Wafer traceability lives in the ATRs. The ATRs sit between FAR and MIR, where STDF V4 expects audit records. PRR carries the source Die X/Y and the FT Unit id as PART_ID. Each ATR's CMD_LINE carries `UNIT_ID` (matching the PRR PART_ID), `SOURCE_WAFER_ID`, `SOURCE_DIE_X`, `SOURCE_DIE_Y`, `ASSEMBLY_LOT`, and `CP_RESULT` as `KEY=VALUE` pairs, so records join after import and Escapes are identifiable. The manifest's FT Units, Assembly Loss, and Escapes reconcile with the file. See [ADR-0011](docs/adr/0011-full-stdf-and-multi-site.md), [ADR-0010](docs/adr/0010-ft-traceability-and-stdf-export.md), and [`examples/ft.json`](examples/ft.json).

## Latent-factor model and CP-FT correlation

Every Numeric Test Item value is generated from shared Latent Factors instead of being sampled independently: a lot factor, a wafer factor, one Spatial Field per wafer, a die factor per die, and independent measurement noise. Each Test Item's model variance decomposes into five components (`lot`, `wafer`, `spatial`, `die`, `measurement`) that must sum to one. Normal items carry their mean and sigma in value space; Lognormal items carry them in log space, so drive-current and leakage items are lognormal by default. The lot, wafer, and spatial factors are shared across items, so items without an explicit pair still vary together at those layers; the die layer combines pair factors with an independent residual.

The optional `latent` section:

```json
"latent": {
  "variance": { "lot": 0.03, "wafer": 0.05, "spatial": 0.05, "die": 0.72, "measurement": 0.15 },
  "spatialLoadings": [
    { "step": "cp", "name": "Vt", "loading": 1.5 },
    { "step": "ft", "name": "Fmax", "loading": 1.2 }
  ],
  "diePairs": [
    { "cpItem": "Idsat", "ftItem": "Ioh", "targetR": 0.7, "tolerance": 0.05 },
    { "cpItem": "Vt", "ftItem": "Fmax", "targetR": -0.25, "tolerance": 0.05 }
  ]
}
```

- Every `latent.variance` component is a fraction of a Test Item's model variance; each must be finite and non-negative, and the five must sum to one. The defaults are lot 0.03, wafer 0.05, spatial 0.05, die 0.72, and measurement 0.15.
- `latent.spatialLoadings[]` sets one Numeric Test Item's Spatial Field loading. `step` is `cp` or `ft`, `name` is a Numeric Test Item in that step, and `loading` must be finite and non-negative, defaulting to 1. The loading enlarges the item's spatial variance share and rescales the other four layers proportionally, so the item's total model variance stays one; configure the name separately in each step where it appears. Duplicate, unknown, or Functional items are rejected.
- `latent.diePairs[]` defines die-level CP-FT pairs. `cpItem` and `ftItem` must name Numeric Test Items; `targetR` is the target Pearson r on the measurement scale; `tolerance` is the allowed deviation and defaults to 0.05.
- Analytical calibration turns each target r into a die-layer factor correlation. Normal/Normal, Lognormal/Lognormal, and mixed pairs all have closed-form solutions. Because FT Units are the CP-passing subset, generation runs up to four deterministic calibration passes, correcting the die-layer correlations from the achieved FT-sample correlation until every pair lands inside its tolerance or the pass budget runs out. The target must be reachable with the configured variance components: the shared lot/wafer/spatial layers set the correlation floor and the die layer sets the adjustable range; an item that appears in several pairs cannot spend more than its whole die-layer budget. Invalid configurations are rejected with exit code 2.
- When `diePairs` is omitted, the built-in physically motivated defaults apply: Idsat/Ioh 0.70, Vt/Fmax −0.25, Cin/Cin 0.80, Leakage/Iil 0.65, and Iddq/Icc 0.60. `"diePairs": []` disables every pair.
- The manifest's `dataset.correlations.die` records each pair's `cpItem`, `ftItem`, `targetR`, `tolerance`, `achievedR`, `withinTolerance`, and `sampleCount`. `achievedR` is the Pearson r across every FT Unit that carries both a CP and an FT result; it and `withinTolerance` are `null` below two samples. `resolvedConfig.latent` records the effective variance components, spatial loadings, and pairs.
- The same configuration and seed produce the same factor values and statistics. CP and FT reuse the same lot, wafer, and spatial factors and the same pair factors; measurement noise and die residuals are independent per step, so CP-FT correlation comes only from the shared factors.

See [ADR-0013](docs/adr/0013-latent-factor-sampling-and-calibration.md), [`examples/latent.json`](examples/latent.json), and [ADR-0002](docs/adr/0002-latent-factor-generation-model.md).

## WAT catalog and TXT export

The built-in complete WAT catalog v1 (`wat-catalog-v3`) has 12 Numeric Test Items: Vtn, Vtp, IdsatN, IdsatP, Tox, Rs, Rc, Vbd, IoffN, IoffP, Ssw, and Vfb. Every Wafer is measured at every PCM Site: one value per Test Item at each site, sampled from the catalog's default distribution (IdsatN/IdsatP are lognormal and threshold voltages are normal). Each value is seeded from the Dataset seed, Lot, Wafer, Site, and Test Item, so the same configuration and seed reproduce the files.

- The default layout is nine PCM Sites: the wafer center plus eight points halfway between the center and the wafer edge, at 45° increments. Coordinates are millimeters from the wafer center.
- `wat.sites` replaces the default layout. Each site needs an `id`, `xMm`, and `yMm`; ids must be unique and every site must lie within the wafer.
- `wat.template` is `long` (default) or `wide`.
- `wat.organization` is `perLot` (default) or `perWafer`.
- `wat.testItems[]` overrides a catalog item's `specLimits`, `testLimits`, and `distribution` by `name`; a `name` the catalog does not define extends the WAT Test Program, requiring `testNumber`, `unit`, `testLimits`, and `distribution`, with `specLimits` optional and defaulting to `testLimits`.

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

## CP/FT CSV export

`generate` writes one human-readable CSV per Wafer for CP and one for FT under the output directory's `csv/` subdirectory:

- `<waferId>_CP.csv`: one row per tested die.
- `<waferId>_FT.csv`: one row per FT Unit, including Escapes; a Wafer with no FT Units still gets a file with metadata and a header only.

Each file starts with a `#`-prefixed metadata block recording `formatVersion`, `step`, `product`, `lotId`, `waferId`, `seed`, `catalogVersion`, `siteCount`, counts and yield, every Test Item's number, type, unit, spec/test limits, and failure Soft Bin, and the Hard/Soft Bin definitions. FT files also record `assemblyLot`, `cpPassedDie`, `assemblyLoss`, `escapeUnits`, `ftUnits`, and `passedUnits`, reconciling with `cpPassedDie - assemblyLoss + escapeUnits = ftUnits`. A blank line follows the metadata block, then the standard wide CSV data block.

- CP data columns: `dieX`, `dieY`, `site`, `hardBin`, `softBin`, `pass`, `firstFail`, then one column per Test Item. Tested die only, ordered Y→X.
- FT data columns: `unitId`, `dieX`, `dieY`, `cpResult`, `site`, `hardBin`, `softBin`, `pass`, `firstFail`, then one column per FT Test Item. `cpResult` is the source Die's CP outcome, and `Fail` marks an Escape; FT `site` is always 1.
- Functional Test Item values are written as `1`/`0`; `firstFail` names the first failing item and is empty on a pass.
- Values use invariant-culture `0.######`; fields follow RFC 4180 quoting, so `unitId` is quoted because it contains a comma.
- The CSV is for human inspection, not a YMS ingestion format; STDF remains the standard import path.

CSV files are written by `generate` by default; no new configuration option. `--overwrite` protects existing CSVs too. The format decision is recorded in [ADR-0014](docs/adr/0014-cp-ft-csv-export.md).

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
