# STDF 库选型：保留自研 writer

> 中文版为主版本；英文原文见文末折叠区。

## 背景

T6 交付了一个无运行时依赖的 STDF V4 writer（`StdfFormat`、`StdfCpWriter`、`StdfFtWriter`、`StdfSummaryWriter`）。2026-10-30 对现有 STDF 读写库做了一次调查，以判断是否存在可以替代它的成熟实现。

## 决策

1. 生产导出路径继续使用自研 writer。
2. 不引入第三方运行时 NuGet 依赖，也不让 Python/Rust/C 子进程或 FFI 进入生产导出路径。
3. 测试期可选交叉校验使用 Semi-ATE-STDF 0.1.33（MIT），入口为 `tools/verify_stdf.py`；它不进入 `dotnet test` 或 CI。
4. 不 fork、不 vendor 任何第三方 STDF 库；能力缺口优先向上游贡献。

## 门槛

写库（生产路径）必须同时满足：

- 可写 STDF V4，覆盖 FAR/MIR/SDR/WIR/WCR/WRR/PIR/PTR/FTR/PRR/TSR/HBR/SBR/PCR/MRR；FT 不含 WIR/WCR/WRR。
- 能表达 CP multi-site 与 per-site 汇总，或允许调用方自行控制记录顺序与汇总记录。
- 字节确定性，记录顺序可控。
- permissive license（生产依赖只接受 MIT/Apache-2.0/BSD）。
- .NET 10 原生；不接受子进程或 FFI。

读库（测试期）门槛较低：可安装、能独立解析 V4、license 允许测试期使用。LGPL/GPL 只允许作为测试期外部工具，不允许链接或随产品分发；AGPL 一律不使用。

## 候选（调查日期 2026-10-30）

| 库 | 语言 / License | 读写 | 结论 |
|---|---|---|---|
| [Semi-ATE-STDF](https://github.com/Semi-ATE/STDF) | Python / MIT | 读+写 | 记录覆盖完整；GitHub 0.1.33（2024-12），PyPI 仍 0.1.28（2022）；调用方控制顺序，不生成汇总。选为测试期交叉校验 |
| [rust-stdf](https://github.com/noonchen/rust-stdf) | Rust / MIT | 读+写 | 1.1.0（crates.io 2026-08），活跃；需 Rust 工具链。次选 |
| [LinqToStdf](https://github.com/marklio/LinqToStdf) | C# / MS-PL | 读+写 | 唯一内置 summary 合成与 V4 排序的 C# 候选；.NET Framework 4.0、无 NuGet、2023 后无更新。移植成本高，淘汰 |
| stdflib | C# / MIT | 读+写 | .NET Core 3.1、2020 后无更新、无 NuGet。淘汰 |
| TDA | C# / MIT | 读+写 | 2.1.5（2026-10），`net10.0-windows`、Windows-only、新仓库。淘汰 |
| STDFParser4Net | C# / MIT | 只读 | netstandard2.1 解析器。不能用于写 |
| Stdf4Net | C# / MIT | 声称读写 | 2022 年包，无仓库/写 API 证据。不可信 |
| pystdf4 | Python / MIT | 写优先 | 解析器未完成，FTR/MPR 不完整。淘汰 |
| pystdf | Python / GPL-2.0 | 只读 | 许可证限制且只读。淘汰 |
| FreeSTDF / stdf4j | C / Java | 读+写 / 只读 | 2006 年 / 无 Maven 发布。过时 |
| Nornion NEDA / nSTDF | C# | 读+写 | 闭源、报价制。淘汰 |

## 理由

没有任何候选同时满足写库门槛。成熟的 STDF V4 读写库都在 Python/Rust/C 生态；C# 侧只有停更的 .NET Framework 项目、Windows-only 的新项目，或无法验证写 API 的包。通用库也不会替我们生成 CP 的 per-site PCR/HBR/SBR/TSR，或保证我们需要的记录顺序；自研 writer 约 600 行，已被 golden 文件与第三方解析器验证，替换收益不足以抵消依赖、许可、打包与确定性风险。

## 交叉校验

- 工具：Semi-ATE-STDF 0.1.33，固定在 `tools/requirements-stdf.txt`。PyPI 只有 0.1.28；若 GitHub 不可达，可用 0.1.28 作为降级交叉校验（本次验证使用 0.1.28）。
- 脚本：`tools/verify_stdf.py` 解析给定文件，检查记录集、CP/FT 的 wafer 记录差异、PTR/FTR 的字段与 Tester Site、FAR/MRR 锚点；不做 golden 比较。
- 交叉校验已经暴露真实问题：FAR 的 `CPU_TYPE` 必须是 2（IBM PC/AT 小端），0（DEC/VAX）会被解析器视为未知字节序；FTR 没有 `PARM_FLG`，`XFAIL_AD`/`YFAIL_AD` 是 I4，`FAIL_PIN`/`SPIN_MAP` 的 D*n 前导是 2 字节 bit 数。这些问题已修复并由 Semi-ATE 完整解析验证。

## 重新评估触发条件

1. T17（规模与性能、流式导出）落地时。
2. 出现满足写库门槛的 .NET 原生 STDF V4 库时。
3. YMS 提供字段级模板、需要改动记录映射时。

<details>
<summary>English</summary>

# STDF library selection: keep the in-house writer

## Context

T6 delivered a dependency-free STDF V4 writer (`StdfFormat`, `StdfCpWriter`, `StdfFtWriter`, `StdfSummaryWriter`). On 2026-10-30 we surveyed existing STDF libraries to decide whether a mature implementation could replace it.

## Decision

1. The production export path keeps the in-house writer.
2. Do not add a third-party runtime NuGet dependency, and do not put a Python/Rust/C subprocess or FFI on the production export path.
3. Use Semi-ATE-STDF 0.1.33 (MIT) for optional test-time cross-checking through `tools/verify_stdf.py`; it is not part of `dotnet test` or CI.
4. Do not fork or vendor a third-party STDF library; contribute capability gaps upstream instead.

## Gates

A writer (production path) must satisfy all of:

- Write STDF V4 covering FAR/MIR/SDR/WIR/WCR/WRR/PIR/PTR/FTR/PRR/TSR/HBR/SBR/PCR/MRR; FT excludes WIR/WCR/WRR.
- Express CP multi-site and per-site summaries, or let the caller control record order and summary records.
- Deterministic bytes with controllable record order.
- Permissive license; production dependencies accept only MIT/Apache-2.0/BSD.
- Native .NET 10; no subprocess or FFI.

A reader (test-time) has a lower bar: installable, independently parses V4, and licensed for test-time use. LGPL/GPL are allowed only as test-time external tools, never linked or shipped; AGPL is never used.

## Candidates (survey date 2026-10-30)

| Library | Language / License | Read/write | Verdict |
|---|---|---|---|
| [Semi-ATE-STDF](https://github.com/Semi-ATE/STDF) | Python / MIT | read+write | Complete record coverage; GitHub 0.1.33 (2024-12), PyPI still 0.1.28 (2022); caller controls order and no summary synthesis. Chosen for test-time cross-checking |
| [rust-stdf](https://github.com/noonchen/rust-stdf) | Rust / MIT | read+write | 1.1.0 (crates.io 2026-08), active; needs the Rust toolchain. Runner-up |
| [LinqToStdf](https://github.com/marklio/LinqToStdf) | C# / MS-PL | read+write | The only C# candidate with built-in summary synthesis and V4 ordering; .NET Framework 4.0, no NuGet, untouched since 2023. Porting cost; rejected |
| stdflib | C# / MIT | read+write | .NET Core 3.1, untouched since 2020, no NuGet. Rejected |
| TDA | C# / MIT | read+write | 2.1.5 (2026-10), `net10.0-windows`, Windows-only, new repository. Rejected |
| STDFParser4Net | C# / MIT | read-only | netstandard2.1 parser. Cannot write |
| Stdf4Net | C# / MIT | claims read+write | 2022 package, no repository and no verifiable write API. Not credible |
| pystdf4 | Python / MIT | writer-first | Parser unfinished, FTR/MPR incomplete. Rejected |
| pystdf | Python / GPL-2.0 | read-only | License restriction and read-only. Rejected |
| FreeSTDF / stdf4j | C / Java | read+write / read-only | 2006 / no Maven release. Stale |
| Nornion NEDA / nSTDF | C# | read+write | Closed source, quote-based. Rejected |

## Rationale

No candidate satisfies the writer gates. Mature STDF V4 read/write libraries live in the Python/Rust/C ecosystems; the C# options are an unmaintained .NET Framework project, a Windows-only new project, or a package with no verifiable write API. A general library also would not synthesize CP's per-site PCR/HBR/SBR/TSR or guarantee the record order we need. The in-house writer is about 600 lines, already verified by golden files and a third-party parser, and replacing it would not buy enough to offset dependency, license, packaging, and determinism risk.

## Cross-check

- Tool: Semi-ATE-STDF 0.1.33, pinned in `tools/requirements-stdf.txt`. PyPI only has 0.1.28; if GitHub is unreachable, 0.1.28 is an acceptable fallback (this validation used 0.1.28).
- Script: `tools/verify_stdf.py` parses the given files and checks the record set, the CP/FT wafer-record difference, PTR/FTR fields and Tester Site, and the FAR/MRR anchors; it does not compare golden files.
- The cross-check already found real defects: FAR `CPU_TYPE` must be 2 (IBM PC/AT little-endian), because 0 (DEC/VAX) is treated as unknown byte order by parsers; FTR has no `PARM_FLG`, `XFAIL_AD`/`YFAIL_AD` are I4, and the D*n fields `FAIL_PIN`/`SPIN_MAP` carry a two-byte bit count. These are fixed and verified by a complete Semi-ATE parse.

## Revisit triggers

1. When T17 (scale and performance, streaming export) lands.
2. When a .NET-native STDF V4 writer that meets the gates appears.
3. When a YMS provides field-level templates that require changing the record mapping.

</details>
