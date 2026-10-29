# C#/.NET 10 with Avalonia and a UI-free shared core

SiliconDraft must generate large CP/FT/WAT datasets and stream them to STDF/TXT at high throughput while offering both a GUI and a CLI. We use C# on .NET 10 LTS, Avalonia for the GUI, and a UI-free `SiliconDraft.Core` library consumed by both `SiliconDraft.Cli` and `SiliconDraft.Gui`, with macOS as the first delivery target and Windows/Linux to follow.

Considered alternatives: Python with a web or Electron UI (easier parsing ecosystem, weaker throughput and heavier packaging) and C++ with Qt (highest performance, much higher development cost). The chosen stack keeps native-class throughput, one cross-platform codebase, and a clean seam between core generation and both user interfaces.
