# Latent-factor 采样与 die 级相关性校准

> 中文版为主版本；英文原文见文末折叠区。

每个 Numeric Test Item 的值由五个独立标准正态分量构成：lot 因子、wafer 因子、Spatial Field、die 因子和 measurement noise。每个分量的权重是其方差分量的平方根，五个分量之和为 1，因此 item 的 marginal 分布仍是 catalog 声明的 Normal 或 Lognormal（lognormal 在 log 空间分解）。Spatial Field 是共享的，但每个 item 可以配置自己的 spatial loading：载荷 L 使 spatial 权重乘以 L，其余四层除以 sqrt(1 - spatial + spatial·L²)，从而保持 item 的总方差为 1，并让不同 item 对同一空间图形有不同敏感度。lot、wafer 和 spatial 因子在所有 item 之间共享，所以未被显式配对的 item 也带有这些层级的共同变异；die 层由每个关联对专属的因子与独立残差组成；measurement noise 每次测量独立。

CP 与 FT 对同一个 Die 使用相同的 lot、wafer、spatial 因子和关联对因子；die 残差与 measurement noise 的 seed 包含 step，因此两个 step 的残余变异独立，CP-FT 相关性只来自共享因子。die 级关联对的目标 r 在测量值尺度上定义；Normal/Normal、Lognormal/Lognormal 和混合分布都用闭式 Gaussian 恒等式换算成潜在相关，再减去共享层基线并除以 die 层方差占比，得到所需的 die 层相关。FT Unit 只是 CP 通过的 die 子集，条件化会轻微改变观测相关，因此生成器最多进行 4 轮确定性校准，用实际 FT 样本的 achieved r 修正 die 层相关；校准只依据 Dataset seed 和配置，因此结果仍可复现。校验阶段拒绝无法由配置的方差分量实现的目标，以及同一 item 的 die 层预算被多个关联对超支的配置。

选择该方案而不是对各 step 独立采样后拟合相关性：独立采样无法保证联合分布或方差分解，后拟合会破坏空间结构。也没有采用任意多元正态协方差矩阵，因为多个关联对容易产生非半正定矩阵，且难以把方差稳定分解到五层；每个关联对使用专属因子可以在预算范围内独立精确校准。

Ground-Truth Manifest 的 `dataset.correlations.die` 记录目标 r、容差、实际 r 与样本数；`resolvedConfig.latent` 记录生效的方差分量与关联对。所有因子值由 Dataset seed 与 Lot、Wafer、Die 身份派生，因此相同 seed 可复现。

<details>
<summary>English</summary>

# Latent-factor sampling and die-level correlation calibration

Every Numeric Test Item value is built from five independent standard-normal components: a lot factor, a wafer factor, the Spatial Field, a die factor, and measurement noise. Each component's weight is the square root of its variance fraction, and the five fractions sum to one, so the item's marginal remains the Normal or Lognormal distribution the catalog declares (lognormal variation decomposes in log space). The Spatial Field is shared, but each item can configure its own spatial loading: a loading L multiplies the spatial weight by L and divides the other four layers by sqrt(1 - spatial + spatial·L²), which keeps the item's total variance at one while giving different items different sensitivity to the same spatial pattern. The lot, wafer, and spatial factors are shared by every item, so items without an explicit pair still carry common variation at those layers. The die layer combines a factor dedicated to each correlation pair with an independent residual, and measurement noise is independent for every measurement.

CP and FT use the same lot, wafer, spatial, and pair factors for the same Die. Their die residuals and measurement noises are seeded with the step in the key, so residual variation is independent across steps and CP-FT correlation comes only from shared factors. A die-level pair's target r is defined on the measurement scale. Closed-form Gaussian identities convert it to a latent correlation for Normal/Normal, Lognormal/Lognormal, and mixed pairs; subtracting the shared-layer baseline and dividing by the die variance share yields the required die-layer correlation. FT Units are only the CP-passing subset, and conditioning on that subset slightly changes the observed correlation, so generation runs up to four deterministic calibration passes that correct the die-layer correlations from the achieved FT-sample r; calibration depends only on the Dataset seed and configuration, so results stay reproducible. Validation rejects targets the configured variance components cannot realize and configurations where several pairs overdraw one item's die-layer budget.

We chose this over independent per-step sampling followed by correlation fitting: independent sampling cannot guarantee a joint distribution or a variance decomposition, and post-hoc fitting destroys spatial structure. We also rejected an arbitrary multivariate-normal covariance matrix, because several pairs easily make it non-positive-semidefinite and it makes a stable five-layer variance decomposition awkward; one factor per pair calibrates each pair exactly and independently within its budget.

The ground-truth manifest's `dataset.correlations.die` records the target r, tolerance, achieved r, whether it landed within tolerance, and the sample count; `resolvedConfig.latent` records the effective variance components, spatial loadings, and pairs. Every factor value derives from the Dataset seed and the Lot, Wafer, and Die identity, so the same seed reproduces the data.

</details>
