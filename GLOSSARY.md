# SiliconDraft

SiliconDraft generates synthetic CP, FT, and WAT datasets with known injected properties, so a yield management system can be validated against ground truth.

## Language

### Dataset and traceability

**Product**:
The device being manufactured and tested; a Dataset covers exactly one Product.
_Avoid_: Device, chip

**Dataset**:
One complete generation output: the CP, FT, and WAT data for a set of lots, together with its ground-truth manifest, produced from one configuration and one seed.
_Avoid_: Run, batch

**Lot**:
A manufacturing batch of wafers processed together; the unit for lot-level trends and correlations.
_Avoid_: Batch

**Wafer**:
A single silicon wafer within a lot; it carries one die map and one WAT measurement set.
_Avoid_: Slice

**Die**:
A candidate chip at a specific X/Y position on a wafer; it carries CP results.
_Avoid_: Site (unqualified)

**FT Unit**:
A packaged device that undergoes FT and is traceable to exactly one die.
_Avoid_: Part, chip

**Assembly Loss**:
A CP-passing die that never becomes an FT unit.
_Avoid_: Attrition

**Escape**:
A CP-failing die that nevertheless appears as an FT unit, used to validate consistency checks.
_Avoid_: Leakage

**CP-FT Traceability**:
The ability to map every FT unit back to its originating wafer and die coordinates.

### Test steps and items

**CP (Circuit Probe)**:
Wafer-level electrical test performed on every valid die.
_Avoid_: Wafer Sort

**FT (Final Test)**:
Electrical test performed on every FT unit after assembly and packaging.

**WAT (Wafer Acceptance Test)**:
Electrical measurement of process control monitor structures at a few fixed sites per wafer, not per die.
_Avoid_: PCM

**PCM Structure**:
A process control monitor structure placed in the scribe line for WAT measurement.
_Avoid_: Test die

**PCM Site**:
A fixed wafer location where PCM structures are measured; the sampling unit of WAT.
_Avoid_: Site (unqualified)

**Tester Site**:
One of a tester's parallel test resources, which can introduce systematic measurement offsets.
_Avoid_: Site (unqualified)

**Test Program**:
The ordered set of test items, limits, and bins applied to one Product at one test step.
_Avoid_: Recipe

**Test Item**:
A named measurement with a business meaning, a unit, limits, and membership in exactly one test step.
_Avoid_: Parameter, test

**Numeric Test Item**:
A test item that yields a measured value.
_Avoid_: Parametric test

**Functional Test Item**:
A test item that yields pass/fail against a pattern or condition.
_Avoid_: Digital test

**Bin**:
The classification assigned to a die or FT unit after test.
_Avoid_: Category, code

**Hard Bin**:
The final disposition category of a die or FT unit, such as pass or a fail class.

**Soft Bin**:
A finer outcome category, typically the failing test item or failure mode.
_Avoid_: Sub-bin

### Correlation and variation

**Correlation Layer**:
One of the three levels at which test items are linked: die, wafer, or lot.

**Die-Level Correlation**:
Correlation between the CP and FT results of the same die.

**Wafer-Level Correlation**:
Correlation between wafer statistics of WAT and wafer statistics of CP or FT.

**Lot-Level Correlation**:
Correlation between lot statistics across time.

**Latent Factor**:
An unobserved process or quality variable that drives several test items and hence creates correlation.

**Spatial Field**:
The within-wafer variation shared by test items, separate from die-level noise.

**Defect Signature**:
A deliberately injected spatial or temporal pattern that a yield management system is expected to detect.
_Avoid_: Anomaly, pattern

**Target Yield Calibration**:
Tuning generation parameters so the achieved yield and bin mix match configured targets.
_Avoid_: Yield forcing

### Wafer geometry

**Die Map**:
The set of valid die positions on a wafer.

**Die Grid**:
The rectangular array of die positions defined by die size, pitch, and grid offset, placed over a wafer.

**Die Grid Offset**:
The physical X/Y translation of the die grid relative to the wafer center, in millimeters.

**Die Index Origin**:
The die position designated (0,0) for coordinate numbering and export.

**Die Pitch**:
The center-to-center distance between adjacent die positions; die size plus scribe lane.

**Wafer Map**:
A yield management view of a die map colored by bin or measurement.

**Wafer Map CSV**:
The tabular export of a Wafer Map; each row is one die with its Die Index coordinates, physical center, and status.

**Notch**:
The reference feature that defines wafer orientation.

**Edge Exclusion**:
The outer wafer annulus in which die are not tested.

**Partial Die**:
A die position that is not fully inside the wafer minus Edge Exclusion but still intersects the wafer; retained Partial Die are untested.
_Avoid_: Edge die

**Valid Die**:
A die whose full area lies within the wafer minus Edge Exclusion; every Valid Die is tested and counted in Gross Die.

**Gross Die**:
The number of Valid Die positions on a wafer.

**Net Die**:
The number of die that pass CP.
_Avoid_: Good die

### Configuration and time

**Resolved Config**:
The effective configuration after defaults and presets are applied, recorded in the ground-truth manifest.

**Simulation Time**:
The config-derived time axis used for lot and wafer timestamps, independent of wall-clock time.
_Avoid_: Fake time, synthetic time

### Verification

**Ground-Truth Manifest**:
A record of the properties deliberately injected into a dataset and the statistical outcomes expected of it, used to judge a yield management system's analyses.
_Avoid_: Metadata
