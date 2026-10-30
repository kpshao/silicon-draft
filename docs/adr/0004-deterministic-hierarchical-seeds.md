# 确定性分层 seed 与 simulation time

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 必须从相同的配置和 seed 逐字节复现一个 Dataset，即使生成过程跨 lot 和 wafer 并行。我们从 Dataset seed 为每个 lot、wafer 和 die 派生独立的子 seed，而不是共享一个全局随机流；每个时间戳都来自配置的 simulation time，而不是系统时钟。因此调度既不影响内容也不影响顺序，Ground-Truth Manifest 也不包含 wall-clock 时间。

考虑过的替代方案：单一全局 RNG（简单但依赖调度）以及在输出中记录 wall-clock 时间戳（破坏可复现性）。

<details>
<summary>English</summary>

# Deterministic hierarchical seeds and simulation time

SiliconDraft must reproduce a Dataset byte-for-byte from the same configuration and seed, even when generation is parallelized across lots and wafers. We derive independent child seeds for each lot, wafer, and die from the Dataset seed instead of sharing one global random stream, and every timestamp comes from the configured simulation time rather than the system clock. Scheduling therefore changes neither content nor ordering, and the ground-truth manifest contains no wall-clock time.

Considered alternatives: one global RNG (simple but scheduling-dependent) and recording wall-clock timestamps in outputs (breaks reproducibility).

</details>
