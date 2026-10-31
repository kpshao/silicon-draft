# CP/FT CSV 导出格式

> 中文版为主版本；英文原文见文末折叠区。

STDF 是二进制格式，导入 YMS 很合适，但不便于人工快速检查生成结果。因此 `generate` 额外为每个 Wafer 的 CP 和 FT 各写一个 CSV，放在输出目录的 `csv/` 子目录：`<waferId>_CP.csv` 一行一个 tested die，`<waferId>_FT.csv` 一行一个 FT Unit。CSV 默认写出，不新增配置项；它只是 Dataset 的可读投影，不是 YMS 的导入格式，标准导入路径仍然是 STDF。

每个文件由元数据块和数据块组成。元数据块以 `#` 开头，记录 `formatVersion`、step、product、lot、wafer、seed、catalog 版本、site 数、计数与良率，并为每个 Test Item 记录编号、类型、单位、spec/test limits 和失败 Soft Bin，同时给出 Hard/Soft Bin 定义；FT 文件还记录 Assembly Lot、`cpPassedDie`、`assemblyLoss`、`escapeUnits`、`ftUnits` 和 `passedUnits`。空行之后是标准 CSV 宽表：列头携带 Test Item 名，元数据块的 item 定义提供列无法携带的单位和 limits。数值使用 invariant culture 的 `0.######`，字段按 RFC 4180 转义；FT 的 `unitId` 形如 `LOT-0001-W01:0,0`，因此带引号。

直接由 `GenerationPipeline.Export` 写 CSV，而不是新增 STDF→CSV 转换命令，是因为 Dataset 中已有 STDF 不携带的 ground truth（第一个失败 item、精确的 bin 分类、Escape 标记），直接投影最简单，也保持 facade 作为唯一 primary seam。按 Wafer 拆分而不是按 Lot，是因为元数据块可以记录单个 WaferID，Excel 打开更快，并与 WAT 的 `perWafer` 先例一致；代价是默认 Dataset 会产生较多文件，`csv/` 子目录把它们与 YMS 导入文件分开。CP 和 FT 分成两个文件而不是一个文件两个数据块，是因为两者的行单位、Test Program 和列集合都不同，分开后每个数据块保持矩形。

替代方案：新增独立的 STDF→CSV 转换命令（需要 STDF reader，且丢失 ground truth）；按 Lot 输出并在数据块中加 `waferId` 列（元数据无法记录单个 WaferID）；把 CP 和 FT 放进同一个文件（列集合不同，宽表被拆碎）；元数据单独存一个文件（“一个 CSV 两部分”的检查体验被破坏）；沿用 WAT wide 的制表符 TXT（用户明确要求 CSV）。

<details>
<summary>English</summary>

# CP/FT CSV export format

STDF is binary: it fits YMS ingestion but is inconvenient for quickly inspecting generated results by hand. `generate` therefore also writes one CSV per Wafer for CP and one for FT under the output directory's `csv/` subdirectory: `<waferId>_CP.csv` has one row per tested die, and `<waferId>_FT.csv` has one row per FT Unit. The CSVs are written by default with no new configuration option; they are a readable projection of the Dataset, not a YMS ingestion format, and STDF remains the standard import path.

Each file has a metadata block and a data block. The metadata block starts with `#` and records `formatVersion`, step, product, lot, wafer, seed, catalog version, site count, counts, and yield; it defines every Test Item's number, type, unit, spec/test limits, and failure Soft Bin, and the Hard/Soft Bin definitions. FT files also record the Assembly Lot, `cpPassedDie`, `assemblyLoss`, `escapeUnits`, `ftUnits`, and `passedUnits`. After a blank line comes the standard wide CSV: the header carries Test Item names, and the metadata block's item definitions supply the units and limits a column cannot. Values use invariant-culture `0.######`, and fields follow RFC 4180 quoting; FT `unitId` looks like `LOT-0001-W01:0,0` and is therefore quoted.

Writing the CSV directly from `GenerationPipeline.Export` rather than adding an STDF-to-CSV command is simplest because the Dataset already holds ground truth that STDF does not (the first failing item, the exact Bin classification, and the Escape flag), and it keeps the facade as the only primary seam. Splitting by Wafer rather than by Lot lets the metadata block record a single WaferID, opens faster in a spreadsheet, and matches the WAT `perWafer` precedent; the cost is more files for a default Dataset, which the `csv/` subdirectory separates from the YMS import files. CP and FT are separate files rather than two data blocks in one file because their row units, Test Programs, and column sets differ; separate files keep each data block rectangular.

Alternatives: a separate STDF-to-CSV command (needs an STDF reader and loses ground truth); per-Lot output with a `waferId` data column (metadata cannot record a single WaferID); CP and FT in one file (the column sets differ and the wide table fragments); metadata in a separate file (breaks the one-CSV, two-part inspection experience); the WAT wide tab-separated TXT format (the user explicitly asked for CSV).

</details>
