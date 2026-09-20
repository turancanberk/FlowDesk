#!/usr/bin/env bash
# Bağımlılık güvenlik taraması (docs/SECURITY.md §15).
#
# Üç paket ağacını da tarar ve bulgu varsa sıfırdan farklı kodla çıkar;
# böylece elle çalıştırıldığında okunur, CI'da (Faz 20) adım olarak
# kullanılabilir.
#
# Kullanım: ./scripts/security-scan.sh
set -uo pipefail

cd "$(dirname "$0")/.."

failures=()

echo "== Backend (NuGet)"

# JSON, metin değil (bkz. scripts/report-vulnerable-packages.py).
backend_report=$(dotnet list backend/FlowDesk.slnx package --vulnerable --include-transitive --format json 2>/dev/null)

if ! printf '%s' "$backend_report" | python3 scripts/report-vulnerable-packages.py; then
  failures+=("backend")
fi

for project in frontend e2e; do
  echo
  echo "== ${project} (npm)"

  # Yüksek ve üzeri: düşük şiddetli bir geliştirme bağımlılığı uyarısı CI'ı
  # durdurmamalı. Tam liste yine ekrana yazılıyor.
  if ! npm --prefix "$project" audit --audit-level=high; then
    failures+=("$project")
  fi
done

echo
if [ ${#failures[@]} -gt 0 ]; then
  echo "Güvenlik açığı bulundu: ${failures[*]}"
  exit 1
fi

echo "Açık bulunamadı."
