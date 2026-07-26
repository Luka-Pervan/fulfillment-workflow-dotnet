import sys
from pathlib import Path
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))

from verify_nuget_audit import verify  # noqa: E402


class VerifyNugetAuditTests(unittest.TestCase):
    def test_clean_multi_project_report_passes(self) -> None:
        vulnerabilities = verify(
            {
                "version": 1,
                "projects": [
                    {"path": "api.csproj"},
                    {
                        "path": "tests.csproj",
                        "frameworks": [
                            {
                                "topLevelPackages": [
                                    {"id": "Example", "resolvedVersion": "1.0.0"}
                                ]
                            }
                        ],
                    },
                ],
            }
        )

        self.assertEqual([], vulnerabilities)

    def test_vulnerability_is_detected_at_nested_package_level(self) -> None:
        vulnerabilities = verify(
            {
                "version": 1,
                "projects": [
                    {
                        "path": "api.csproj",
                        "frameworks": [
                            {
                                "transitivePackages": [
                                    {
                                        "id": "Unsafe",
                                        "vulnerabilities": [
                                            {
                                                "severity": "High",
                                                "advisoryurl": "https://example.invalid",
                                            }
                                        ],
                                    }
                                ]
                            }
                        ],
                    }
                ],
            }
        )

        self.assertEqual(1, len(vulnerabilities))

    def test_unknown_schema_fails_closed(self) -> None:
        with self.assertRaises(ValueError):
            verify({"version": 2, "projects": [{"path": "api.csproj"}]})


if __name__ == "__main__":
    unittest.main()
