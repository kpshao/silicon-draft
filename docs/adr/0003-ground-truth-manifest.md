# 将 Ground-Truth Manifest 作为一等输出

> 中文版为主版本；英文原文见文末折叠区。

只有当良率管理系统的分析可以对照刻意注入的内容进行核对时，SiliconDraft 数据集才能充当基准。因此每次运行都会写出 manifest，记录配置、seed、生效的晶圆几何、目标与实际良率和 bin mix、配置的相关性对及其预期系数、注入的 Defect Signature，以及预期统计结果；验证会将实际结果与预期比较，超出配置容差即失败。

替代方案：把配置文件当作唯一记录（丢失实际统计与容差），或把 ground truth 打到 stdout（不可审计、不可复现）。

<details>
<summary>English</summary>

# Ground-truth manifest as a first-class output

A SiliconDraft dataset only serves as a benchmark if a yield management system's analyses can be checked against what was deliberately injected. Every run therefore writes a manifest recording the configuration, seed, effective wafer geometry, target and achieved yield and bin mix, configured correlation pairs with expected coefficients, injected defect signatures, and expected statistical outcomes; verification compares achieved against expected and fails outside configured tolerances.

Alternatives: treating the configuration file as the only record (loses achieved statistics and tolerances) or logging ground truth to stdout (not auditable or reproducible).

</details>
