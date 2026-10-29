# Ground-truth manifest as a first-class output

A SiliconDraft dataset only serves as a benchmark if a yield management system's analyses can be checked against what was deliberately injected. Every run therefore writes a manifest recording the configuration, seed, effective wafer geometry, target and achieved yield and bin mix, configured correlation pairs with expected coefficients, injected defect signatures, and expected statistical outcomes; verification compares achieved against expected and fails outside configured tolerances.

Alternatives: treating the configuration file as the only record (loses achieved statistics and tolerances) or logging ground truth to stdout (not auditable or reproducible).
