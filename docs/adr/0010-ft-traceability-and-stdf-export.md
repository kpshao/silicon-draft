# FT Unit 选择与 CP-FT 追溯导出

> 中文版为主版本；英文原文见文末折叠区。

每个 FT Unit 必须恰好映射到一个 CP 通过的 Die，以便 YMS 计算 Die-Level Correlation，而且这个映射必须在导出后仍然存在。因此每个 Lot 从其 CP 结果生成 FT Unit：CP 通过的 Die 在 Dataset 级 Assembly Loss 之后 1:1 成为 FT Unit，CP 失败的 Die 可以按配置的 Escape 率进入 FT。manifest 记录最终计数，`<LOTID>_FT.stdf` 携带每个 unit 的追溯信息。

Assembly Loss 是整个 Dataset 上 CP 通过 Die 的计数（`ft.assemblyLoss`），而不是比率，因此配置值与实际值相同，验收恒等式 `FT Units = CP-passing Die − Assembly Loss + Escape` 即使 Dataset 有多个 Lot 也无需舍入即可成立。在整个 Dataset 上应用它，可以保持配置计数等于 FT Unit 的减少量；manifest 记录每个 Lot 实际承受的损失。若某配置损失的 die 多于 Dataset 通过的数量，则在生成时以运行时错误拒绝，因为 CP 通过计数只有在生成之后才可知。Escape 是逐个 CP 失败 Die 独立施加的概率（`ft.escapeRate`）。

选择是确定性的，且与调度无关（ADR-0004）。Assembly Loss 使用由 Dataset seed 和固定 domain tag 播种的 SplitMix64 流打乱 Dataset 的 CP 通过 Die，然后丢弃前 N 个；Escape 使用由 Dataset seed、Lot、Wafer 和 Die 坐标播种的流对每个 CP 失败 Die 进行判定。FT 结果使用相同的按 Die 播种。因此重新生成相同配置和 seed 会产生相同的 Unit、ID 和值。

FT Unit 的稳定标识符是 `<waferId>:<x>,<y>`，与来源 Die 的 CP PRR PART_ID 是同一字符串。它由身份而非序列位置派生，因此能承受 Dataset 其他部分的变化，并直接 join 到 CP 记录。每个 Lot 的 assembly lot 是 `ALOT-<LOTID>`，每个制造 Lot 一个。

FT 与 CP 步骤共享 Bin 编号：pass Hard/Soft Bin 以及 parametric/functional 失败 Hard Bin，因此跨步骤良率分析看到同一个 pass Bin 和同一套失败分类。FT Test Item 携带自己的失败 Soft Bin，且不得与生效的 pass Soft Bin 冲突；配置解析器会拒绝这种冲突。Failure Soft Bin 保持 step-specific：CP item 和 FT item 可以共享同一编号，因为该编号在其步骤的 Test Program 内解释，因此不要求跨步骤互斥。

STDF FT 使用与 CP 相同的最小记录集：FAR、每个 Unit 一条 ATR、MIR（`TEST_COD` 为 `FT`）、每个 Unit 一组 PIR/PTR/PRR，以及 MRR。ATR 紧接在 FAR 之后、MIR 之前出现，也就是 STDF V4 放置 audit 记录的位置。PRR 携带来源 Die X/Y，并以 Unit id 作为 PART_ID。每条 ATR 的 CMD_LINE 以分号分隔的 `KEY=VALUE` 对编码 `UNIT_ID`、`SOURCE_WAFER_ID`、`SOURCE_DIE_X`、`SOURCE_DIE_Y`、`ASSEMBLY_LOT` 和 `CP_RESULT`。`UNIT_ID` 与 PRR PART_ID 一致，因此 audit 记录与 part 记录可以 join；Escape 的 `CP_RESULT` 为 `Fail`，从而可将 manifest 的 Escape 计数与文件对账。T6 可以加入 WIR/WRR 和完整记录集，而无需改变此编码。

替代方案：Assembly Loss 用比率（舍入使配置值与实际值不一致）；Assembly Loss 按 Lot 计（配置计数会随 Lot 数量缩放并破坏 Dataset 级恒等式）；基于序列的 Unit id（新增夹具或 Lot 时不稳定）；只在 PRR PART_TXT 中追溯（自由文本而非具名字段）；文件级单条 ATR（无法携带每个 Unit 的来源坐标）；单独的 sidecar 文件（无法在 YMS 导入后存活）。

<details>
<summary>English</summary>

# FT Unit selection and CP-FT traceability export

Every FT Unit must map to exactly one CP-passing Die so a YMS can compute Die-Level Correlation, and the mapping must survive export. Each Lot therefore generates FT Units from its CP results: the CP-passing Die become FT Units 1:1 after a Dataset-level Assembly Loss, and CP-failing Die may enter FT at a configured Escape rate. The manifest records the resulting counts, and `<LOTID>_FT.stdf` carries the per-unit traceability.

Assembly Loss is a count of CP-passing Die for the whole Dataset (`ft.assemblyLoss`), not a rate, so the configured and achieved loss are the same number and the acceptance identity `FT Units = CP-passing Die − Assembly Loss + Escape` holds without rounding even when the Dataset has several Lots. Applying it Dataset-wide keeps the configured count equal to the reduction in FT Units; the manifest records the loss each Lot actually suffered. A configuration that loses more die than the Dataset passes is rejected at generation time with a runtime error, because the CP-passing count is only known after generation. Escape is a probability (`ft.escapeRate`) applied independently to each CP-failing Die.

Selection is deterministic and scheduling-independent (ADR-0004). Assembly Loss shuffles the Dataset's CP-passing Die with a SplitMix64 stream seeded from the Dataset seed and a fixed domain tag, then drops the first N; Escape tests each CP-failing Die with a stream seeded from the Dataset seed, Lot, Wafer, and Die coordinates. FT results use the same per-Die seeding. Regenerating the same configuration and seed therefore produces the same Units, IDs, and values.

An FT Unit's stable identifier is `<waferId>:<x>,<y>`, the same string as the CP PRR PART_ID for its source Die. It is derived from identity rather than sequence position, so it survives changes elsewhere in the Dataset and joins directly to CP records. Each Lot's assembly lot is `ALOT-<LOTID>`, one per manufacturing Lot.

FT shares the CP step's Bin numbering for the pass Hard/Soft Bin and the parametric/functional failure Hard Bins, so cross-step yield analysis sees one pass Bin and one failure taxonomy. FT Test Items carry their own failure Soft Bins, which must not collide with the effective pass Soft Bin; the configuration resolver rejects such a collision. Failure Soft Bins remain step-specific: a CP item and an FT item may share a number because the number is interpreted within its step's Test Program, so cross-step disjointness is not required.

STDF FT uses the same minimal record set as CP: FAR, one ATR per Unit, MIR (TEST_COD `FT`), one PIR/PTR/PRR group per Unit, and MRR. The ATRs appear immediately after FAR and before MIR, where STDF V4 places audit records. PRR carries the source Die X/Y and the Unit id as PART_ID. Each ATR's CMD_LINE encodes `UNIT_ID`, `SOURCE_WAFER_ID`, `SOURCE_DIE_X`, `SOURCE_DIE_Y`, `ASSEMBLY_LOT`, and `CP_RESULT` as semicolon-separated `KEY=VALUE` pairs. `UNIT_ID` matches the PRR PART_ID, so the audit record and the part record join; `CP_RESULT` is `Fail` for an Escape, which lets the manifest's Escape count be reconciled against the file. T6 may add WIR/WRR and the full record set without changing this encoding.

Alternatives: Assembly Loss as a rate (rounding makes the configured and achieved counts differ); Assembly Loss per Lot (the configured count would scale with the Lot count and break the Dataset-level identity); sequence-based Unit ids (unstable when a fixture or Lot is added); traceability only in PRR PART_TXT (free text instead of named fields); one file-level ATR (cannot carry per-Unit source coordinates); a separate sidecar file (does not survive YMS import).

</details>
