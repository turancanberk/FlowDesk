#!/usr/bin/env bash
# Bulut senkronizasyonu (iCloud Drive, Dropbox) çakışma kopyalarını temizler.
#
# Depo senkronize edilen bir dizinde tutulduğunda "Program 2.cs" gibi kopyalar
# oluşuyor. Bu kopyalar .gitignore ile yok sayılıyor ama derlemeyi bozuyorlar:
# aynı sınıf iki kez tanımlanmış oluyor ve `dotnet run` birden fazla proje
# dosyası görüyor.
#
# Kullanım: ./scripts/clean-sync-duplicates.sh
set -euo pipefail

cd "$(dirname "$0")/.."

duplicates=()
while IFS= read -r file; do
  duplicates+=("$file")
done < <(
  find . \( -name "* [0-9].*" -o -name "*conflicted copy*" \) \
    -not -path "*/node_modules/*" \
    -not -path "*/bin/*" \
    -not -path "*/obj/*" \
    -not -path "./.git/*" \
    -not -path "*/.next/*"
)

if [ ${#duplicates[@]} -eq 0 ]; then
  echo "Çakışma kopyası bulunamadı."
  exit 0
fi

printf 'Silinecek %d dosya:\n' "${#duplicates[@]}"
printf '  %s\n' "${duplicates[@]}"

for file in "${duplicates[@]}"; do
  rm -f "$file"
done

echo "Temizlendi."
