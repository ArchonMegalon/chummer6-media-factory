from __future__ import annotations

import base64
import importlib.util
import json
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
            {"contract", "dotnetSdkVersion", "feedDirectory", "packages"}, set(lock)
        )
        self.assertEqual("10.0.103", lock["dotnetSdkVersion"])
        self.assertEqual(1, len(lock["packages"]))
        package = lock["packages"][0]
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

    def test_nuget_configuration_allows_only_the_exact_local_package_feed(self) -> None:
        config = ET.parse(NUGET_CONFIG).getroot()
        sources = config.findall("./packageSources/add")
        self.assertEqual(1, len(sources))
        self.assertEqual(".tmp/package-feed", sources[0].attrib["value"])
        mappings = config.findall("./packageSourceMapping/packageSource")
        self.assertEqual(1, len(mappings))
        patterns = mappings[0].findall("./package")
        self.assertEqual(["Chummer.Hub.Registry.Contracts"], [p.attrib["pattern"] for p in patterns])

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
