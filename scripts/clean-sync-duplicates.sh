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
  find . \( -name "* [0-9].*" -o -name "* [0-9]" -o -name "*conflicted copy*" \) \
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

printf 'Bulunan %d kopya:\n' "${#duplicates[@]}"
printf '  %s\n' "${duplicates[@]}"

# Senkronizasyon klasörleri de kopyalıyor ("app 2"). Boş olanlar silinir; dolu
# bir klasör kendiliğinden silinmez, çünkü içinde henüz asıl yere taşınmamış
# bir değişiklik olabilir.
left_for_review=()

for path in "${duplicates[@]}"; do
  if [ -d "$path" ]; then
    rmdir "$path" 2>/dev/null || left_for_review+=("$path")
  else
    rm -f "$path"
  fi
done

if [ ${#left_for_review[@]} -gt 0 ]; then
  echo "Boş olmayan kopya klasörler elle incelenmeli:"
  printf '  %s\n' "${left_for_review[@]}"
  exit 1
fi

echo "Temizlendi."
