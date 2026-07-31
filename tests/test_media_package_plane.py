from __future__ import annotations

import base64
import hashlib
import importlib.util
import json
import os
import tarfile
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / "eng/package-plane.lock.json"
RUNTIME_PROJECT = (
    ROOT / "src/Chummer.Media.Factory.Runtime/Chummer.Media.Factory.Runtime.csproj"
)
PACKAGE_LOCK = ROOT / "src/Chummer.Media.Factory.Runtime/packages.lock.json"
NUGET_CONFIG = ROOT / "NuGet.Config"
BOOTSTRAP = ROOT / "scripts/ai/bootstrap_media_package_feed.py"
WORKFLOW = ROOT / ".github/workflows/pr-ci.yml"


def load_bootstrap_module():
    spec = importlib.util.spec_from_file_location("media_package_bootstrap_test", BOOTSTRAP)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load package bootstrap")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class MediaPackagePlaneTests(unittest.TestCase):
    def test_runtime_consumes_the_exact_owner_package_without_sibling_paths(self) -> None:
        lock = json.loads(LOCK.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                "contract",
                "dotnetSdkVersion",
                "dotnetRuntimeVersion",
                "dotnetArchive",
                "toolchainSha256",
                "buildInputs",
                "feedDirectory",
                "packages",
            },
            set(lock),
        )
        self.assertEqual("chummer.media.package-plane-lock/v3", lock["contract"])
        self.assertEqual("10.0.103", lock["dotnetSdkVersion"])
        self.assertEqual("10.0.3", lock["dotnetRuntimeVersion"])
        self.assertEqual(
            {
                "url": "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.103/dotnet-sdk-10.0.103-linux-x64.tar.gz",
                "sha256": "84dc1f3150ec2800fa38efdbe4d65a855026d68f745c3fe06f522e87e993af0f",
                "operatingSystem": "linux",
                "architecture": "x64",
            },
            lock["dotnetArchive"],
        )
        self.assertEqual(1, len(lock["packages"]))
        package = lock["packages"][0]
        self.assertEqual("chummer-hub-registry", package["checkoutDirectory"])
        project_text = RUNTIME_PROJECT.read_text(encoding="utf-8")
        self.assertNotIn("chummercomplete", project_text)
        self.assertNotIn("chummer.run-services", project_text)
        self.assertNotIn("chummer-hub-registry", project_text)
        project = ET.parse(RUNTIME_PROJECT).getroot()
        references = project.findall(".//PackageReference")
        self.assertEqual(1, len(references))
        self.assertEqual(package["packageId"], references[0].attrib["Include"])
        self.assertEqual(package["version"], references[0].attrib["Version"])
        self.assertEqual(
            "true", project.findtext(".//RestorePackagesWithLockFile")
        )
        self.assertEqual("true", project.findtext(".//RestoreLockedMode"))

    def test_build_recipe_and_private_toolchain_are_authority_locked(self) -> None:
        lock = json.loads(LOCK.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                "scripts/ai/bootstrap_media_package_feed.py",
                "eng/NuGet.RegistryBootstrap.Config",
            },
            set(lock["buildInputs"]),
        )
        for path, expected in lock["buildInputs"].items():
            self.assertEqual(
                hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), expected
            )
        self.assertEqual(
            {
                "dotnetHost": "bff05e5f15646f8b7bb72d1ba8ea1d60db348f17f848962f49840b58276f6c6d",
                "csc": "9a4237515874153817a8bf4a9c889cafed5148e2d77b04f5dd925c903d3161dc",
                "msbuild": "cc96c3846ae171984d29ba572ef6f22d273c675cd745c59dc7225fd5cd69610b",
                "nugetPackaging": "980fd0205cf99d02e52664ea82c678dcd32232b59d96e277b774d4743e7496b1",
            },
            lock["toolchainSha256"],
        )

    def test_build_environment_rejects_ambient_ci_and_msbuild_poisoning(self) -> None:
        module = load_bootstrap_module()
        poisoned = {
            "PATH": os.environ.get("PATH", ""),
            "DOTNET_ROOT": "/ambient/dotnet",
            "NUGET_PACKAGES": "/ambient/packages",
            "DOTNET_CLI_HOME": "/ambient/home",
            "RestorePackagesPath": "/ambient/restore",
            "RestoreAdditionalProjectSources": "/ambient/feed",
            "GITHUB_ACTIONS": "true",
            "GITHUB_SHA": "f" * 40,
            "GITHUB_WORKSPACE": "/ambient/workspace",
            "MSBuildSDKsPath": "/ambient/msbuild",
            "SourceRevisionId": "e" * 40,
            "RepositoryCommit": "d" * 40,
            "CI": "false",
            "SOURCE_DATE_EPOCH": "1234567890",
        }
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            private_dotnet = temp_root / "private-dotnet"
            result = module.clean_environment(
                temp_root / "build", private_dotnet, poisoned
            )
            self.assertEqual(str(private_dotnet), result["DOTNET_ROOT"])
            self.assertEqual("true", result["CI"])
            self.assertEqual("0", result["SOURCE_DATE_EPOCH"])
            self.assertEqual("0", result["DOTNET_MULTILEVEL_LOOKUP"])
            self.assertEqual("UTC", result["TZ"])
            self.assertFalse(
                any(str(value).startswith("/ambient") for value in result.values())
            )
            for key in (
                "GITHUB_ACTIONS",
                "GITHUB_SHA",
                "GITHUB_WORKSPACE",
                "MSBuildSDKsPath",
                "SourceRevisionId",
                "RepositoryCommit",
                "RestoreAdditionalProjectSources",
            ):
                self.assertNotIn(key, result)

    def test_owner_build_properties_pin_source_authority_and_path_map(self) -> None:
        module = load_bootstrap_module()
        package = json.loads(LOCK.read_text(encoding="utf-8"))["packages"][0]
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            source = temp_root / "machine-path" / package["checkoutDirectory"]
            properties = module.package_build_properties(
                package, source, temp_root / "packages"
            )
        self.assertIn(f"-p:RepositoryCommit={package['commit']}", properties)
        self.assertIn(f"-p:SourceRevisionId={package['commit']}", properties)
        self.assertIn(f"-p:RepositoryUrl={package['repositoryUrl']}", properties)
        self.assertIn("-p:RepositoryBranch=", properties)
        self.assertIn("-p:ContinuousIntegrationBuild=true", properties)
        self.assertIn("-p:Deterministic=true", properties)
        self.assertIn("-p:DeterministicSourcePaths=true", properties)
        self.assertIn("-p:EmbedUntrackedSources=false", properties)
        self.assertIn("-p:UseSharedCompilation=false", properties)
        self.assertIn(
            f"-p:PathMap={source.resolve()}=/_/src/{package['checkoutDirectory']}",
            properties,
        )

    def test_ci_installs_only_the_digest_locked_private_sdk(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        self.assertNotIn("actions/setup-dotnet", workflow)
        self.assertNotIn("dotnet-install.sh", workflow)
        self.assertIn(
            "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.103/dotnet-sdk-10.0.103-linux-x64.tar.gz",
            workflow,
        )
        self.assertIn(
            "84dc1f3150ec2800fa38efdbe4d65a855026d68f745c3fe06f522e87e993af0f",
            workflow,
        )
        self.assertIn("DOTNET_ROOT=${media_dotnet_root}", workflow)
        self.assertIn("CHUMMER_DOTNET_ARCHIVE=${media_dotnet_archive}", workflow)

    def test_complete_sdk_tree_is_authenticated_before_execution_and_rejects_extras(self) -> None:
        module = load_bootstrap_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            sdk = root / "sdk-root"
            (sdk / "host/fxr/10.0.3").mkdir(parents=True)
            (sdk / "sdk/10.0.103/Roslyn/bincore").mkdir(parents=True)
            (sdk / "packs/Microsoft.NETCore.App.Ref/10.0.3/ref/net10.0").mkdir(
                parents=True
            )
            (sdk / "dotnet").write_bytes(b"host")
            (sdk / "host/fxr/10.0.3/libhostfxr.so").write_bytes(b"runtime")
            (sdk / "sdk/10.0.103/Roslyn/bincore/csc.dll").write_bytes(b"compiler")
            (sdk / "packs/Microsoft.NETCore.App.Ref/10.0.3/ref/net10.0/System.dll").write_bytes(
                b"reference"
            )
            archive = root / "sdk.tar.gz"
            with tarfile.open(archive, "w:gz") as stream:
                for entry in sorted(sdk.rglob("*")):
                    stream.add(
                        entry,
                        arcname=f"./{entry.relative_to(sdk).as_posix()}",
                        recursive=False,
                    )
            for entry in [*sdk.rglob("*"), sdk]:
                if not entry.is_symlink():
                    entry.chmod(entry.stat().st_mode & ~0o222)
            lock = {
                "dotnetArchive": {
                    "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
                    "operatingSystem": "linux",
                    "architecture": "x64",
                }
            }
            receipt = module.authenticate_dotnet_archive_and_tree(lock, archive, sdk)
            self.assertEqual(lock["dotnetArchive"]["sha256"], receipt["archiveSha256"])
            extra = sdk / "sdk/10.0.103/attacker.targets"
            extra.parent.chmod(extra.parent.stat().st_mode | 0o200)
            extra.write_text("poison", encoding="utf-8")
            extra.chmod(extra.stat().st_mode & ~0o222)
            extra.parent.chmod(extra.parent.stat().st_mode & ~0o222)
            with self.assertRaisesRegex(module.PackagePlaneError, "extras"):
                module.authenticate_dotnet_archive_and_tree(lock, archive, sdk)
            extra.parent.chmod(extra.parent.stat().st_mode | 0o200)
            extra.unlink()
            extra.parent.chmod(extra.parent.stat().st_mode & ~0o222)
            target = sdk / "sdk/10.0.103/Roslyn/bincore/csc.dll"
            target.parent.chmod(target.parent.stat().st_mode | 0o200)
            target.unlink()
            target.symlink_to(sdk / "dotnet")
            target.parent.chmod(target.parent.stat().st_mode & ~0o222)
            with self.assertRaisesRegex(module.PackagePlaneError, "symlink/reparse"):
                module.authenticate_dotnet_archive_and_tree(lock, archive, sdk)
            for entry in [sdk, *sdk.rglob("*")]:
                if entry.is_dir() and not entry.is_symlink():
                    entry.chmod(entry.stat().st_mode | 0o700)

    def test_feed_path_and_entries_reject_symlinks_before_writes(self) -> None:
        module = load_bootstrap_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            real_parent = root / "real"
            real_parent.mkdir()
            linked_parent = root / "linked"
            linked_parent.symlink_to(real_parent, target_is_directory=True)
            with self.assertRaisesRegex(module.PackagePlaneError, "symlink"):
                with module.open_lexical_directory(linked_parent / "feed", create=True):
                    pass
            self.assertFalse((real_parent / "feed").exists())

            feed = root / "feed"
            feed.mkdir()
            (feed / "feed-inventory.json").symlink_to(root / "outside")
            with module.open_lexical_directory(feed, create=False) as (descriptor, _):
                with self.assertRaisesRegex(module.PackagePlaneError, "not a regular"):
                    module.validate_feed_entries(descriptor, {"feed-inventory.json"})

    def test_lockfile_content_hash_binds_the_normalized_owner_package(self) -> None:
        lock = json.loads(LOCK.read_text(encoding="utf-8"))
        package = lock["packages"][0]
        package_lock = json.loads(PACKAGE_LOCK.read_text(encoding="utf-8"))
        dependencies = package_lock["dependencies"]
        self.assertEqual({"net10.0"}, set(dependencies))
        rows = dependencies["net10.0"]
        self.assertEqual(
            {package["packageId"], "chummer.media.contracts"}, set(rows)
        )
        package_row = rows[package["packageId"]]
        self.assertEqual("Direct", package_row["type"])
        self.assertEqual(package["version"], package_row["resolved"])
        content_hash = base64.b64decode(package_row["contentHash"], validate=True)
        self.assertEqual(bytes.fromhex(package["normalizedNupkgSha512"]), content_hash)

    def test_nuget_configuration_pins_contracts_locally_and_sdk_packs_officially(self) -> None:
        config = ET.parse(NUGET_CONFIG).getroot()
        sources = config.findall("./packageSources/add")
        self.assertEqual(
            [
                ("chummer-media-package-plane", ".tmp/package-feed"),
                ("nuget.org", "https://api.nuget.org/v3/index.json"),
            ],
            [(source.attrib["key"], source.attrib["value"]) for source in sources],
        )
        mappings = config.findall("./packageSourceMapping/packageSource")
        self.assertEqual(
            {
                "chummer-media-package-plane": ["Chummer.Hub.Registry.Contracts"],
                "nuget.org": ["Microsoft.*"],
            },
            {
                mapping.attrib["key"]: [
                    pattern.attrib["pattern"]
                    for pattern in mapping.findall("./package")
                ]
                for mapping in mappings
            },
        )

    def test_package_normalization_is_independent_of_source_zip_compression(self) -> None:
        module = load_bootstrap_module()
        relationships = b"""<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/package.nuspec" Id="random-manifest" />
  <Relationship Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="/package/services/metadata/core-properties/random.psmdcp" Id="random-core" />
</Relationships>"""
        core_properties = b"""<?xml version="1.0" encoding="utf-8"?>
<coreProperties xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties">
  <dc:creator>owner</dc:creator>
  <dc:description>description</dc:description>
  <dc:identifier>package</dc:identifier>
  <version>1.0.0</version>
  <keywords>contracts</keywords>
  <lastModifiedBy>host-specific value</lastModifiedBy>
</coreProperties>"""
        entries = {
            "_rels/.rels": relationships,
            "package.nuspec": b"package metadata",
            "package/services/metadata/core-properties/random.psmdcp": core_properties,
        }
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            normalized_paths = []
            for compression_level in (1, 9):
                source = temp_root / f"source-{compression_level}.nupkg"
                normalized = temp_root / f"normalized-{compression_level}.nupkg"
                with zipfile.ZipFile(
                    source,
                    "w",
                    compression=zipfile.ZIP_DEFLATED,
                    compresslevel=compression_level,
                ) as archive:
                    for name, content in entries.items():
                        archive.writestr(name, content)
                module.normalize_nupkg(source, normalized)
                normalized_paths.append(normalized)

            self.assertEqual(normalized_paths[0].read_bytes(), normalized_paths[1].read_bytes())
            with zipfile.ZipFile(normalized_paths[0], "r") as archive:
                self.assertTrue(archive.infolist())
                self.assertTrue(
                    all(info.compress_type == zipfile.ZIP_STORED for info in archive.infolist())
                )
                self.assertTrue(
                    all(info.date_time == (1980, 1, 1, 0, 0, 0) for info in archive.infolist())
                )


if __name__ == "__main__":
    unittest.main()
