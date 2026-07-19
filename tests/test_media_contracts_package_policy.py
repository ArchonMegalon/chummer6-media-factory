from __future__ import annotations

import importlib.util
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/ai/verify_media_contracts_package_policy.py"
PROJECT = ROOT / "src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj"


def load_module():
    spec = importlib.util.spec_from_file_location("media_contract_package_policy", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class MediaContractsPackagePolicyTests(unittest.TestCase):
    def test_msbuild_cannot_authorize_package_license_metadata(self) -> None:
        project = PROJECT.read_text(encoding="utf-8-sig")
        self.assertIn("<IsPackable>false</IsPackable>", project)
        self.assertIn("RejectDirectMediaContractsPackageProduction", project)
        for attacker_controlled_property in (
            "TreatAsLocalProperty",
            "ChummerMediaPackagePublishing",
            "ChummerMediaApprovedPackageLicenseExpression",
            "<PackageLicenseExpression",
        ):
            self.assertNotIn(attacker_controlled_property, project)

    def test_every_msbuild_injection_vector_fails_with_zero_package_bytes(self) -> None:
        vectors = (
            ["--no-restore"],
            ["@attacker.rsp"],
            ["-p:CustomBeforeMicrosoftCommonTargets=attacker.targets"],
            ["-p:CustomAfterMicrosoftCommonTargets=attacker.targets"],
            ["-p:ImportBefore=attacker.props"],
            ["-p:ImportAfter=attacker.targets"],
            ["-p:IsPackable=true", "-p:PackageLicenseExpression=MIT"],
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for index, vector in enumerate(vectors):
                output = root / str(index)
                output.mkdir()
                completed = subprocess.run(
                    [sys.executable, str(SCRIPT), "--output", str(output), *vector],
                    cwd=ROOT,
                    check=False,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                )
                self.assertNotEqual(0, completed.returncode, vector)
                self.assertFalse(list(output.glob("*.nupkg")), vector)
                self.assertFalse(list(output.glob("*.snupkg")), vector)

    def test_blocked_owner_policy_fails_before_creating_output_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "packages"
            completed = subprocess.run(
                [sys.executable, str(SCRIPT), "--output", str(output)],
                cwd=ROOT,
                check=False,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
            )
            self.assertNotEqual(0, completed.returncode)
            self.assertIn("blocked by external policy", completed.stdout)
            self.assertFalse(output.exists())

    def test_final_nupkg_license_is_validated_from_archive_bytes(self) -> None:
        module = load_module()
        policy = {
            "contract": "chummer.media.contract-package-policy/v1",
            "authorized": True,
            "packageId": "Chummer.Media.Contracts",
            "project": "src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj",
            "approvedLicense": {"kind": "expression", "value": "MIT"},
            "reason": "test-only policy object",
        }
        template = """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata><id>Chummer.Media.Contracts</id><version>1.0.0</version><license type="expression">{license}</license></metadata>
</package>
"""
        with tempfile.TemporaryDirectory() as temporary:
            package = Path(temporary) / "Chummer.Media.Contracts.1.0.0.nupkg"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr(
                    "Chummer.Media.Contracts.nuspec",
                    template.format(license="MIT"),
                )
            module.validate_final_nupkg(package, policy)
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr(
                    "Chummer.Media.Contracts.nuspec",
                    template.format(license="Apache-2.0"),
                )
            with self.assertRaisesRegex(module.PackagePolicyError, "license diverges"):
                module.validate_final_nupkg(package, policy)


if __name__ == "__main__":
    unittest.main()
