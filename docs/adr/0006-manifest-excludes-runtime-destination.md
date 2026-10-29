# The ground-truth manifest excludes the runtime output destination

SiliconDraft must reproduce a Dataset from the same configuration and seed, and the ground-truth manifest is the record used to compare runs. The CLI lets `--output` override the configured destination. If that effective destination were recorded in the Resolved Config inside the manifest, the same configuration and seed would produce different manifests depending on where the run was written, weakening reproducibility and making golden-file tests depend on temporary paths.

The manifest therefore records the configuration as resolved (including its configured `output.directory`) together with the seed and Dataset metadata. Runtime-only overrides such as the destination directory are not recorded; the manifest lives under `_truth/` at the Dataset root, so the Dataset's location is self-evident.

The manifest is filled in as the capabilities that produce its sections land. Issue #3 (the walking skeleton) records the identity sections: `manifestSchemaVersion`, `configSchemaVersion`, `seed`, `resolvedConfig`, and `dataset`. Effective geometry, injected truth, expected/achieved statistics, file hashes, and verification results are added by the tickets that produce them, as required by ADR-0003.

Alternatives: record the effective destination (breaks reproducibility across output locations), omit the output section entirely (loses a configured setting), or forbid `--output` overrides (the spec makes configuration primary but still allows overrides).
