# 最小 STDF V4 CP 导出

> 中文版为主版本；英文原文见文末折叠区。

第一个 CP 闭环必须产生一个标准格式的文件，使良率管理系统在完整 STDF 功能集落地之前就能摄取。因此每个 Lot 导出一个 `<LOTID>_CP.stdf`，包含 FAR、MIR、每个受测 Die 一组 PIR/PTR/PRR，以及 MRR。文件为小端序，FAR 声明版本 4，每个 Test Item 是一条 PTR，携带 TEST_TXT、UNITS、test limits（LO_LIMIT/HI_LIMIT）和 spec limits（LO_SPEC/HI_SPEC），PRR 携带 NUM_TEST、HARD_BIN、SOFT_BIN、X_COORD、Y_COORD 以及 `wafer:die` 形式的 PART_ID，因此即使没有 WIR，die 身份也能保留。

Functional Test Item 以 PTR 导出，结果为 1/0，单位为 P/F，limits 为 0.5–1.5，因为验收标准要求每个 item 都携带单位和 limits，而 FTR 两者都没有。FTR、PCR、HBR、SBR、WIR、multi-site 记录以及其余可选字段刻意推迟到 T6，而不是在此近似实现。

时间戳来自固定的 simulation epoch，而不是系统时钟，因此相同配置和 seed 产生逐字节相同的文件，golden 文件在 Simulation Time 可配置之前保持稳定。

对于给定的运行时和平台，输出保证逐字节相同。采样分布使用超越函数，其最后一位可能因平台而异，因此 golden STDF 文件是同平台快照，而不是跨平台字节契约。

替代方案：Functional item 使用 FTR（记录中没有单位或 limits，不满足验收标准）；导出不带 limits 的 functional item；或在 T3 写入完整的 STDF 记录集（与 T6 重复，并推迟第一个可用的 CP 闭环）。

<details>
<summary>English</summary>

# Minimal STDF V4 CP export

The first CP loop must produce a standard-format file that a yield management system can ingest before the full STDF feature set lands. Each Lot therefore exports one `<LOTID>_CP.stdf` containing FAR, MIR, one PIR/PTR/PRR group per tested Die, and MRR. The file is little-endian, FAR declares version 4, every Test Item is a PTR carrying TEST_TXT, UNITS, test limits (LO_LIMIT/HI_LIMIT), and spec limits (LO_SPEC/HI_SPEC), and PRR carries NUM_TEST, HARD_BIN, SOFT_BIN, X_COORD, Y_COORD, and a `wafer:die` PART_ID so die identity survives without WIR.

Functional Test Items are exported as PTRs with a 1/0 result, P/F units, and 0.5–1.5 limits because the acceptance criteria require every item to carry units and limits, while FTR carries neither. FTR, PCR, HBR, SBR, WIR, multi-site records, and the remaining optional fields are deliberately deferred to T6 rather than approximated here.

Timestamps come from a fixed simulation epoch rather than the system clock, so the same configuration and seed produce byte-identical files and the golden file stays stable until Simulation Time is configured.

Byte-identical output is guaranteed for a given runtime and platform. The sampling distributions use transcendental functions whose last bit may differ across platforms, so the golden STDF file is a same-platform snapshot rather than a cross-platform byte contract.

Alternatives: use FTR for functional items (no units or limits in the record, failing the acceptance criteria), export functional items without limits, or write the complete STDF record set in T3 (duplicates T6 and delays the first usable CP loop).

</details>
