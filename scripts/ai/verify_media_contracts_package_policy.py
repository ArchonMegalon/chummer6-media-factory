#!/usr/bin/env python3
"""Authorize Media.Contracts packaging outside attacker-controlled MSBuild evaluation."""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_POLICY = ROOT / "eng/media-contracts-package-policy.json"
POLICY_KEYS = {
    "contract",
    "authorized",
    "packageId",
    "project",
    "approvedLicense",
    "reason",
}
LICENSE_KEYS = {"kind", "value"}
FORBIDDEN_ARGUMENT = re.compile(
    r"(?:custom(?:before|after)|import(?:before|after)?|msbuildprojectextensionspath|"
    r"restoreadditionalprojectsources|restoreignorefailedsources|restorepackagespath)",
    re.IGNORECASE,
)


class PackagePolicyError(RuntimeError):
    """Raised when package authorization or final bytes are not exact."""


def load_policy(path: Path = DEFAULT_POLICY) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise PackagePolicyError(f"unable to load package policy: {exc}") from exc
    if not isinstance(payload, dict) or set(payload) != POLICY_KEYS:
        raise PackagePolicyError("package policy has an invalid exact shape")
    if (
        payload["contract"] != "chummer.media.contract-package-policy/v1"
        or payload["packageId"] != "Chummer.Media.Contracts"
        or payload["project"]
        != "src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj"
        or not isinstance(payload["authorized"], bool)
        or not isinstance(payload["reason"], str)
        or not payload["reason"].strip()
    ):
        raise PackagePolicyError("package policy authority fields are invalid")
    license_policy = payload["approvedLicense"]
    if payload["authorized"]:
        if (
            not isinstance(license_policy, dict)
            or set(license_policy) != LICENSE_KEYS
            or license_policy.get("kind") not in {"expression", "file"}
            or not isinstance(license_policy.get("value"), str)
            or not license_policy["value"].strip()
        ):
            raise PackagePolicyError(
                "authorized package policy requires one exact approved license"
            )
    elif license_policy is not None:
        raise PackagePolicyError("blocked package policy cannot carry a latent license")
    return payload


def reject_unsafe_arguments(arguments: list[str]) -> None:
    for argument in arguments:
        lowered = argument.lower()
        if (
            argument.startswith("@")
            or lowered in {"--no-restore", "--no-build"}
            or FORBIDDEN_ARGUMENT.search(argument)
            or lowered.startswith(("-p:", "/p:", "--property:"))
            or lowered.endswith((".rsp", ".targets", ".props"))
        ):
            raise PackagePolicyError(
                "package lane rejects response files, restore bypasses, imports, "
                f"and MSBuild property injection: {argument}"
            )
    if arguments:
        raise PackagePolicyError(
            f"package lane accepts no caller-controlled build arguments: {arguments}"
        )


def assert_zero_package_bytes(output: Path) -> None:
    if output.exists():
        for entry in output.iterdir():
            if entry.is_file() and entry.name.lower().endswith((".nupkg", ".snupkg")):
                raise PackagePolicyError(
                    f"blocked package lane must contain zero package bytes: {entry}"
                )


def validate_final_nupkg(path: Path, policy: dict[str, Any]) -> None:
    """Validate final archive metadata instead of trusting evaluated properties."""
    if not policy["authorized"]:
        raise PackagePolicyError("package publication is not authorized")
    if path.is_symlink() or not path.is_file() or path.suffix.lower() != ".nupkg":
        raise PackagePolicyError("final package must be one regular nupkg")
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise PackagePolicyError("final package contains duplicate entries")
        nuspecs = [name for name in names if name.lower().endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise PackagePolicyError("final package must contain one nuspec")
        try:
            nuspec = ET.fromstring(archive.read(nuspecs[0]))
        except (KeyError, ET.ParseError) as exc:
            raise PackagePolicyError("final package nuspec is invalid") from exc
        namespace = {"n": "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"}
        metadata = nuspec.find("n:metadata", namespace)
        if (
            metadata is None
            or metadata.findtext("n:id", default="", namespaces=namespace)
            != policy["packageId"]
        ):
            raise PackagePolicyError("final package id diverges from policy")
        license_node = metadata.find("n:license", namespace)
        license_policy = policy["approvedLicense"]
        assert isinstance(license_policy, dict)
        if (
            license_node is None
            or license_node.attrib.get("type") != license_policy["kind"]
            or (license_node.text or "") != license_policy["value"]
        ):
            raise PackagePolicyError("final package license diverges from policy")
        if license_policy["kind"] == "file" and license_policy["value"] not in names:
            raise PackagePolicyError("approved package license file is absent")


def parse_args() -> tuple[argparse.Namespace, list[str]]:
    parser = argparse.ArgumentParser()
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_known_args()


def main() -> int:
    args, extra = parse_args()
    reject_unsafe_arguments(extra)
    assert_zero_package_bytes(args.output)
    policy = load_policy(args.policy)
    if not policy["authorized"]:
        raise PackagePolicyError(
            f"package publication is blocked by external policy: {policy['reason']}"
        )
    raise PackagePolicyError(
        "authorized package assembly is unavailable until an approved license policy lands"
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, PackagePolicyError) as exc:
        print(f"media contracts package policy: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
