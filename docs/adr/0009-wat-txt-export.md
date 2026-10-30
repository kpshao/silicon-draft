# WAT TXT 导出格式

> 中文版为主版本；英文原文见文末折叠区。

WAT 测量在 PCM Site 上采样，而不是按 Die 采样，因此不共享 CP 的 STDF 路径。每个 Dataset 写出制表符分隔的 TXT 文件，命名为 `<stem>_WAT_<template>.txt`，其中 stem 默认是 Lot id，当 `wat.organization` 为 `perWafer` 时是 Wafer id。两种模板都以 `# catalogVersion=<version>` 开头，因此导出文件会指明产生其字段的 catalog。

Long 格式每个 Site 与 Test Item 重复一行，除 `lotId` 和 `waferId` 外还携带 `site`、`siteXmm`、`siteYmm`、`testItem`、`testNumber`、`value`、`unit`、`lowSpec`、`highSpec`、`lowTest`、`highTest`。Wide 格式每个 Wafer 的每个 Site 一行，把每个 Test Item 的值放入独立列，并在开头的 `#` 定义块中记录每个 item 的编号、单位、spec limits 和 test limits，因为列本身无法携带单位或 limits。

默认 PCM Site 布局为晶圆中心，加上晶圆中心与边缘中点处按 45° 递增的八个点；坐标是相对晶圆中心的毫米值。布局可配置，因为不同 fab 采样不同的 scribe-line 结构。每个值由 Dataset seed、Lot、Wafer、Site 和 Test Item 播种，从而使输出与生成顺序无关（ADR-0004）。

manifest 记录 WAT catalog 版本、site 和 item 计数，以及每个 Dataset、lot 和 wafer 的测量计数，因此导出字段可以与 Dataset 记录的 catalog 版本对账。

替代方案：单一 STDF WAT 文件（YMS 的 WAT 导入器消费 TXT，而非 STDF）；按 Die 生成 WAT 行（WAT 在 PCM Site 采样，而非按 Die）；只在 long 模板中嵌入单位和 limits（使 wide 导入无法解释数值）。

<details>
<summary>English</summary>

# WAT TXT export format

WAT measurements are sampled at PCM Sites rather than per Die, so they do not share the CP STDF path. Each Dataset writes tab-separated TXT files named `<stem>_WAT_<template>.txt`, where the stem is the Lot id by default or the Wafer id when `wat.organization` is `perWafer`. Both templates begin with `# catalogVersion=<version>` so an exported file names the catalog that produced its fields.

Long format repeats one row per Site and Test Item and carries `site`, `siteXmm`, `siteYmm`, `testItem`, `testNumber`, `value`, `unit`, `lowSpec`, `highSpec`, `lowTest`, `highTest` alongside `lotId` and `waferId`. Wide format writes one row per Site per Wafer, puts each Test Item value in its own column, and records each item's number, unit, spec limits, and test limits in a leading `#` definition block, because a column cannot carry units or limits.

The default PCM Site layout is the wafer center plus eight points halfway between the center and the wafer edge at 45° increments; coordinates are millimeters from the wafer center. The layout is configurable because different fabs sample different scribe-line structures. Every value is seeded from the Dataset seed, Lot, Wafer, Site, and Test Item, which keeps output independent of generation order (ADR-0004).

The manifest records the WAT catalog version, site and item counts, and measurement counts per Dataset, lot, and wafer, so exported fields can be reconciled with the catalog version recorded for the Dataset.

Alternatives: one STDF WAT file (YMS WAT importers consume TXT, not STDF); per-Die WAT rows (WAT is sampled at PCM Sites, not per Die); embedding units and limits only in the long template (leaves wide imports unable to interpret values).

</details>
