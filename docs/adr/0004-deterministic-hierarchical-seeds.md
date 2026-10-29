# Deterministic hierarchical seeds and simulation time

SiliconDraft must reproduce a Dataset byte-for-byte from the same configuration and seed, even when generation is parallelized across lots and wafers. We derive independent child seeds for each lot, wafer, and die from the Dataset seed instead of sharing one global random stream, and every timestamp comes from the configured simulation time rather than the system clock. Scheduling therefore changes neither content nor ordering, and the ground-truth manifest contains no wall-clock time.

Considered alternatives: one global RNG (simple but scheduling-dependent) and recording wall-clock timestamps in outputs (breaks reproducibility).
