from __future__ import annotations

import base64
import json
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LOCK = ROOT / "eng/package-plane.lock.json"
RUNTIME_PROJECT = (
    ROOT / "src/Chummer.Media.Factory.Runtime/Chummer.Media.Factory.Runtime.csproj"
)
PACKAGE_LOCK = ROOT / "src/Chummer.Media.Factory.Runtime/packages.lock.json"
NUGET_CONFIG = ROOT / "NuGet.Config"


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


if __name__ == "__main__":
    unittest.main()
