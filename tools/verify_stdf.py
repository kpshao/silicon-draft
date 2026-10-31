#!/usr/bin/env python3
"""Independent STDF V4 sanity check using Semi-ATE-STDF.

This is a developer tool, not part of `dotnet test` or CI. It verifies that a
generated file is readable by a third-party parser and that its record set
matches the step contract. It does not compare against the golden files.

Install the pinned parser in a throwaway environment:

    python3 -m venv .venv-stdf
    .venv-stdf/bin/pip install -r tools/requirements-stdf.txt

Usage:

    .venv-stdf/bin/python tools/verify_stdf.py out/LOT-0001_CP.stdf out/LOT-0001_FT.stdf

Exit codes: 0 = every file passed, 1 = a structural check failed, 2 = the
parser is missing or the arguments are unusable.
"""
from __future__ import annotations

import collections
import sys
from pathlib import Path

try:
    from Semi_ATE import STDF
except ImportError as exc:
    print(
        "error: Semi-ATE-STDF is not installed; see the install steps in this file",
        file=sys.stderr,
    )
    raise SystemExit(2) from exc

CP_RECORDS = frozenset(
    {
        (0, 10),  # FAR
        (1, 10),  # MIR
        (1, 20),  # MRR
        (1, 30),  # PCR
        (1, 40),  # HBR
        (1, 50),  # SBR
        (1, 80),  # SDR
        (2, 10),  # WIR
        (2, 20),  # WRR
        (2, 30),  # WCR
        (5, 10),  # PIR
        (5, 20),  # PRR
        (10, 30),  # TSR
        (15, 10),  # PTR
        (15, 20),  # FTR
    }
)
FT_RECORDS = (CP_RECORDS - {(2, 10), (2, 20), (2, 30)}) | {(0, 20)}  # ATR
WAFER_RECORDS = frozenset({(2, 10), (2, 20), (2, 30)})

REQUIRED_FIELDS = {
    (5, 10): ("SITE_NUM",),
    (5, 20): ("SITE_NUM", "HARD_BIN", "SOFT_BIN", "X_COORD", "Y_COORD"),
    (15, 10): ("SITE_NUM", "TEST_TXT", "UNITS", "LO_LIMIT", "HI_LIMIT"),
    (15, 20): ("SITE_NUM", "TEST_TXT", "TEST_FLG"),
}


def record_kind(record) -> tuple[int, int]:
    return record.get_value("REC_TYP"), record.get_value("REC_SUB")


def check(path: Path) -> list[str]:
    errors: list[str] = []
    counts: collections.Counter[tuple[int, int]] = collections.Counter()
    order: list[tuple[int, int]] = []
    sites: set[int] = set()
    sdr_sites: int | None = None
    step: str | None = None

    for record in STDF.records_from_file(str(path)):
        kind = record_kind(record)
        counts[kind] += 1
        order.append(kind)
        if kind == (1, 10):  # MIR
            step = record.get_value("TEST_COD")
        elif kind == (1, 80):  # SDR
            sdr_sites = record.get_value("SITE_CNT")
        if kind in {(5, 10), (5, 20), (15, 10), (15, 20)}:
            site = record.get_value("SITE_NUM")
            if site is not None:
                sites.add(site)
        for field in REQUIRED_FIELDS.get(kind, ()):
            if record.get_value(field) is None:
                errors.append(f"{kind[0]}/{kind[1]} record is missing {field}")

    if not order:
        return [f"{path.name}: no records"]
    if order[0] != (0, 10):
        errors.append(f"{path.name}: FAR must be the first record")
    if order[-1] != (1, 20):
        errors.append(f"{path.name}: MRR must be the last record")
    if step not in ("CP", "FT"):
        errors.append(f"{path.name}: MIR TEST_COD must be CP or FT, found {step!r}")

    expected = CP_RECORDS if step == "CP" else FT_RECORDS
    for kind in sorted(expected - set(counts)):
        errors.append(f"{path.name}: missing record {kind[0]}/{kind[1]}")

    wafer_records = WAFER_RECORDS & set(counts)
    if step == "CP" and wafer_records != WAFER_RECORDS:
        errors.append(f"{path.name}: CP is missing wafer records {sorted(WAFER_RECORDS - wafer_records)}")
    if step == "FT" and wafer_records:
        errors.append(f"{path.name}: FT must not contain wafer records {sorted(wafer_records)}")

    if sdr_sites is None or sdr_sites < 1:
        errors.append(f"{path.name}: SDR must declare at least one Tester Site")
    if not sites or 0 in sites:
        errors.append(f"{path.name}: part and test records must carry a Tester Site")

    print(
        f"{path.name}: {step or '?'} {sum(counts.values())} records, "
        f"SDR={sdr_sites} sites={sorted(sites)}"
    )
    return errors


def main(argv: list[str]) -> int:
    if not argv:
        print("usage: verify_stdf.py <file.stdf> [<file.stdf> ...]", file=sys.stderr)
        return 2

    failed = False
    for argument in argv:
        path = Path(argument)
        if not path.is_file():
            print(f"error: {path} does not exist", file=sys.stderr)
            failed = True
            continue
        try:
            errors = check(path)
        except Exception as exc:  # third-party parser failure is itself a finding
            print(f"{path.name}: parse failed: {exc}", file=sys.stderr)
            failed = True
            continue
        for error in errors:
            print(f"error: {error}", file=sys.stderr)
        failed = failed or bool(errors)

    print("STDF verification passed" if not failed else "STDF verification failed")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
