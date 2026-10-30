# 完整 STDF V4 保真度与 multi-site

> 中文版为主版本；英文原文见文末折叠区。

T6 必须让 YMS 能按 Tester Site 和完整记录集分析 CP/FT 数据。因此每个 Lot 的 STDF 文件从最小集扩展到 STDF V4 的完整相关记录集，CP 默认在 8 个 Tester Site 上并行测试，并支持 per-item site offset。

## 记录集与顺序

CP 文件的记录顺序是 FAR、MIR、SDR，然后每个 Wafer 一组 WIR、WCR、若干 PIR/测试记录/PRR、WRR，最后是 TSR、HBR、SBR、PCR 汇总和 MRR。FT 文件在 FAR 之后先写每个 FT Unit 的 ATR，再写 MIR 和 SDR，然后写每个 FT Unit 的 PIR/测试记录/PRR，以及同样的汇总和 MRR。FT 是封装测试而不是 wafer sort，因此不写 WIR/WCR/WRR；来源 Wafer 和 Die 的追溯由 ATR 承载。测试记录是 Numeric Test Item 的 PTR 或 Functional Test Item 的 FTR。SDR 在 MIR 之后、任何引用 site 的记录之前声明 site group。

验收标准和父 spec 列出的 `WER` 在 STDF V4 中不存在。Wafer 级配置记录的标准名称是 WCR (2,30)，本实现写出 WCR；不写任何非标准记录，以免标准解析器错位。

## PTR、FTR 与单位/limits

STDF V4 的 FTR 没有 unit、spec limits、test limits 或 PARM_FLG 字段，只有测试编号、Tester Site、pass/fail、结果文本和 vector 字段；其起始顺序是 TEST_NUM、HEAD_NUM、SITE_NUM、TEST_FLG、OPT_FLAG。因此 Numeric Test Item 写 PTR，携带 TEST_TXT、UNITS、LO_LIMIT/HI_LIMIT 与 LO_SPEC/HI_SPEC；Functional Test Item 写 FTR，携带 TEST_TXT、SITE_NUM、TEST_FLG 与 RSLT_TXT。两者都携带 Tester Site。Functional item 的 units 和 limits 仍由 Test Program 在生成时执行，但不进入 FTR，因为标准没有可放它们的字段。PTR 的 OPT_FLAG 置保留位 1；FTR 的 OPT_FLAG 置 0xFF（所有可选 vector 数据无效，保留位 6–7 为 1），随后写 TEST_TXT 和 RSLT_TXT。

## Multi-site 模型

`cp.siteCount` 默认 8，允许 1–254。参数化数据不存在真实的并行调度，因此每个 Die 的 Tester Site 由 Dataset seed、Lot、Wafer 和 Die 坐标经独立 domain tag 哈希得到；它不消耗测试采样流，所以引入 multi-site 不会改变同一 Die 的原始测量值。FT 固定使用 1 个 Tester Site。

`cp.testItems[].siteOffsets` 为每个 Tester Site 提供一个有限的加性偏移，长度必须等于 `siteCount`，且只允许用于 Numeric item。catalog 为每个 Numeric item 声明 8 个默认偏移；当 `siteCount` 不是 8 时按模式循环展开。配置提供 offsets 时完全替换默认模式。Functional item 拒绝 offsets，因为 0/1 结果没有可加的物理量。

SDR 声明 site group；PIR、PTR、FTR 和 PRR 的 SITE_NUM 是该 Die 的 site；PCR、HBR、SBR 和 TSR 按 site 各写一份，因此导入后可直接比较 site 间的良率、bin mix、test fail rate 和参数分布。HBR/SBR 写出每个已定义 bin，包括 0 计数，使各 site 之和与 manifest 对账。PCR 写 PART_CNT、RTST_CNT、ABRT_CNT 和 GOOD_CNT；WRR 额外写 FUNC_CNT（等于 PART_CNT）和 WAFER_ID。

## 结果

manifest schema 升到 6，Resolved Config 记录 `cp.siteCount` 与配置的 `siteOffsets`。CP catalog 升到 `cp-catalog-v2`，因为默认 site offsets 属于 item 定义。golden 文件全部重新生成；CP 覆盖含 WIR/WCR/WRR 的完整记录集，FT 覆盖除 wafer 记录外的完整记录集。相同配置与 seed 仍产生逐字节相同的文件。

替代方案：写非标准 `WER` 记录（标准解析器会错位）；把 unit/limits 塞进 FTR 的 PROG_TXT（标准解析器不会当作 limits）；保留 Functional item 的 PTR 并额外写 FTR（同一测试被记录两次）；在 FT 中写 WIR/WRR（会让 YMS 把封装测试误当作 wafer sort 数据，且来源 Wafer 信息已在 ATR 中）；用单个标量 offset 乘以 site 号（无法表达任意的 per-site 模式）；从测试采样流中分配 site（改变已有测量值）；只写 255 汇总 bin（丢失 site 维度）。

<details>
<summary>English</summary>

# Full STDF V4 fidelity and multi-site

T6 must let a YMS analyze CP/FT data by Tester Site and by the complete record set. Each Lot's STDF file therefore grows from the minimal set to STDF V4's complete relevant record set, CP tests on eight Tester Sites by default, and per-item site offsets are supported.

## Record set and order

A CP file orders FAR, MIR, SDR, then one WIR, WCR, PIR/test-record/PRR groups, and WRR per Wafer, then the TSR, HBR, SBR, and PCR summaries, and MRR. An FT file writes one ATR per FT Unit after FAR, then MIR and SDR, then each FT Unit's PIR/test-record/PRR group, followed by the same summaries and MRR. FT is package test rather than wafer sort, so it has no WIR/WCR/WRR; source Wafer and Die traceability lives in the ATRs. Test records are PTRs for Numeric Test Items and FTRs for Functional Test Items. SDR follows MIR and precedes any record that references a site.

The `WER` named by the acceptance criteria and parent spec does not exist in STDF V4. The standard Wafer configuration record is WCR (2,30), and this implementation writes WCR; it writes no non-standard record that would desynchronize standard parsers.

## PTR, FTR, units, and limits

STDF V4's FTR has no unit, spec-limit, test-limit, or PARM_FLG fields; it carries a test number, Tester Site, pass/fail, result text, and vector fields, and begins TEST_NUM, HEAD_NUM, SITE_NUM, TEST_FLG, OPT_FLAG. Numeric Test Items therefore write PTRs carrying TEST_TXT, UNITS, LO_LIMIT/HI_LIMIT, and LO_SPEC/HI_SPEC; Functional Test Items write FTRs carrying TEST_TXT, SITE_NUM, TEST_FLG, and RSLT_TXT. Both carry the Tester Site. Functional units and limits are still enforced by the Test Program at generation time but do not enter the FTR because the standard has no field for them. PTRs set OPT_FLAG's reserved bit 1; FTRs set OPT_FLAG to 0xFF (all optional vector data invalid, reserved bits 6-7 set) and then write TEST_TXT and RSLT_TXT.

## Multi-site model

`cp.siteCount` defaults to 8 and allows 1-254. Parametric data has no real parallel schedule, so every Die's Tester Site is hashed from the Dataset seed, Lot, Wafer, and Die coordinates with an independent domain tag. It consumes no sampling stream, so introducing multi-site does not change a Die's raw measurements. FT uses one Tester Site.

`cp.testItems[].siteOffsets` supplies one finite additive offset per Tester Site, must have length `siteCount`, and is allowed only on Numeric items. The catalog declares eight default offsets per Numeric item; when `siteCount` is not eight, the pattern repeats cyclically. Configured offsets replace the default pattern entirely. Functional items reject offsets because a 0/1 result has no physical quantity to shift.

SDR declares the site group; PIR, PTR, FTR, and PRR SITE_NUM identify the Die's site; PCR, HBR, SBR, and TSR are written per site, so imported data compares yield, bin mix, test fail rate, and parameter distributions across sites directly. HBR/SBR write every defined bin, including zero counts, so per-site sums reconcile with the manifest. PCR writes PART_CNT, RTST_CNT, ABRT_CNT, and GOOD_CNT; WRR additionally writes FUNC_CNT (equal to PART_CNT) and WAFER_ID.

## Consequences

The manifest schema rises to 6, and the Resolved Config records `cp.siteCount` and any configured `siteOffsets`. The CP catalog rises to `cp-catalog-v2` because default site offsets are part of an item's definition. All golden files are regenerated; CP covers the complete record set including WIR/WCR/WRR, and FT covers the complete set except the wafer records. The same configuration and seed still produce byte-identical files.

Alternatives: write a non-standard `WER` record (standard parsers desynchronize); put unit/limits in FTR's PROG_TXT (standard parsers will not treat them as limits); keep Functional items as PTRs and additionally write FTRs (the same test is recorded twice); write WIR/WRR in FT (makes a YMS misread package test as wafer sort, and source Wafer information already lives in the ATRs); use a single scalar offset times the site number (cannot express arbitrary per-site patterns); draw site assignment from the test sampling stream (changes existing measurements); write only 255 summary bins (loses the site dimension).

</details>
