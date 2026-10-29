# Shared latent-factor model for cross-step correlation

SiliconDraft must produce CP, FT, and WAT data whose correlations are known and controllable at die, wafer, and lot levels, and whose spatial signatures are deliberate rather than emergent. We generate every test item from shared latent factors (lot, wafer, die, plus a within-wafer spatial field) with per-item loadings, decompose variance into lot, wafer, spatial, die, and measurement layers, and calibrate configured correlation pairs to their target coefficients within tolerance. Test items default to physically motivated distributions and relationships, and additive defect signatures operate on the same spatial field and can target either test items or bin-failure probabilities.

Considered alternatives: independent per-step random generation with post-hoc correlation fitting (breaks spatial structure and cannot guarantee joint distributions) and perturbing real datasets (limited control, licensing and privacy constraints).
