#!/usr/bin/env python3
"""`dotnet list package --vulnerable --format json` çıktısını okur.

Bulgu varsa satır satır yazar ve 1 ile çıkar. Metin çıktısı yerine JSON
okunuyor: komut bulgu bulduğunda da 0 ile çıkıyor ve insan okunur çıktısı
yerelleştirilmiş (docs/SECURITY.md §15).
"""

import json
import sys


def main() -> int:
    report = json.load(sys.stdin)
    found = 0

    for project in report.get("projects", []):
        name = project.get("path", "?").split("/")[-1]

        for framework in project.get("frameworks", []):
            for group in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(group, []):
                    for vulnerability in package.get("vulnerabilities", []):
                        found += 1
                        print(
                            f"  {name}: {package['id']} {package.get('resolvedVersion', '')}"
                            f" — {vulnerability.get('severity')} {vulnerability.get('advisoryurl')}"
                        )

    if found == 0:
        print("  Açık bulunamadı.")

    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
