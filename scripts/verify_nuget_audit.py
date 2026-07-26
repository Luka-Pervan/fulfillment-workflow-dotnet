#!/usr/bin/env python3
"""Fail closed when a .NET JSON package audit is invalid or reports vulnerabilities."""

from pathlib import Path
import json
import sys
from typing import Any


def collect_vulnerabilities(value: Any) -> list[dict[str, Any]]:
    """Return every vulnerability record from the documented package-list tree."""
    found: list[dict[str, Any]] = []
    if isinstance(value, dict):
        for key, child in value.items():
            if key == "vulnerabilities":
                if not isinstance(child, list):
                    raise ValueError("'vulnerabilities' must be a JSON array.")
                if not all(isinstance(item, dict) for item in child):
                    raise ValueError("Every vulnerability must be a JSON object.")
                found.extend(child)
            else:
                found.extend(collect_vulnerabilities(child))
    elif isinstance(value, list):
        for child in value:
            found.extend(collect_vulnerabilities(child))
    return found


def verify(payload: dict[str, Any]) -> list[dict[str, Any]]:
    """Validate the audit shape and return any reported vulnerabilities."""
    if payload.get("version") != 1:
        raise ValueError("Unsupported or missing NuGet audit JSON version.")
    projects = payload.get("projects")
    if not isinstance(projects, list) or not projects:
        raise ValueError("NuGet audit did not contain any projects.")
    if not all(
        isinstance(project, dict) and isinstance(project.get("path"), str)
        for project in projects
    ):
        raise ValueError("NuGet audit project entries are malformed.")
    return collect_vulnerabilities(projects)


def main() -> int:
    if len(sys.argv) != 2:
        print("Usage: verify_nuget_audit.py <audit.json>", file=sys.stderr)
        return 2

    try:
        payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
        if not isinstance(payload, dict):
            raise ValueError("NuGet audit root must be a JSON object.")
        vulnerabilities = verify(payload)
    except (OSError, json.JSONDecodeError, ValueError) as exception:
        print(f"NuGet audit verification failed: {exception}", file=sys.stderr)
        return 1

    if vulnerabilities:
        print(
            f"NuGet audit found {len(vulnerabilities)} vulnerable package record(s).",
            file=sys.stderr,
        )
        return 1

    print(f"NuGet audit passed for {len(payload['projects'])} projects.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
