# WAT TXT export format

WAT measurements are sampled at PCM Sites rather than per Die, so they do not share the CP STDF path. Each Dataset writes tab-separated TXT files named `<stem>_WAT_<template>.txt`, where the stem is the Lot id by default or the Wafer id when `wat.organization` is `perWafer`. Both templates begin with `# catalogVersion=<version>` so an exported file names the catalog that produced its fields.

Long format repeats one row per Site and Test Item and carries `site`, `siteXmm`, `siteYmm`, `testItem`, `testNumber`, `value`, `unit`, `lowSpec`, `highSpec`, `lowTest`, `highTest` alongside `lotId` and `waferId`. Wide format writes one row per Site per Wafer, puts each Test Item value in its own column, and records each item's number, unit, spec limits, and test limits in a leading `#` definition block, because a column cannot carry units or limits.

The default PCM Site layout is the wafer center plus eight points halfway between the center and the wafer edge at 45° increments; coordinates are millimeters from the wafer center. The layout is configurable because different fabs sample different scribe-line structures. Every value is seeded from the Dataset seed, Lot, Wafer, Site, and Test Item, which keeps output independent of generation order (ADR-0004).

The manifest records the WAT catalog version, site and item counts, and measurement counts per Dataset, lot, and wafer, so exported fields can be reconciled with the catalog version recorded for the Dataset.

Alternatives: one STDF WAT file (YMS WAT importers consume TXT, not STDF); per-Die WAT rows (WAT is sampled at PCM Sites, not per Die); embedding units and limits only in the long template (leaves wide imports unable to interpret values).
