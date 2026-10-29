# Minimal STDF V4 CP export

The first CP loop must produce a standard-format file that a yield management system can ingest before the full STDF feature set lands. Each Lot therefore exports one `<LOTID>_CP.stdf` containing FAR, MIR, one PIR/PTR/PRR group per tested Die, and MRR. The file is little-endian, FAR declares version 4, every Test Item is a PTR carrying TEST_TXT, UNITS, test limits (LO_LIMIT/HI_LIMIT), and spec limits (LO_SPEC/HI_SPEC), and PRR carries NUM_TEST, HARD_BIN, SOFT_BIN, X_COORD, Y_COORD, and a `wafer:die` PART_ID so die identity survives without WIR.

Functional Test Items are exported as PTRs with a 1/0 result, P/F units, and 0.5–1.5 limits because the acceptance criteria require every item to carry units and limits, while FTR carries neither. FTR, PCR, HBR, SBR, WIR, multi-site records, and the remaining optional fields are deliberately deferred to T6 rather than approximated here.

Timestamps come from a fixed simulation epoch rather than the system clock, so the same configuration and seed produce byte-identical files and the golden file stays stable until Simulation Time is configured.

Byte-identical output is guaranteed for a given runtime and platform. The sampling distributions use transcendental functions whose last bit may differ across platforms, so the golden STDF file is a same-platform snapshot rather than a cross-platform byte contract.

Alternatives: use FTR for functional items (no units or limits in the record, failing the acceptance criteria), export functional items without limits, or write the complete STDF record set in T3 (duplicates T6 and delays the first usable CP loop).
