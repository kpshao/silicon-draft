# 采用 C#/.NET 10、Avalonia 与无 UI 共享核心

> 中文版为主版本；英文原文见文末折叠区。

SiliconDraft 必须生成大规模 CP/FT/WAT 数据集，并以高吞吐流式写入 STDF/TXT，同时提供 GUI 和 CLI。我们采用 .NET 10 LTS 上的 C#、Avalonia 作为 GUI，以及一个无 UI 的 `SiliconDraft.Core` 库，供 `SiliconDraft.Cli` 和 `SiliconDraft.Gui` 共用；首个交付目标是 macOS，随后是 Windows/Linux。

考虑过的替代方案：Python 配 Web 或 Electron UI（解析生态更易用，但吞吐较弱、打包更重）与 C++ 配 Qt（性能最高，但开发成本高得多）。所选技术栈兼顾接近原生的吞吐、单一跨平台代码库，以及核心生成逻辑与两个用户界面之间的清晰 seam。

<details>
<summary>English</summary>

# C#/.NET 10 with Avalonia and a UI-free shared core

SiliconDraft must generate large CP/FT/WAT datasets and stream them to STDF/TXT at high throughput while offering both a GUI and a CLI. We use C# on .NET 10 LTS, Avalonia for the GUI, and a UI-free `SiliconDraft.Core` library consumed by both `SiliconDraft.Cli` and `SiliconDraft.Gui`, with macOS as the first delivery target and Windows/Linux to follow.

Considered alternatives: Python with a web or Electron UI (easier parsing ecosystem, weaker throughput and heavier packaging) and C++ with Qt (highest performance, much higher development cost). The chosen stack keeps native-class throughput, one cross-platform codebase, and a clean seam between core generation and both user interfaces.

</details>
