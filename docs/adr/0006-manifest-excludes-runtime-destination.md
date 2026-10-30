# Ground-Truth Manifest 不记录运行时输出目标

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 必须从相同配置和 seed 复现一个 Dataset，而 Ground-Truth Manifest 是用来比较各次运行的记录。CLI 允许 `--output` 覆盖配置的目标目录。如果把这个生效目标记录进 manifest 内的 Resolved Config，那么相同配置和 seed 会因运行写入位置不同而产生不同的 manifest，削弱可复现性，并使 golden-file 测试依赖临时路径。

因此 manifest 记录解析后的配置（包括其配置的 `output.directory`），以及 seed 和 Dataset 元数据。诸如目标目录这样的仅运行时覆盖不会被记录；manifest 位于 Dataset 根目录下的 `_truth/`，因此 Dataset 的位置本身一目了然。

manifest 随着产生其各节的能力落地而逐步填充。Issue #3（walking skeleton）记录身份相关节：`manifestSchemaVersion`、`configSchemaVersion`、`seed`、`resolvedConfig` 和 `dataset`。生效几何、注入 truth、预期/实际统计、文件哈希和验证结果由产生它们的 ticket 按 ADR-0003 的要求加入。

替代方案：记录生效目标（跨输出位置时破坏可复现性）、完全省略 output 段（丢失一项配置设置），或禁止 `--output` 覆盖（spec 以配置为主，但仍允许覆盖）。

<details>
<summary>English</summary>

# The ground-truth manifest excludes the runtime output destination

SiliconDraft must reproduce a Dataset from the same configuration and seed, and the ground-truth manifest is the record used to compare runs. The CLI lets `--output` override the configured destination. If that effective destination were recorded in the Resolved Config inside the manifest, the same configuration and seed would produce different manifests depending on where the run was written, weakening reproducibility and making golden-file tests depend on temporary paths.

The manifest therefore records the configuration as resolved (including its configured `output.directory`) together with the seed and Dataset metadata. Runtime-only overrides such as the destination directory are not recorded; the manifest lives under `_truth/` at the Dataset root, so the Dataset's location is self-evident.

The manifest is filled in as the capabilities that produce its sections land. Issue #3 (the walking skeleton) records the identity sections: `manifestSchemaVersion`, `configSchemaVersion`, `seed`, `resolvedConfig`, and `dataset`. Effective geometry, injected truth, expected/achieved statistics, file hashes, and verification results are added by the tickets that produce them, as required by ADR-0003.

Alternatives: record the effective destination (breaks reproducibility across output locations), omit the output section entirely (loses a configured setting), or forbid `--output` overrides (the spec makes configuration primary but still allows overrides).

</details>
