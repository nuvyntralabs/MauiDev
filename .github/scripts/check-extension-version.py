#!/usr/bin/env python3
"""Require extension/vscode/package.json version to match Directory.Build.props Version."""

from __future__ import annotations

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def fail(message: str) -> None:
    print(f"::error::{message}")
    raise SystemExit(1)


def props_version(path: Path) -> str:
    tree = ET.parse(path)
    for element in tree.iter():
        if element.tag.split("}")[-1] == "Version" and element.text and not element.text.strip().startswith("$("):
            return element.text.strip()
    fail(f"{path} has no Version")


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    expected = props_version(root / "Directory.Build.props")
    package = json.loads((root / "extension" / "vscode" / "package.json").read_text(encoding="utf-8"))
    actual = str(package.get("version", "")).strip()
    print(f"Directory.Build.props Version={expected}")
    print(f"extension/vscode/package.json version={actual}")
    if actual != expected:
        fail(f"Extension version {actual} must match {expected}")
    print("Extension version is aligned")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
