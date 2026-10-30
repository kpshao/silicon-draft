# 晶圆几何、Die Grid 变换与 Die Index Origin 约定

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 将配置的 Die Grid 映射到圆形晶圆上，并导出 YMS 分析可以比较的 die 坐标。这些约定之所以被记录，是因为更改它们会改变每一个生成的 Wafer Map，以及任何建立在其上的 golden 文件。

物理坐标系的原点在晶圆中心，+X 向右，+Y 向上。Notch 角度从 +X 逆时针度量；默认 270° 将 notch 置于底部。Notch 方向会被记录并用于 map 视图；它不改变 die 有效性，因为可用区域是圆形的。

Die 中心位于点阵 `gridOffset + (i × pitchWidth, j × pitchHeight)` 上，Die Pitch 默认等于 die 尺寸加 scribe。镜像围绕晶圆中心取反 X 和/或 Y，随后围绕晶圆中心进行旋转（0/90/180/270，逆时针）；die 足迹随网格一起变换。

当 die 的整个足迹位于晶圆半径减去 Edge Exclusion 之内时，该 die 有效。无效但与物理晶圆相交的 die 是 Partial Die。Partial Die 默认被排除；保留时它们以未测试（`partial`）状态导出，且绝不计入 Gross Die。Gross Die 统计有效 die；Net Die 统计通过 CP 的 die。在 CP 步骤落地之前，每个有效 die 都通过，因此 Net Die 等于 Gross Die。manifest 分别记录发现的 Partial Die（`partialDie`）和导出的 Partial Die（`partialDieRetained`）。

Die 坐标沿变换后网格的列轴和行轴编号，因此即使物理 die 集合对称，镜像和旋转也会重新标记 map。`center` 将最靠近晶圆中心的有效 die 标为 (0,0)。角点原点使用有效 map 在变换后网格坐标系中的包围盒：`bottomLeft` 产生非负坐标，`topRight` 产生非正坐标，其他角点则混合正负号。锚点只使用有效 die，因此保留 Partial Die 绝不会重新编号受测 map。

几何在生成前校验：非正尺寸、使可用半径为零的 Edge Exclusion、小于其 die 尺寸的 pitch、不支持的旋转或 index origin、没有任何有效 die 的网格，以及超过 1,000,000 个候选位置的网格都会被拒绝。

替代方案：在原始晶圆 X/Y 坐标系中分配坐标（旋转居中点阵将不可观察，使 YMS 方向设置失效）；以实际边缘 die 作为角点原点的锚点（坐标依赖不规则的晶圆边缘）；将保留的 Partial Die 计入 Gross Die（虚增良率分母）。

<details>
<summary>English</summary>

# Wafer geometry, Die Grid transforms, and Die Index Origin conventions

SiliconDraft maps a configured Die Grid onto a circular wafer and exports die coordinates that YMS analyses can compare. These conventions are recorded because changing them would change every generated Wafer Map and any golden files built on it.

The physical frame has its origin at the wafer center, +X to the right, and +Y up. The Notch angle is counter-clockwise from +X; the default 270° puts the notch at the bottom. Notch orientation is recorded and used by map views; it does not change die validity because the usable region is circular.

Die centers lie on the lattice `gridOffset + (i × pitchWidth, j × pitchHeight)`, with Die Pitch defaulting to die size plus scribe. Mirroring negates X and/or Y about the wafer center, then rotation (0/90/180/270 counter-clockwise) is applied about the wafer center; the die footprints transform with the grid.

A die is valid when its entire footprint lies inside the wafer radius minus Edge Exclusion. A die that is not valid but intersects the physical wafer is a Partial Die. Partial Die are excluded by default; when retained they are exported as untested (`partial`) and never count toward Gross Die. Gross Die counts valid die; Net Die counts CP-passing die. Until the CP step lands, every valid die passes, so Net Die equals Gross Die. The manifest records the Partial Die found (`partialDie`) and the number exported (`partialDieRetained`) separately.

Die coordinates are indexed along the transformed grid's column and row axes, so mirroring and rotation relabel the map even when the physical die set is symmetric. `center` labels the valid die nearest the wafer center (0,0). Corner origins use the valid map's bounding box in the transformed grid frame: `bottomLeft` yields non-negative coordinates and `topRight` non-positive ones, with the other corners mixing signs. Anchors use valid die only, so retaining Partial Die never renumbers the tested map.

Geometry is validated before generation: non-positive sizes, an Edge Exclusion that leaves no usable radius, a pitch below its die dimension, an unsupported rotation or index origin, a grid with no valid die, and a grid exceeding 1,000,000 candidate positions are rejected.

Alternatives: assigning coordinates in the raw wafer X/Y frame (rotating a centered lattice would be unobservable, defeating the YMS-orientation setting); anchoring corner origins on an actual edge die (coordinates depend on the irregular wafer edge); counting retained Partial Die in Gross Die (inflates the yield denominator).

</details>
