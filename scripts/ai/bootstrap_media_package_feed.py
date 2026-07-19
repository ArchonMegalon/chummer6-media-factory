#!/usr/bin/env python3
"""Build the one Media Factory compatibility package from its exact owner commit."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import re
import stat
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from contextlib import contextmanager
from pathlib import Path, PurePosixPath
from typing import Any, Mapping


ROOT = Path(__file__).resolve().parents[2]
LOCK_PATH = ROOT / "eng/package-plane.lock.json"
BOOTSTRAP_NUGET_CONFIG = ROOT / "eng/NuGet.RegistryBootstrap.Config"
LOCK_KEYS = {
    "contract",
    "dotnetSdkVersion",
    "dotnetRuntimeVersion",
    "dotnetArchive",
    "toolchainSha256",
    "buildInputs",
    "feedDirectory",
    "packages",
}
DOTNET_ARCHIVE_KEYS = {"url", "sha256", "operatingSystem", "architecture"}
TOOLCHAIN_KEYS = {"dotnetHost", "csc", "msbuild", "nugetPackaging"}
PACKAGE_KEYS = {
    "repository",
    "repositoryUrl",
    "commit",
    "checkoutDirectory",
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
CORE_PROPERTIES_PREFIX = "package/services/metadata/core-properties/"
NORMALIZED_LAST_MODIFIED_BY = "Chummer deterministic package plane/v2"
CANONICAL_ZIP_TIMESTAMP = (1980, 1, 1, 0, 0, 0)
CANONICAL_ZIP_EXTERNAL_ATTR = 0o100644 << 16
BUILD_RECIPE_PATH = "scripts/ai/bootstrap_media_package_feed.py"
BOOTSTRAP_NUGET_CONFIG_PATH = "eng/NuGet.RegistryBootstrap.Config"
DOTNET_ARCHIVE_URL = (
    "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.103/"
    "dotnet-sdk-10.0.103-linux-x64.tar.gz"
)
DOTNET_ARCHIVE_SHA256 = "84dc1f3150ec2800fa38efdbe4d65a855026d68f745c3fe06f522e87e993af0f"
BUILD_INPUT_PATHS = {BUILD_RECIPE_PATH, BOOTSTRAP_NUGET_CONFIG_PATH}
EXPECTED_TOOLCHAIN_SHA256 = {
    "dotnetHost": "bff05e5f15646f8b7bb72d1ba8ea1d60db348f17f848962f49840b58276f6c6d",
    "csc": "9a4237515874153817a8bf4a9c889cafed5148e2d77b04f5dd925c903d3161dc",
    "msbuild": "cc96c3846ae171984d29ba572ef6f22d273c675cd745c59dc7225fd5cd69610b",
    "nugetPackaging": "980fd0205cf99d02e52664ea82c678dcd32232b59d96e277b774d4743e7496b1",
}
OWNER_REPOSITORY = "ArchonMegalon/chummer6-hub-registry"
OWNER_REPOSITORY_URL = "https://github.com/ArchonMegalon/chummer6-hub-registry.git"
OWNER_CHECKOUT_DIRECTORY = "chummer-hub-registry"


class PackagePlaneError(RuntimeError):
    """Raised when source or package bytes diverge from the package-plane lock."""


def digest_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def digest_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_lock() -> tuple[dict[str, Any], dict[str, Any]]:
    payload = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    if not isinstance(payload, dict) or set(payload) != LOCK_KEYS:
        raise PackagePlaneError("package-plane lock has an invalid top-level shape")
    if payload["contract"] != "chummer.media.package-plane-lock/v3":
        raise PackagePlaneError("package-plane lock contract is invalid")
    if payload["dotnetSdkVersion"] != "10.0.103":
        raise PackagePlaneError("package-plane SDK version is invalid")
    if payload["dotnetRuntimeVersion"] != "10.0.3":
        raise PackagePlaneError("package-plane runtime version is invalid")
    archive = payload["dotnetArchive"]
    if (
        not isinstance(archive, dict)
        or set(archive) != DOTNET_ARCHIVE_KEYS
        or archive.get("url") != DOTNET_ARCHIVE_URL
        or archive.get("sha256") != DOTNET_ARCHIVE_SHA256
        or archive.get("operatingSystem") != "linux"
        or archive.get("architecture") != "x64"
    ):
        raise PackagePlaneError("package-plane SDK archive authority is invalid")
    toolchain = payload["toolchainSha256"]
    if (
        not isinstance(toolchain, dict)
        or set(toolchain) != TOOLCHAIN_KEYS
        or any(SHA256.fullmatch(str(value)) is None for value in toolchain.values())
        or toolchain != EXPECTED_TOOLCHAIN_SHA256
    ):
        raise PackagePlaneError("package-plane toolchain authority is invalid")
    build_inputs = payload["buildInputs"]
    if (
        not isinstance(build_inputs, dict)
        or set(build_inputs) != BUILD_INPUT_PATHS
        or any(SHA256.fullmatch(str(value)) is None for value in build_inputs.values())
    ):
        raise PackagePlaneError("package-plane build recipe authority is invalid")
    if payload["feedDirectory"] != ".tmp/package-feed":
        raise PackagePlaneError("package-plane feed directory is invalid")
    packages = payload["packages"]
    if not isinstance(packages, list) or len(packages) != 1:
        raise PackagePlaneError("package-plane lock must contain exactly one owner package")
    package = packages[0]
    if not isinstance(package, dict) or set(package) != PACKAGE_KEYS:
        raise PackagePlaneError("package-plane package row has an invalid shape")
    if package["repository"] != OWNER_REPOSITORY:
        raise PackagePlaneError("package-plane owner repository is invalid")
    if package["repositoryUrl"] != OWNER_REPOSITORY_URL:
        raise PackagePlaneError("package-plane owner URL is invalid")
    if package["checkoutDirectory"] != OWNER_CHECKOUT_DIRECTORY:
        raise PackagePlaneError("package-plane checkout directory is invalid")
    expected_package_values = {
        "project": "Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj",
        "packageId": "Chummer.Hub.Registry.Contracts",
        "version": "0.0.0-packageplane.20260718.2",
        "nupkgName": "Chummer.Hub.Registry.Contracts.0.0.0-packageplane.20260718.2.nupkg",
        "assemblyPath": "lib/net10.0/Chummer.Hub.Registry.Contracts.dll",
        "licensePath": "LICENSE",
        "licenseSha256": "2ecaed15e0f77335d19138e3a98b82779714a4483c45d356a75053f9d33de0e4",
    }
    if any(package.get(key) != value for key, value in expected_package_values.items()):
        raise PackagePlaneError("package-plane owner package authority is invalid")
    if SHA40.fullmatch(str(package["commit"])) is None:
        raise PackagePlaneError("package-plane owner commit is invalid")
    for key in ("assemblySha256", "licenseSha256", "normalizedNupkgSha256"):
        if SHA256.fullmatch(str(package[key])) is None:
            raise PackagePlaneError(f"package-plane {key} is invalid")
    if SHA512.fullmatch(str(package["normalizedNupkgSha512"])) is None:
        raise PackagePlaneError("package-plane normalizedNupkgSha512 is invalid")
    return payload, package


def lexical_absolute(path: Path) -> Path:
    if ".." in path.parts:
        raise PackagePlaneError(f"lexical paths cannot contain parent traversal: {path}")
    value = path if path.is_absolute() else Path.cwd() / path
    return Path(os.path.abspath(os.fspath(value)))


def _is_reparse_point(value: os.stat_result) -> bool:
    attributes = getattr(value, "st_file_attributes", 0)
    marker = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return bool(marker and attributes & marker)


def require_no_link_components(path: Path, *, final_kind: str) -> Path:
    absolute = lexical_absolute(path)
    current = Path(absolute.anchor)
    for index, component in enumerate(absolute.parts[1:], start=1):
        current /= component
        try:
            metadata = current.lstat()
        except OSError as exc:
            raise PackagePlaneError(f"required path component is unavailable: {current}") from exc
        if stat.S_ISLNK(metadata.st_mode) or _is_reparse_point(metadata):
            raise PackagePlaneError(f"symlink/reparse path component is forbidden: {current}")
        final = index == len(absolute.parts) - 1
        if final and final_kind == "file" and not stat.S_ISREG(metadata.st_mode):
            raise PackagePlaneError(f"required path is not a regular file: {current}")
        if final and final_kind == "directory" and not stat.S_ISDIR(metadata.st_mode):
            raise PackagePlaneError(f"required path is not a directory: {current}")
        if not final and not stat.S_ISDIR(metadata.st_mode):
            raise PackagePlaneError(f"path parent is not a directory: {current}")
    return absolute


def resolve_dotnet(dotnet: Path, _base: Mapping[str, str]) -> Path:
    if not dotnet.is_absolute():
        raise PackagePlaneError("dotnet host must be an explicit absolute path")
    absolute = require_no_link_components(dotnet, final_kind="file")
    if absolute.name != "dotnet":
        raise PackagePlaneError(f"dotnet host has an invalid name: {absolute}")
    return absolute


def _canonical_tar_name(name: str) -> str:
    while name.startswith("./"):
        name = name[2:]
    if name in {"", "."}:
        return ""
    path = PurePosixPath(name)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise PackagePlaneError(f"SDK archive contains unsafe path: {name}")
    return path.as_posix()


def authenticate_dotnet_archive_and_tree(
    lock: dict[str, Any], archive_path: Path, dotnet_root: Path
) -> dict[str, Any]:
    observed_system = platform.system().lower()
    observed_architecture = platform.machine().lower()
    if (
        observed_system != lock["dotnetArchive"]["operatingSystem"]
        or observed_architecture not in {"x86_64", "amd64"}
        or lock["dotnetArchive"]["architecture"] != "x64"
    ):
        raise PackagePlaneError(
            "official SDK archive platform/architecture does not match this host"
        )
    archive_path = require_no_link_components(archive_path, final_kind="file")
    dotnet_root = require_no_link_components(dotnet_root, final_kind="directory")
    if dotnet_root.lstat().st_mode & 0o222:
        raise PackagePlaneError(
            "authenticated private SDK root must be read-only before execution"
        )
    archive_digest = digest_file(archive_path)
    if archive_digest != lock["dotnetArchive"]["sha256"]:
        raise PackagePlaneError(".NET SDK archive bytes diverge from the official pin")

    expected: dict[str, tuple[str, int, str]] = {}
    try:
        with tarfile.open(archive_path, "r:gz") as archive:
            for member in archive.getmembers():
                name = _canonical_tar_name(member.name)
                if not name:
                    continue
                if name in expected:
                    raise PackagePlaneError(f"SDK archive contains duplicate entry: {name}")
                if member.isdir():
                    expected[name] = ("directory", 0, "")
                    continue
                if not member.isreg():
                    raise PackagePlaneError(
                        f"SDK archive contains a link or special entry: {name}"
                    )
                stream = archive.extractfile(member)
                if stream is None:
                    raise PackagePlaneError(f"SDK archive entry is unreadable: {name}")
                content = stream.read()
                if len(content) != member.size:
                    raise PackagePlaneError(f"SDK archive entry is truncated: {name}")
                expected[name] = ("file", member.size, digest_bytes(content))
    except (OSError, tarfile.TarError) as exc:
        raise PackagePlaneError(f"unable to authenticate .NET SDK archive: {exc}") from exc

    observed: dict[str, tuple[str, int, str]] = {}
    for current_root, directories, files in os.walk(dotnet_root, followlinks=False):
        current_path = Path(current_root)
        relative_root = current_path.relative_to(dotnet_root)
        for name in [*directories, *files]:
            candidate = current_path / name
            relative = (relative_root / name).as_posix()
            metadata = candidate.lstat()
            if stat.S_ISLNK(metadata.st_mode) or _is_reparse_point(metadata):
                raise PackagePlaneError(
                    f"private SDK contains a symlink/reparse entry: {relative}"
                )
            if metadata.st_mode & 0o222:
                raise PackagePlaneError(
                    f"authenticated private SDK entry must be read-only: {relative}"
                )
            if stat.S_ISDIR(metadata.st_mode):
                observed[relative] = ("directory", 0, "")
            elif stat.S_ISREG(metadata.st_mode):
                observed[relative] = (
                    "file",
                    metadata.st_size,
                    digest_file(candidate),
                )
            else:
                raise PackagePlaneError(
                    f"private SDK contains a non-regular entry: {relative}"
                )
    if set(observed) != set(expected):
        missing = sorted(set(expected) - set(observed))[:10]
        extras = sorted(set(observed) - set(expected))[:10]
        raise PackagePlaneError(
            f"private SDK inventory diverges from authenticated archive "
            f"(missing={missing}, extras={extras})"
        )
    for name, expected_row in expected.items():
        if observed[name] != expected_row:
            raise PackagePlaneError(
                f"private SDK entry diverges from authenticated archive: {name}"
            )
    return {
        "archiveSha256": archive_digest,
        "inventorySha256": digest_bytes(
            json.dumps(expected, separators=(",", ":"), sort_keys=True).encode("utf-8")
        ),
        "inventoryEntryCount": len(expected),
    }


@contextmanager
def open_lexical_directory(path: Path, *, create: bool):
    """Open every lexical component with no-follow semantics and return a dir fd."""
    if ".." in path.parts:
        raise PackagePlaneError(f"feed path cannot contain parent traversal: {path}")
    absolute = lexical_absolute(path)
    flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_CLOEXEC", 0)
    no_follow = getattr(os, "O_NOFOLLOW", 0)
    descriptor = os.open(absolute.anchor, flags)
    try:
        for component in absolute.parts[1:]:
            try:
                metadata = os.stat(component, dir_fd=descriptor, follow_symlinks=False)
            except FileNotFoundError:
                if not create:
                    raise PackagePlaneError(
                        f"lexical directory component is absent: {absolute}"
                    )
                os.mkdir(component, mode=0o700, dir_fd=descriptor)
                metadata = os.stat(component, dir_fd=descriptor, follow_symlinks=False)
            if (
                stat.S_ISLNK(metadata.st_mode)
                or _is_reparse_point(metadata)
                or not stat.S_ISDIR(metadata.st_mode)
            ):
                raise PackagePlaneError(
                    f"feed parent is a symlink, reparse point, or non-directory: {component}"
                )
            next_descriptor = os.open(component, flags | no_follow, dir_fd=descriptor)
            os.close(descriptor)
            descriptor = next_descriptor
        yield descriptor, absolute
    finally:
        os.close(descriptor)


def validate_feed_entries(descriptor: int, allowed: set[str]) -> set[str]:
    names = set(os.listdir(descriptor))
    unexpected = names - allowed
    if unexpected:
        raise PackagePlaneError(
            f"package feed contains ungoverned entries: {sorted(unexpected)}"
        )
    for name in names:
        metadata = os.stat(name, dir_fd=descriptor, follow_symlinks=False)
        if (
            stat.S_ISLNK(metadata.st_mode)
            or _is_reparse_point(metadata)
            or not stat.S_ISREG(metadata.st_mode)
        ):
            raise PackagePlaneError(
                f"package feed entry is not a regular non-link file: {name}"
            )
    return names


def replace_regular_file(descriptor: int, name: str, content: bytes) -> None:
    if "/" in name or "\\" in name or name in {"", ".", ".."}:
        raise PackagePlaneError(f"unsafe package feed filename: {name}")
    temporary_name = f".{name}.new"
    flags = (
        os.O_WRONLY
        | os.O_CREAT
        | os.O_EXCL
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_NOFOLLOW", 0)
    )
    try:
        file_descriptor = os.open(temporary_name, flags, 0o600, dir_fd=descriptor)
    except FileExistsError as exc:
        raise PackagePlaneError(
            f"package feed contains an ungoverned temporary entry: {temporary_name}"
        ) from exc
    try:
        with os.fdopen(file_descriptor, "wb", closefd=True) as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        metadata = os.stat(temporary_name, dir_fd=descriptor, follow_symlinks=False)
        if not stat.S_ISREG(metadata.st_mode) or _is_reparse_point(metadata):
            raise PackagePlaneError(f"temporary feed entry is not regular: {temporary_name}")
        os.replace(
            temporary_name,
            name,
            src_dir_fd=descriptor,
            dst_dir_fd=descriptor,
        )
        metadata = os.stat(name, dir_fd=descriptor, follow_symlinks=False)
        if not stat.S_ISREG(metadata.st_mode) or _is_reparse_point(metadata):
            raise PackagePlaneError(f"final feed entry is not regular: {name}")
        os.fsync(descriptor)
    except Exception:
        try:
            os.unlink(temporary_name, dir_fd=descriptor)
        except FileNotFoundError:
            pass
        raise


def clean_environment(
    root: Path,
    dotnet_root: Path,
    base: Mapping[str, str] | None = None,
) -> dict[str, str]:
    source = os.environ if base is None else base
    inherited = {
        "PATH",
        "SSL_CERT_FILE",
        "SSL_CERT_DIR",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "NO_PROXY",
        "http_proxy",
        "https_proxy",
        "no_proxy",
    }
    environment = {key: value for key, value in source.items() if key in inherited}
    temporary_root = root / "tmp"
    temporary_root.mkdir(parents=True, exist_ok=True)
    environment.update(
        {
            "GIT_TERMINAL_PROMPT": "0",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_SYSTEM": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "DOTNET_ROOT": str(dotnet_root),
            "DOTNET_CLI_HOME": str(root / "dotnet-home"),
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "DOTNET_NOLOGO": "1",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_ROLL_FORWARD": "LatestPatch",
            "DOTNET_ROLL_FORWARD_TO_PRERELEASE": "0",
            "NUGET_PACKAGES": str(root / "nuget-packages"),
            "NUGET_HTTP_CACHE_PATH": str(root / "nuget-http-cache"),
            "RestorePackagesPath": str(root / "nuget-packages"),
            "CI": "true",
            "LANG": "C.UTF-8",
            "LC_ALL": "C.UTF-8",
            "TZ": "UTC",
            "TMPDIR": str(temporary_root),
            "SOURCE_DATE_EPOCH": "0",
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


def validate_build_inputs(lock: dict[str, Any]) -> dict[str, str]:
    observed: dict[str, str] = {}
    for relative_path in sorted(BUILD_INPUT_PATHS):
        candidate = require_no_link_components(ROOT / relative_path, final_kind="file")
        digest = digest_file(candidate)
        if digest != lock["buildInputs"][relative_path]:
            raise PackagePlaneError(
                f"package build input does not match the authority lock: {relative_path}"
            )
        observed[relative_path] = digest
    return observed


def _dotnet_host_version(dotnet_info: str) -> str:
    in_host = False
    for line in dotnet_info.splitlines():
        if line.strip() == "Host:":
            in_host = True
            continue
        if in_host and line.strip().startswith("Version:"):
            return line.split(":", 1)[1].strip()
        if in_host and line and not line[0].isspace():
            break
    return ""


def validate_authenticated_dotnet_identity(
    lock: dict[str, Any],
    dotnet: Path,
    dotnet_root: Path,
    *,
    environment: dict[str, str],
) -> dict[str, str]:
    if dotnet.parent != dotnet_root:
        raise PackagePlaneError("dotnet host must live in the private SDK root")
    # This is deliberately the first execution of dotnet. The complete archive
    # and extracted tree were authenticated before this function is called.
    version = run([str(dotnet), "--version"], cwd=ROOT, environment=environment)
    if version != lock["dotnetSdkVersion"]:
        raise PackagePlaneError(
            f"exact .NET SDK {lock['dotnetSdkVersion']} is required; found {version}"
        )
    info = run([str(dotnet), "--info"], cwd=ROOT, environment=environment)
    host_version = _dotnet_host_version(info)
    if host_version != lock["dotnetRuntimeVersion"]:
        raise PackagePlaneError(
            "exact private .NET host/runtime is required "
            f"(expected {lock['dotnetRuntimeVersion']}, found {host_version or 'unknown'})"
        )
    expected_sdk_root = dotnet_root / "sdk" / lock["dotnetSdkVersion"]
    sdk_rows: list[Path] = []
    for line in run(
        [str(dotnet), "--list-sdks"], cwd=ROOT, environment=environment
    ).splitlines():
        version_text, separator, location = line.partition(" [")
        if (
            version_text == lock["dotnetSdkVersion"]
            and separator
            and location.endswith("]")
        ):
            sdk_rows.append(lexical_absolute(Path(location[:-1]) / version_text))
    if sdk_rows != [expected_sdk_root]:
        raise PackagePlaneError("private SDK root does not contain the one exact SDK")
    runtime_root = dotnet_root / "shared/Microsoft.NETCore.App"
    runtime_versions = (
        {entry.name for entry in runtime_root.iterdir() if entry.is_dir()}
        if runtime_root.is_dir()
        else set()
    )
    if runtime_versions != {lock["dotnetRuntimeVersion"]}:
        raise PackagePlaneError("private SDK root contains runtime version drift")
    files = {
        "dotnetHost": dotnet,
        "csc": expected_sdk_root / "Roslyn/bincore/csc.dll",
        "msbuild": expected_sdk_root / "Microsoft.Build.dll",
        "nugetPackaging": expected_sdk_root / "NuGet.Packaging.dll",
    }
    for path in files.values():
        require_no_link_components(path, final_kind="file")
    observed = {key: digest_file(path) for key, path in files.items()}
    if observed != lock["toolchainSha256"]:
        raise PackagePlaneError("private SDK toolchain bytes diverge from the lock")
    return observed


def package_build_properties(
    package: dict[str, Any], source: Path, package_root: Path
) -> list[str]:
    normalized_source_root = f"/_/src/{package['checkoutDirectory']}"
    return [
        f"-p:PackageVersion={package['version']}",
        f"-p:Version={package['version']}",
        f"-p:RepositoryCommit={package['commit']}",
        f"-p:SourceRevisionId={package['commit']}",
        f"-p:RepositoryUrl={package['repositoryUrl']}",
        "-p:RepositoryBranch=",
        "-p:PublishRepositoryUrl=true",
        "-p:ContinuousIntegrationBuild=true",
        "-p:Deterministic=true",
        "-p:DeterministicSourcePaths=true",
        "-p:EmbedUntrackedSources=false",
        f"-p:PathMap={source.resolve()}={normalized_source_root}",
        "-p:UseSharedCompilation=false",
        f"-p:RestorePackagesPath={package_root}",
    ]


def normalize_core_properties(
    value: bytes, package: dict[str, Any] | None = None
) -> bytes:
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
    observed_values = [str(child.text or "").strip() for child in root]
    if package is None:
        canonical_values = [*observed_values[:-1], NORMALIZED_LAST_MODIFIED_BY]
    else:
        canonical_values = [
            package["packageId"],
            "Chummer deterministic package-plane artifact",
            package["packageId"],
            package["version"],
            "",
            NORMALIZED_LAST_MODIFIED_BY,
        ]
    ET.register_namespace("", CORE_PROPERTIES_NS)
    ET.register_namespace("dc", DC_NS)
    canonical = ET.Element(f"{{{CORE_PROPERTIES_NS}}}coreProperties")
    for tag, text in zip(expected_tags, canonical_values, strict=True):
        ET.SubElement(canonical, tag).text = text
    ET.indent(canonical, space="  ")
    return ET.tostring(canonical, encoding="utf-8", xml_declaration=True)


def canonical_relationships(nuspec_name: str, core_path: str) -> bytes:
    ET.register_namespace("", RELATIONSHIPS_NS)
    root = ET.Element(f"{{{RELATIONSHIPS_NS}}}Relationships")
    relationships = (
        (MANIFEST_RELATIONSHIP, f"/{nuspec_name}"),
        (CORE_RELATIONSHIP, f"/{core_path}"),
    )
    for relationship_type, target in sorted(relationships):
        identifier = "R" + hashlib.sha256(
            f"{relationship_type}\n{target}".encode("utf-8")
        ).hexdigest()[:16].upper()
        ET.SubElement(
            root,
            f"{{{RELATIONSHIPS_NS}}}Relationship",
            {"Type": relationship_type, "Target": target, "Id": identifier},
        )
    ET.indent(root, space="  ")
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def normalize_nupkg(
    source: Path, destination: Path, package: dict[str, Any] | None = None
) -> None:
    with zipfile.ZipFile(source, "r") as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise PackagePlaneError("owner package contains duplicate entries")
        entries = {name: archive.read(name) for name in names}
    core_paths = [
        name
        for name in entries
        if name.startswith(CORE_PROPERTIES_PREFIX)
        and name.endswith(".psmdcp")
    ]
    nuspec_paths = [name for name in entries if name.lower().endswith(".nuspec")]
    if len(core_paths) != 1 or len(nuspec_paths) != 1 or "_rels/.rels" not in entries:
        raise PackagePlaneError("owner package has an invalid core-properties inventory")
    original_core_path = core_paths[0]
    core_bytes = entries.pop(original_core_path)
    canonical_core = normalize_core_properties(core_bytes, package)
    normalized_core_path = (
        CORE_PROPERTIES_PREFIX + digest_bytes(canonical_core)[:32] + ".psmdcp"
    )
    entries[normalized_core_path] = canonical_core
    try:
        relationships = ET.fromstring(entries["_rels/.rels"])
    except ET.ParseError as exc:
        raise PackagePlaneError("owner package relationships are invalid") from exc
    relationship_types = {
        relationship.attrib.get("Type", "") for relationship in relationships
    }
    if relationship_types != {MANIFEST_RELATIONSHIP, CORE_RELATIONSHIP}:
        raise PackagePlaneError("owner package relationship set is incomplete")
    entries["_rels/.rels"] = canonical_relationships(
        nuspec_paths[0], normalized_core_path
    )
    destination.parent.mkdir(parents=True, exist_ok=True)
    # Store canonical entries without DEFLATE. Compressed bytes can vary across
    # Python/zlib builds even when every input byte and ZIP header is identical.
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_STORED) as archive:
        archive.comment = b""
        for name in sorted(entries):
            if (
                name.startswith("/")
                or "\\" in name
                or any(segment in {"", ".", ".."} for segment in name.split("/"))
            ):
                raise PackagePlaneError(f"owner package contains unsafe entry {name}")
            info = zipfile.ZipInfo(name, date_time=CANONICAL_ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_STORED
            info.create_system = 3
            info.external_attr = CANONICAL_ZIP_EXTERNAL_ATTR
            info.extra = b""
            info.comment = b""
            archive.writestr(info, entries[name], compress_type=zipfile.ZIP_STORED)


def validate_nupkg(path: Path, package: dict[str, Any]) -> None:
    actual_digest = digest_file(path)
    actual_sha512 = hashlib.sha512(path.read_bytes()).hexdigest()
    with zipfile.ZipFile(path, "r") as archive:
        ordered_names = archive.namelist()
        names = set(ordered_names)
        core_paths = [
            name
            for name in ordered_names
            if name.startswith(CORE_PROPERTIES_PREFIX) and name.endswith(".psmdcp")
        ]
        if len(core_paths) != 1:
            raise PackagePlaneError("owner package must contain one core-properties part")
        core_path = core_paths[0]
        expected_names = {
            "_rels/.rels",
            f"{package['packageId']}.nuspec",
            package["assemblyPath"],
            "PACKAGE_README.md",
            package["licensePath"],
            "[Content_Types].xml",
            core_path,
        }
        if names != expected_names:
            raise PackagePlaneError("owner package contains an unexpected file inventory")
        if ordered_names != sorted(ordered_names) or archive.comment:
            raise PackagePlaneError("owner package archive layout is not canonical")
        for info in archive.infolist():
            if (
                info.date_time != CANONICAL_ZIP_TIMESTAMP
                or info.compress_type != zipfile.ZIP_STORED
                or info.create_system != 3
                or info.external_attr != CANONICAL_ZIP_EXTERNAL_ATTR
                or info.extra
                or info.comment
            ):
                raise PackagePlaneError("owner package ZIP metadata is not canonical")
        entry_digests = {
            name: digest_bytes(archive.read(name)) for name in sorted(names)
        }
        actual_assembly_sha256 = digest_bytes(archive.read(package["assemblyPath"]))
        if digest_bytes(archive.read(package["licensePath"])) != package["licenseSha256"]:
            raise PackagePlaneError("owner package license bytes diverge from the lock")
        core_bytes = archive.read(core_path)
        expected_core_path = (
            CORE_PROPERTIES_PREFIX + digest_bytes(core_bytes)[:32] + ".psmdcp"
        )
        if core_path != expected_core_path:
            raise PackagePlaneError("owner package core-properties path is not content-addressed")
        if core_bytes != normalize_core_properties(core_bytes, package):
            raise PackagePlaneError("owner package core properties are not canonical")
        if archive.read("_rels/.rels") != canonical_relationships(
            f"{package['packageId']}.nuspec", core_path
        ):
            raise PackagePlaneError("owner package relationships are not canonical")
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
    observed = {
        "assemblySha256": actual_assembly_sha256,
        "normalizedNupkgSha256": actual_digest,
        "normalizedNupkgSha512": actual_sha512,
    }
    expected = {
        "assemblySha256": package["assemblySha256"],
        "normalizedNupkgSha256": package["normalizedNupkgSha256"],
        "normalizedNupkgSha512": package["normalizedNupkgSha512"],
    }
    if observed != expected:
        raise PackagePlaneError(
            "owner package or assembly digests diverge from the lock "
            f"(expected {json.dumps(expected, sort_keys=True)}, "
            f"observed {json.dumps(observed, sort_keys=True)}); "
            f"entry SHA-256 values: {json.dumps(entry_digests, sort_keys=True)}"
        )


def bootstrap(*, dotnet: Path, dotnet_archive: Path, feed: Path) -> dict[str, Any]:
    lock, package = load_lock()
    observed_build_inputs = validate_build_inputs(lock)
    dotnet_host = resolve_dotnet(dotnet, os.environ)
    dotnet_root = dotnet_host.parent
    authenticated_sdk = authenticate_dotnet_archive_and_tree(
        lock, dotnet_archive, dotnet_root
    )
    with tempfile.TemporaryDirectory(prefix="chummer-media-package-plane-") as temporary:
        temporary_root = Path(temporary)
        environment = clean_environment(temporary_root, dotnet_root)
        observed_toolchain = validate_authenticated_dotnet_identity(
            lock,
            dotnet_host,
            dotnet_root,
            environment=environment,
        )
        source = (
            temporary_root / "sources" / package["checkoutDirectory"]
        )
        source.parent.mkdir(parents=True, exist_ok=True)
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
        if not project.is_file():
            raise PackagePlaneError("owner package project is missing")
        package_root = temporary_root / "nuget-packages"
        common_properties = package_build_properties(package, source, package_root)
        run(
            [
                str(dotnet_host),
                "restore",
                str(project),
                "--configfile",
                str(BOOTSTRAP_NUGET_CONFIG),
                "--packages",
                str(package_root),
                "--no-cache",
                "--nologo",
                "--verbosity",
                "quiet",
                "-m:1",
                *common_properties,
            ],
            cwd=source,
            environment=environment,
        )
        raw_output = temporary_root / "raw"
        run(
            [
                str(dotnet_host),
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
                "-m:1",
                *common_properties,
            ],
            cwd=source,
            environment=environment,
        )
        raw_packages = list(raw_output.glob("*.nupkg"))
        if len(raw_packages) != 1 or raw_packages[0].name != package["nupkgName"]:
            raise PackagePlaneError("owner pack did not emit the one exact locked package")
        normalized = temporary_root / package["nupkgName"]
        normalize_nupkg(raw_packages[0], normalized, package)
        validate_nupkg(normalized, package)
        if run(
            ["git", "status", "--porcelain", "--untracked-files=all"],
            cwd=source,
            environment=environment,
        ):
            raise PackagePlaneError("owner checkout is dirty after package production")
        if (
            authenticate_dotnet_archive_and_tree(lock, dotnet_archive, dotnet_root)
            != authenticated_sdk
        ):
            raise PackagePlaneError("authenticated SDK identity changed during package production")
        inventory = {
            "contract": "chummer.media.package-feed-inventory/v3",
            "sourceRepository": package["repository"],
            "sourceCommit": package["commit"],
            "dotnetSdkVersion": lock["dotnetSdkVersion"],
            "dotnetRuntimeVersion": lock["dotnetRuntimeVersion"],
            "dotnetArchive": lock["dotnetArchive"],
            "authenticatedSdk": authenticated_sdk,
            "toolchainSha256": observed_toolchain,
            "buildInputsSha256": observed_build_inputs,
            "packageId": package["packageId"],
            "packageVersion": package["version"],
            "packageSha256": package["normalizedNupkgSha256"],
            "packageSha512": package["normalizedNupkgSha512"],
            "assemblySha256": package["assemblySha256"],
        }
        expected_feed_names = {package["nupkgName"], "feed-inventory.json"}
        with open_lexical_directory(feed, create=True) as (feed_descriptor, _):
            validate_feed_entries(feed_descriptor, expected_feed_names)
            replace_regular_file(
                feed_descriptor, package["nupkgName"], normalized.read_bytes()
            )
            replace_regular_file(
                feed_descriptor,
                "feed-inventory.json",
                (json.dumps(inventory, indent=2, sort_keys=True) + "\n").encode(
                    "utf-8"
                ),
            )
            if validate_feed_entries(feed_descriptor, expected_feed_names) != expected_feed_names:
                raise PackagePlaneError(
                    "package feed does not contain the exact governed entry set"
                )
        return inventory


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", type=Path, required=True)
    parser.add_argument("--dotnet-archive", type=Path, required=True)
    parser.add_argument("--feed", type=Path, default=ROOT / ".tmp/package-feed")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    inventory = bootstrap(
        dotnet=args.dotnet,
        dotnet_archive=args.dotnet_archive,
        feed=args.feed,
    )
    print(json.dumps(inventory, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError, PackagePlaneError) as exc:
        raise SystemExit(f"media package plane: {exc}") from exc
