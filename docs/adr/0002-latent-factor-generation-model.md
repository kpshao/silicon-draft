# 用于跨步骤相关性的共享 latent-factor 模型

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 必须生成 CP、FT 和 WAT 数据，其相关性在 die、wafer 和 lot 层级上已知且可控，其空间图形是刻意注入的而非涌现的。我们从共享 latent factor（lot、wafer、die，外加一个晶圆内 spatial field）生成每个 test item，并为每个 item 配置载荷；将方差分解为 lot、wafer、spatial、die 和 measurement 层，并在容差内把配置的相关性对校准到目标系数。Test Item 默认采用有物理依据的分布与关系，叠加式 Defect Signature 作用于同一个 spatial field，可以针对 test item 或 bin 失败概率。

考虑过的替代方案：各步骤独立随机生成后再拟合相关性（破坏空间结构，且无法保证联合分布），以及对真实数据集做扰动（可控性有限，且受许可与隐私约束）。

<details>
<summary>English</summary>

# Shared latent-factor model for cross-step correlation

SiliconDraft must produce CP, FT, and WAT data whose correlations are known and controllable at die, wafer, and lot levels, and whose spatial signatures are deliberate rather than emergent. We generate every test item from shared latent factors (lot, wafer, die, plus a within-wafer spatial field) with per-item loadings, decompose variance into lot, wafer, spatial, die, and measurement layers, and calibrate configured correlation pairs to their target coefficients within tolerance. Test items default to physically motivated distributions and relationships, and additive defect signatures operate on the same spatial field and can target either test items or bin-failure probabilities.

Considered alternatives: independent per-step random generation with post-hoc correlation fitting (breaks spatial structure and cannot guarantee joint distributions) and perturbing real datasets (limited control, licensing and privacy constraints).

</details>
