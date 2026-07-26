#!/usr/bin/env python3
"""Fail unless the required PostgreSQL functional test executed and passed."""

from pathlib import Path
import sys
import xml.etree.ElementTree as ET


EXPECTED_TEST = (
    "Fulfillment.Api.IntegrationTests.PostgresPersistenceTests."
    "PostgreSql_enforces_idempotency_concurrency_and_atomic_outbox"
)


def main() -> int:
    if len(sys.argv) != 2:
        print("Usage: verify-postgres-test-results.py <results.trx>", file=sys.stderr)
        return 2

    results_path = Path(sys.argv[1])
    if not results_path.is_file():
        print(f"TRX file was not created: {results_path}", file=sys.stderr)
        return 1

    root = ET.parse(results_path).getroot()
    results = [
        element
        for element in root.iter()
        if element.tag.endswith("UnitTestResult")
        and element.attrib.get("testName") == EXPECTED_TEST
    ]

    if len(results) != 1 or results[0].attrib.get("outcome") != "Passed":
        print(
            f"Expected exactly one passing result for {EXPECTED_TEST}; "
            f"found {[result.attrib.get('outcome') for result in results]}.",
            file=sys.stderr,
        )
        return 1

    print(f"Verified passing PostgreSQL functional test: {EXPECTED_TEST}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
