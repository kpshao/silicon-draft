# SiliconDraft

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 生成带有已知注入属性的合成 CP、FT 和 WAT 数据集，使良率管理系统可以对照 ground truth 进行验证。

## 语言

### 数据集与可追溯性

**Product（产品）**：
正在制造和测试的器件；一个 Dataset 恰好覆盖一个 Product。
_Avoid_: Device, chip

**Dataset（数据集）**：
一次完整的生成输出：一组 lot 的 CP、FT 和 WAT 数据，连同其 Ground-Truth Manifest，由一份配置和一个 seed 产生。
_Avoid_: Run, batch

**Lot（批次）**：
一起加工的一批晶圆，是 lot 级趋势与相关性的单位。
_Avoid_: Batch

**Wafer（晶圆）**：
Lot 中的单片晶圆；它携带一张 Die Map 和一组 WAT 测量。
_Avoid_: Slice

**Die（晶粒）**：
晶圆上特定 X/Y 位置的一个候选芯片；它携带 CP 结果。
_Avoid_: Site (unqualified)

**FT Unit（终测单元）**：
经过 FT 的封装器件，可追溯到恰好一个 die。
_Avoid_: Part, chip

**Assembly Loss（封装损失）**：
通过 CP 但从未成为 FT Unit 的 die。
_Avoid_: Attrition

**Assembly Lot（封装批次）**：
把来自一个制造 Lot 的 die 转换成 FT Unit 的封装批次；导出为 `ASSEMBLY_LOT`。
_Avoid_: Package lot

**Escape（逃逸品）**：
CP 失败却仍然作为 FT Unit 出现的 die，用于验证一致性检查。
_Avoid_: Leakage

**CP-FT Traceability（CP-FT 追溯）**：
将每个 FT Unit 映射回其来源晶圆与 die 坐标的能力。

### 测试步骤与测试项

**CP（Circuit Probe）**：
对每个有效 die 执行的晶圆级电测。
_Avoid_: Wafer Sort

**FT（Final Test）**：
封装后对每个 FT Unit 执行的电测。

**WAT（Wafer Acceptance Test）**：
对每个晶圆上少数固定位置的工艺控制监控结构进行的电测，不按 die 进行。
_Avoid_: PCM

**PCM Structure（PCM 结构）**：
放置在 scribe line 中、用于 WAT 测量的工艺控制监控结构。
_Avoid_: Test die

**PCM Site（PCM 测量点）**：
对 PCM 结构进行测量的固定晶圆位置；WAT 的采样单位。
_Avoid_: Site (unqualified)

**Tester Site（测试机 site）**：
测试机的一个并行测试资源，可能引入系统性的测量偏移。
_Avoid_: Site (unqualified)

**Site Offset（站点偏移）**：
某个 Test Item 在特定 Tester Site 上的加性测量偏移，用于模拟 site-to-site 系统差异。
_Avoid_: Site bias, per-site shift

**Test Program（测试程序）**：
在某个测试步骤中施加于一个 Product 的有序 test item、limits 和 bins 集合。
_Avoid_: Recipe

**Test Item（测试项）**：
具有业务含义、单位、limits，并且恰好属于一个测试步骤的具名测量。
_Avoid_: Parameter, test

**CP Catalog（CP 测试项目录）**：
内置的 CP Test Item 集合，包含其单位、默认分布、limits 和失败 Soft Bin；由于导出数据会记录产生它的定义，因此带版本。
_Avoid_: Test list

**FT Catalog（FT 测试项目录）**：
内置的 FT Test Item 集合，包含其单位、默认分布、limits 和失败 Soft Bin；由于导出数据会记录产生它的定义，因此带版本。
_Avoid_: FT test list

**WAT Catalog（WAT 测试项目录）**：
内置的 WAT Test Item 集合，包含其单位、默认分布以及 spec 与 test limits；由于导出数据会记录产生它的定义，因此带版本。
_Avoid_: WAT test list

**Numeric Test Item（数值型测试项）**：
产生测量值的 test item。
_Avoid_: Parametric test

**Functional Test Item（功能型测试项）**：
对照某个 pattern 或条件产生 pass/fail 结果的 test item。
_Avoid_: Digital test

**Spec Limits（规格限）**：
Test Item 的产品规格范围，用于导出分析，而不是用来判定 pass/fail。
_Avoid_: Product limits

**Test Limits（测试限）**：
测试机判定 Test Item 是否通过时使用的 limits；结果超出范围即该 item 失败。
_Avoid_: Guardband limits

**Bin（分箱）**：
测试后分配给 die 或 FT Unit 的分类。
_Avoid_: Category, code

**Hard Bin（硬分箱）**：
die 或 FT Unit 的最终处置类别，例如 pass 或某个失败类别。

**Soft Bin（软分箱）**：
更细的结果类别，通常是失败的 test item 或失效模式。
_Avoid_: Sub-bin

**Parametric Fail（参数失败）**：
CP 时第一个失败的 Test Item 为 Numeric 时所分配的 Hard Bin 类别。
_Avoid_: Param fail bin

**Functional Fail（功能失败）**：
CP 时第一个失败的 Test Item 为 Functional 时所分配的 Hard Bin 类别。
_Avoid_: Func fail bin

### 相关性与变异

**Correlation Layer（相关性层级）**：
Test Item 相互关联的三个层级之一：die、wafer 或 lot。

**Die-Level Correlation（die 级相关性）**：
同一个 die 的 CP 与 FT 结果之间的相关性。

**Wafer-Level Correlation（wafer 级相关性）**：
WAT 的 wafer 统计量与其 CP 或 FT 的 wafer 统计量之间的相关性。

**Lot-Level Correlation（lot 级相关性）**：
不同时间点 lot 统计量之间的相关性。

**Latent Factor（潜在因子）**：
驱动多个 test item、从而产生相关性的未观测工艺或质量变量。

**Spatial Field（空间场）**：
在晶圆内部由 test item 共享的变异，与 die 级噪声分开。

**Defect Signature（缺陷图形）**：
刻意注入的空间或时间图形，良率管理系统应当能够检测到它。
_Avoid_: Anomaly, pattern

**Target Yield Calibration（目标良率校准）**：
调整生成参数，使实际良率和 bin mix 匹配配置目标。
_Avoid_: Yield forcing

### 晶圆几何

**Die Map（die 地图）**：
晶圆上有效 die 位置的集合。

**Die Grid（die 网格）**：
由 die 尺寸、pitch 和 grid offset 定义、覆盖在晶圆上的矩形 die 位置阵列。

**Die Grid Offset（die 网格偏移）**：
Die Grid 相对晶圆中心的物理 X/Y 平移，单位为毫米。

**Die Index Origin（die 索引原点）**：
被指定为 (0,0)、用于坐标编号和导出的 die 位置。

**Die Pitch（die 间距）**：
相邻 die 位置的中心到中心距离；die 尺寸加 scribe lane。

**Wafer Map（晶圆图）**：
按 bin 或测量值着色的 Die Map 良率管理视图。

**Wafer Map CSV（晶圆图 CSV）**：
Wafer Map 的表格导出；每行一个 die，包含其 Die Index 坐标、物理中心和状态。

**Notch（缺口）**：
定义晶圆方向的参考特征。

**Edge Exclusion（边缘排除）**：
晶圆外侧不测试 die 的环形区域。

**Partial Die（部分 die）**：
不完全位于晶圆减去 Edge Exclusion 之内、但仍与晶圆相交的 die 位置；保留的 Partial Die 不被测试。
_Avoid_: Edge die

**Valid Die（有效 die）**：
全部面积位于晶圆减去 Edge Exclusion 之内的 die；每个 Valid Die 都被测试并计入 Gross Die。

**Gross Die（总 die 数）**：
晶圆上 Valid Die 位置的数量。

**Net Die（净 die 数）**：
通过 CP 的 die 数量。
_Avoid_: Good die

### 配置与时间

**Resolved Config（解析后配置）**：
应用默认值和 preset 之后的生效配置，记录在 Ground-Truth Manifest 中。

**Simulation Time（仿真时间）**：
由配置派生的时间轴，用于 lot 和 wafer 时间戳，独立于 wall-clock 时间。
_Avoid_: Fake time, synthetic time

### 验证

**Ground-Truth Manifest（真值清单）**：
记录刻意注入到 Dataset 中的属性及其预期统计结果，用于评判良率管理系统的分析。
_Avoid_: Metadata

<details>
<summary>English</summary>

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

**Assembly Lot**:
The assembly batch that turns die from one manufacturing Lot into FT units; exported as `ASSEMBLY_LOT`.
_Avoid_: Package lot

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

**Site Offset**:
An additive measurement offset a Test Item carries on a specific Tester Site, modeling systematic site-to-site differences.
_Avoid_: Site bias, per-site shift

**Test Program**:
The ordered set of test items, limits, and bins applied to one Product at one test step.
_Avoid_: Recipe

**Test Item**:
A named measurement with a business meaning, a unit, limits, and membership in exactly one test step.
_Avoid_: Parameter, test

**CP Catalog**:
The built-in set of CP Test Items with their units, default distributions, limits, and failure Soft Bins; versioned because exported data records which definitions produced it.
_Avoid_: Test list

**FT Catalog**:
The built-in set of FT Test Items with their units, default distributions, limits, and failure Soft Bins; versioned because exported data records which definitions produced it.
_Avoid_: FT test list

**WAT Catalog**:
The built-in set of WAT Test Items with their units, default distributions, and spec and test limits; versioned because exported data records which definitions produced it.
_Avoid_: WAT test list

**Numeric Test Item**:
A test item that yields a measured value.
_Avoid_: Parametric test

**Functional Test Item**:
A test item that yields pass/fail against a pattern or condition.
_Avoid_: Digital test

**Spec Limits**:
The product specification range for a Test Item, exported for analysis rather than used to decide pass/fail.
_Avoid_: Product limits

**Test Limits**:
The limits the tester applies when deciding whether a Test Item passes; a result outside them fails that item.
_Avoid_: Guardband limits

**Bin**:
The classification assigned to a die or FT unit after test.
_Avoid_: Category, code

**Hard Bin**:
The final disposition category of a die or FT unit, such as pass or a fail class.

**Soft Bin**:
A finer outcome category, typically the failing test item or failure mode.
_Avoid_: Sub-bin

**Parametric Fail**:
The Hard Bin class assigned when the first failing Test Item at CP is Numeric.
_Avoid_: Param fail bin

**Functional Fail**:
The Hard Bin class assigned when the first failing Test Item at CP is Functional.
_Avoid_: Func fail bin

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

</details>
