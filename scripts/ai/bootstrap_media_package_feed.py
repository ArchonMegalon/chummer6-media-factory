#!/usr/bin/env python3
"""Build the one Media Factory compatibility package from its exact owner commit."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
LOCK_PATH = ROOT / "eng/package-plane.lock.json"
BOOTSTRAP_NUGET_CONFIG = ROOT / "eng/NuGet.RegistryBootstrap.Config"
LOCK_KEYS = {"contract", "dotnetSdkVersion", "feedDirectory", "packages"}
PACKAGE_KEYS = {
    "repository",
    "repositoryUrl",
    "commit",
    "project",
    "packageId",
    "version",
    "nupkgName",
    "assemblyPath",
    "assemblySha256",
    "licensePath",
    "licenseSha256",
    "normalizedNupkgSha256",
    "normalizedNupkgSha512",
}
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
SHA512 = re.compile(r"^[0-9a-f]{128}$")
RELATIONSHIPS_NS = "http://schemas.openxmlformats.org/package/2006/relationships"
CORE_PROPERTIES_NS = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
DC_NS = "http://purl.org/dc/elements/1.1/"
CORE_RELATIONSHIP = (
    "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"
)
MANIFEST_RELATIONSHIP = "http://schemas.microsoft.com/packaging/2010/07/manifest"
NORMALIZED_CORE_PATH = (
    "package/services/metadata/core-properties/registry-contracts.psmdcp"
)
NORMALIZED_LAST_MODIFIED_BY = "Chummer deterministic package plane/v1"


class PackagePlaneError(RuntimeError):
    """Raised when source or package bytes diverge from the package-plane lock."""


def digest_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def digest_file(path: Path) -> str:
    return digest_bytes(path.read_bytes())


def load_lock() -> tuple[dict[str, Any], dict[str, Any]]:
    payload = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    if not isinstance(payload, dict) or set(payload) != LOCK_KEYS:
        raise PackagePlaneError("package-plane lock has an invalid top-level shape")
    if payload["contract"] != "chummer.media.package-plane-lock/v1":
        raise PackagePlaneError("package-plane lock contract is invalid")
    packages = payload["packages"]
    if not isinstance(packages, list) or len(packages) != 1:
        raise PackagePlaneError("package-plane lock must contain exactly one owner package")
    package = packages[0]
    if not isinstance(package, dict) or set(package) != PACKAGE_KEYS:
        raise PackagePlaneError("package-plane package row has an invalid shape")
    if package["repository"] != "ArchonMegalon/chummer6-hub-registry":
        raise PackagePlaneError("package-plane owner repository is invalid")
    if package["repositoryUrl"] != "https://github.com/ArchonMegalon/chummer6-hub-registry.git":
        raise PackagePlaneError("package-plane owner URL is invalid")
    if SHA40.fullmatch(str(package["commit"])) is None:
        raise PackagePlaneError("package-plane owner commit is invalid")
    for key in ("assemblySha256", "licenseSha256", "normalizedNupkgSha256"):
        if SHA256.fullmatch(str(package[key])) is None:
            raise PackagePlaneError(f"package-plane {key} is invalid")
    if SHA512.fullmatch(str(package["normalizedNupkgSha512"])) is None:
        raise PackagePlaneError("package-plane normalizedNupkgSha512 is invalid")
    return payload, package


def clean_environment(root: Path) -> dict[str, str]:
    environment = {
        key: value
        for key, value in os.environ.items()
        if not key.startswith("GIT_")
        and key
        not in {
            "NUGET_PACKAGES",
            "NUGET_HTTP_CACHE_PATH",
            "DOTNET_CLI_HOME",
            "RestorePackagesPath",
            "MSBuildSDKsPath",
        }
    }
    environment.update(
        {
            "GIT_TERMINAL_PROMPT": "0",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_SYSTEM": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "DOTNET_CLI_HOME": str(root / "dotnet-home"),
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "NUGET_PACKAGES": str(root / "nuget-packages"),
            "NUGET_HTTP_CACHE_PATH": str(root / "nuget-http-cache"),
            "RestorePackagesPath": str(root / "nuget-packages"),
        }
    )
    return environment


def run(command: list[str], *, cwd: Path, environment: dict[str, str]) -> str:
    completed = subprocess.run(
        command,
        cwd=cwd,
        env=environment,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    if completed.returncode != 0:
        raise PackagePlaneError(
            f"command failed ({' '.join(command)}):\n{completed.stdout}"
        )
    return completed.stdout.strip()


def normalize_core_properties(value: bytes) -> bytes:
    try:
        root = ET.fromstring(value)
    except ET.ParseError as exc:
        raise PackagePlaneError("owner package core properties are invalid") from exc
    expected_tags = [
        f"{{{DC_NS}}}creator",
        f"{{{DC_NS}}}description",
        f"{{{DC_NS}}}identifier",
        f"{{{CORE_PROPERTIES_NS}}}version",
        f"{{{CORE_PROPERTIES_NS}}}keywords",
        f"{{{CORE_PROPERTIES_NS}}}lastModifiedBy",
    ]
    if root.tag != f"{{{CORE_PROPERTIES_NS}}}coreProperties" or [
        child.tag for child in root
    ] != expected_tags:
        raise PackagePlaneError("owner package core properties have an unexpected shape")
    for child in root:
        child.text = str(child.text or "").strip()
    root[-1].text = NORMALIZED_LAST_MODIFIED_BY
    ET.register_namespace("", CORE_PROPERTIES_NS)
    ET.register_namespace("dc", DC_NS)
    ET.indent(root, space="  ")
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def normalize_nupkg(source: Path, destination: Path) -> None:
    with zipfile.ZipFile(source, "r") as archive:
        entries = {name: archive.read(name) for name in archive.namelist()}
    core_paths = [
        name
        for name in entries
        if name.startswith("package/services/metadata/core-properties/")
        and name.endswith(".psmdcp")
    ]
    if len(core_paths) != 1 or "_rels/.rels" not in entries:
        raise PackagePlaneError("owner package has an invalid core-properties inventory")
    original_core_path = core_paths[0]
    core_bytes = entries.pop(original_core_path)
    entries[NORMALIZED_CORE_PATH] = normalize_core_properties(core_bytes)
    try:
        relationships = ET.fromstring(entries["_rels/.rels"])
    except ET.ParseError as exc:
        raise PackagePlaneError("owner package relationships are invalid") from exc
    relationship_types: set[str] = set()
    for relationship in relationships:
        relationship_type = relationship.attrib.get("Type", "")
        relationship_types.add(relationship_type)
        if relationship_type == MANIFEST_RELATIONSHIP:
            relationship.set("Id", "R_MANIFEST")
        elif relationship_type == CORE_RELATIONSHIP:
            relationship.set("Id", "R_CORE_PROPERTIES")
            relationship.set("Target", f"/{NORMALIZED_CORE_PATH}")
        else:
            raise PackagePlaneError("owner package contains an unexpected relationship")
    if relationship_types != {MANIFEST_RELATIONSHIP, CORE_RELATIONSHIP}:
        raise PackagePlaneError("owner package relationship set is incomplete")
    ET.register_namespace("", RELATIONSHIPS_NS)
    entries["_rels/.rels"] = ET.tostring(
        relationships, encoding="utf-8", xml_declaration=True
    )
    destination.parent.mkdir(parents=True, exist_ok=True)
    # Store canonical entries without DEFLATE. Compressed bytes can vary across
    # Python/zlib builds even when every input byte and ZIP header is identical.
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_STORED) as archive:
        for name in sorted(entries):
            if (
                name.startswith("/")
                or "\\" in name
                or any(segment in {"", ".", ".."} for segment in name.split("/"))
            ):
                raise PackagePlaneError(f"owner package contains unsafe entry {name}")
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_STORED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, entries[name], compress_type=zipfile.ZIP_STORED)


def validate_nupkg(path: Path, package: dict[str, Any]) -> None:
    actual_digest = digest_file(path)
    actual_sha512 = hashlib.sha512(path.read_bytes()).hexdigest()
    with zipfile.ZipFile(path, "r") as archive:
        names = set(archive.namelist())
        expected_names = {
            "_rels/.rels",
            f"{package['packageId']}.nuspec",
            package["assemblyPath"],
            "PACKAGE_README.md",
            package["licensePath"],
            "[Content_Types].xml",
            NORMALIZED_CORE_PATH,
        }
        if names != expected_names:
            raise PackagePlaneError("owner package contains an unexpected file inventory")
        entry_digests = {
            name: digest_bytes(archive.read(name)) for name in sorted(names)
        }
        actual_assembly_sha256 = digest_bytes(archive.read(package["assemblyPath"]))
        if actual_assembly_sha256 != package["assemblySha256"]:
            raise PackagePlaneError(
                "owner contract assembly bytes diverge from the lock "
                f"(expected {package['assemblySha256']}, actual {actual_assembly_sha256})"
            )
        if digest_bytes(archive.read(package["licensePath"])) != package["licenseSha256"]:
            raise PackagePlaneError("owner package license bytes diverge from the lock")
        nuspec = ET.fromstring(archive.read(f"{package['packageId']}.nuspec"))
    namespace = {"n": "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"}
    metadata = nuspec.find("n:metadata", namespace)
    if metadata is None:
        raise PackagePlaneError("owner package nuspec metadata is missing")
    values = {
        child.tag.rsplit("}", 1)[-1]: child.text or ""
        for child in metadata
        if child.tag.rsplit("}", 1)[-1] in {"id", "version", "license", "repository"}
    }
    repository = metadata.find("n:repository", namespace)
    license_node = metadata.find("n:license", namespace)
    if (
        values.get("id") != package["packageId"]
        or values.get("version") != package["version"]
        or license_node is None
        or license_node.attrib.get("type") != "file"
        or (license_node.text or "") != package["licensePath"]
        or repository is None
        or repository.attrib.get("url") != package["repositoryUrl"]
        or repository.attrib.get("commit") != package["commit"]
    ):
        raise PackagePlaneError("owner package nuspec authority metadata diverges from the lock")
    if actual_digest != package["normalizedNupkgSha256"]:
        raise PackagePlaneError(
            "normalized owner package digest diverges from the lock "
            f"(expected {package['normalizedNupkgSha256']}, actual {actual_digest}); "
            f"entry SHA-256 values: {json.dumps(entry_digests, sort_keys=True)}"
        )
    if actual_sha512 != package["normalizedNupkgSha512"]:
        raise PackagePlaneError("normalized owner package SHA-512 diverges from the lock")


def bootstrap(*, dotnet: Path, feed: Path) -> dict[str, Any]:
    lock, package = load_lock()
    with tempfile.TemporaryDirectory(prefix="chummer-media-package-plane-") as temporary:
        temporary_root = Path(temporary)
        environment = clean_environment(temporary_root)
        version = run([str(dotnet), "--version"], cwd=ROOT, environment=environment)
        if version != lock["dotnetSdkVersion"]:
            raise PackagePlaneError(
                f"exact .NET SDK {lock['dotnetSdkVersion']} is required; found {version}"
            )
        source = temporary_root / "owner"
        source.mkdir()
        run(["git", "init", "--quiet"], cwd=source, environment=environment)
        run(
            ["git", "remote", "add", "origin", package["repositoryUrl"]],
            cwd=source,
            environment=environment,
        )
        run(
            ["git", "fetch", "--quiet", "--depth", "1", "origin", package["commit"]],
            cwd=source,
            environment=environment,
        )
        run(["git", "checkout", "--quiet", "--detach", "FETCH_HEAD"], cwd=source, environment=environment)
        if run(["git", "rev-parse", "HEAD"], cwd=source, environment=environment) != package["commit"]:
            raise PackagePlaneError("owner checkout did not resolve the exact locked commit")
        if run(["git", "status", "--porcelain", "--untracked-files=all"], cwd=source, environment=environment):
            raise PackagePlaneError("owner checkout is dirty before package production")
        project = source / package["project"]
        run(
            [
                str(dotnet),
                "restore",
                str(project),
                "--configfile",
                str(BOOTSTRAP_NUGET_CONFIG),
                "--nologo",
                "--verbosity",
                "quiet",
            ],
            cwd=source,
            environment=environment,
        )
        raw_output = temporary_root / "raw"
        run(
            [
                str(dotnet),
                "pack",
                str(project),
                "--no-restore",
                "--configuration",
                "Release",
                "--output",
                str(raw_output),
                "--nologo",
                "--verbosity",
                "quiet",
                "-p:ContinuousIntegrationBuild=true",
            ],
            cwd=source,
            environment=environment,
        )
        raw_packages = list(raw_output.glob("*.nupkg"))
        if len(raw_packages) != 1 or raw_packages[0].name != package["nupkgName"]:
            raise PackagePlaneError("owner pack did not emit the one exact locked package")
        normalized = temporary_root / package["nupkgName"]
        normalize_nupkg(raw_packages[0], normalized)
        validate_nupkg(normalized, package)
        expected_feed_names = {package["nupkgName"], "feed-inventory.json"}
        feed.mkdir(parents=True, exist_ok=True)
        unexpected = {entry.name for entry in feed.iterdir()} - expected_feed_names
        if unexpected:
            raise PackagePlaneError(f"package feed contains ungoverned entries: {sorted(unexpected)}")
        temporary_package = feed / f".{package['nupkgName']}.tmp"
        shutil.copyfile(normalized, temporary_package)
        os.replace(temporary_package, feed / package["nupkgName"])
        inventory = {
            "contract": "chummer.media.package-feed-inventory/v1",
            "sourceRepository": package["repository"],
            "sourceCommit": package["commit"],
            "dotnetSdkVersion": lock["dotnetSdkVersion"],
            "packageId": package["packageId"],
            "packageVersion": package["version"],
            "packageSha256": package["normalizedNupkgSha256"],
            "packageSha512": package["normalizedNupkgSha512"],
            "assemblySha256": package["assemblySha256"],
        }
        inventory_temp = feed / ".feed-inventory.json.tmp"
        inventory_temp.write_text(
            json.dumps(inventory, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        os.replace(inventory_temp, feed / "feed-inventory.json")
        if {entry.name for entry in feed.iterdir()} != expected_feed_names:
            raise PackagePlaneError("package feed does not contain the exact governed entry set")
        return inventory


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", type=Path, default=Path("dotnet"))
    parser.add_argument("--feed", type=Path, default=ROOT / ".tmp/package-feed")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    inventory = bootstrap(dotnet=args.dotnet, feed=args.feed.resolve())
    print(json.dumps(inventory, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError, PackagePlaneError) as exc:
        raise SystemExit(f"media package plane: {exc}") from exc
