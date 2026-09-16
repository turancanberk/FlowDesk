# FlowDesk — Tasarım Sistemi

## Tasarım yönü

FlowDesk bir operasyon aracıdır, bir pazarlama sayfası değil. Kullanıcı gün
boyu bu ekranlara bakar; ekranın işi bilgiyi hızlı ve net taşımaktır.

Ürün şöyle hissettirmelidir: **profesyonel, yoğun, sakin, işlevsel, bilgi
odaklı, kesin.**

Referanslar: etkileşim ve bileşen olgunluğu için **Atlassian Design System**,
bilgi yoğunluğu ve gezinme için **Linear**. Bunlar kopyalanmaz; FlowDesk kendi
sistemini kurar.

## Anti-pattern listesi

Aşağıdakiler "yapay zekâ üretimi genel SaaS paneli" görünümünün imzalarıdır ve
**kullanılmaz**:

- dekoratif gradyan, özellikle mor/mavi geçişler
- glassmorphism, bulanık cam yüzeyler
- parlama (glow) efektleri
- dev dekoratif lekeler (blob)
- aşırı gölge
- her yerde `rounded-xl` / `rounded-2xl`
- her bölümü karta sarmak
- dev KPI kartları
- büyük boş dashboard alanları
- gereğinden büyük başlıklar
- aşırı boşluk
- her başlığın yanına dekoratif ikon koymak
- her rozeti hap (pill) biçiminde yapmak
- her gezinme öğesini büyük yuvarlatılmış dikdörtgen yapmak
- gereksiz animasyon
- operasyonel sayfalarda ortalanmış pazarlama düzeni
- boşluk doldurmak için dekoratif bileşen

Kenarlık, tipografi ve hizalama; gölge ve dekorasyondan daha önemlidir.

---

## Renk

Renk anlam taşır. Dekorasyon için renk kullanılmaz. Vurgu rengi seyrek
kullanılır; her yerde vurgu varsa hiçbir yerde vurgu yoktur.

### Nötr ölçek

Hafif soğuk gri. Yüzeylerin ve metnin tamamı buradan gelir.

| Token | Değer | Kullanım |
|---|---|---|
| `neutral-0` | `#FFFFFF` | Ana yüzey |
| `neutral-25` | `#FCFCFD` | Alternatif satır |
| `neutral-50` | `#F7F8F9` | Uygulama arka planı, tablo başlığı |
| `neutral-100` | `#F1F2F4` | Hover yüzeyi |
| `neutral-200` | `#E4E6EA` | Kenarlık (varsayılan) |
| `neutral-300` | `#D2D6DC` | Kenarlık (güçlü), ayraç |
| `neutral-400` | `#A9B0BA` | Devre dışı metin, yer tutucu |
| `neutral-500` | `#7C8593` | İkincil ikon |
| `neutral-600` | `#5C6675` | İkincil metin |
| `neutral-700` | `#434C59` | Gövde metni |
| `neutral-800` | `#2D343E` | Başlık |
| `neutral-900` | `#1A1F26` | Birincil metin |

### Vurgu — petrol

| Token | Değer | Kullanım |
|---|---|---|
| `accent-50` | `#ECF5F5` | Seçili satır arka planı |
| `accent-100` | `#D3E8E8` | Rozet arka planı |
| `accent-200` | `#A8D1D2` | Kenarlık (vurgulu) |
| `accent-400` | `#45959A` | Odak halkası |
| `accent-500` | `#1F7A80` | **Birincil eylem** |
| `accent-600` | `#16636A` | Birincil eylem hover |
| `accent-700` | `#125156` | Birincil eylem active |
| `accent-800` | `#103F43` | Vurgulu metin |

### Anlamsal renkler

| Anlam | Metin | Arka plan | Kenarlık |
|---|---|---|---|
| Başarı | `#1B6B45` | `#E8F5EE` | `#A9D9C1` |
| Uyarı | `#8A5A00` | `#FDF3E2` | `#EDD09A` |
| Hata | `#A32A2A` | `#FCECEC` | `#EEB4B4` |
| Bilgi | `#1F5AA8` | `#EAF1FB` | `#B3CBEB` |

### Durum renkleri

Talep durumu ve öncelik, tablo içinde hızlı taranabilir olmalıdır.

| Talep durumu | Görünen | Renk ailesi |
|---|---|---|
| `Open` | Açık | Bilgi (mavi) |
| `InProgress` | Devam Ediyor | Vurgu (petrol) |
| `Waiting` | Bekliyor | Uyarı (amber) |
| `Resolved` | Çözüldü | Başarı (yeşil) |
| `Closed` | Kapalı | Nötr |

| Öncelik | Görünen | Renk ailesi |
|---|---|---|
| `Low` | Düşük | Nötr |
| `Medium` | Orta | Bilgi |
| `High` | Yüksek | Uyarı |
| `Urgent` | Acil | Hata |

| Görev durumu | Görünen | Renk ailesi |
|---|---|---|
| `Todo` | Yapılacak | Nötr |
| `InProgress` | Devam Ediyor | Vurgu |
| `Done` | Tamamlandı | Başarı |

Renk tek başına anlam taşımaz; her rozet metin de içerir. Renk körlüğü olan
kullanıcılar için ayrım metinden okunur.

---

## Tipografi

**IBM Plex Sans** arayüzün tamamında kullanılır.

**IBM Plex Mono** yalnızca teknik alanlarda kullanılır: talep numarası
(`TLP-1042`), kayıt kimliği, depolama anahtarı, `traceId`. Dekoratif amaçla
kullanılmaz.

### Ölçek

Kompakt. Başlıklar büyütülerek hiyerarşi kurulmaz; ağırlık ve renk kullanılır.

| Rol | Boyut / satır | Ağırlık | Renk |
|---|---|---|---|
| Sayfa başlığı | 20 / 28 | 600 | `neutral-900` |
| Bölüm başlığı | 16 / 24 | 600 | `neutral-800` |
| Alt başlık | 14 / 20 | 600 | `neutral-800` |
| Gövde | 14 / 20 | 400 | `neutral-700` |
| İkincil | 13 / 18 | 400 | `neutral-600` |
| Etiket | 12 / 16 | 500 | `neutral-600` |
| Yardımcı metin | 12 / 16 | 400 | `neutral-500` |
| Kenar çubuğu grup başlığı | 11 / 16 | 600, harf aralığı 0.04em, büyük harf | `neutral-500` |
| Mono | 12 / 16 | 400 | `neutral-600` |

---

## Boşluk ve yoğunluk

4 px tabanlı ölçek: `4, 8, 12, 16, 20, 24, 32, 40, 48`.

Sayfa iç boşluğu 24 px (mobilde 16 px). Bölümler arası 24 px. Form alanları
arası 16 px. Etiket ile girdi arası 6 px.

Hedef, iş yazılımı yoğunluğudur: ekranda anlamlı miktarda bilgi görünmelidir.
Boşluk bilgiyi ayırmak için kullanılır, ekranı doldurmak için değil.

---

## Köşe yarıçapı

| Bileşen | Yarıçap |
|---|---|
| Buton | 6 px |
| Girdi, select, textarea | 6 px |
| Rozet | 4 px |
| Kart, panel | 8 px |
| Diyalog | 10 px |
| Açılır menü, popover | 8 px |
| Avatar | tam yuvarlak |

Hap (pill) biçimi yalnızca anlamsal olarak uygun olduğunda kullanılır: sayaç
rozeti, filtre çipi. Durum rozetleri hap değildir.

---

## Yükseklikler

| Bileşen | Yükseklik |
|---|---|
| Buton / girdi (küçük) | 28 px |
| Buton / girdi (varsayılan) | 32 px |
| Buton / girdi (büyük) | 36 px |
| Tablo satırı | 44 px |
| Tablo başlığı | 36 px |
| Kenar çubuğu öğesi | 32 px |
| Üst çubuk | 48 px |

---

## Kenarlık ve gölge

Kenarlık birincil ayırıcıdır: `1px solid neutral-200`.

Gölge yalnızca gerçekten üst katmanda duran yüzeyler içindir:

| Token | Kullanım |
|---|---|
| `shadow-overlay` | Açılır menü, popover, tooltip |
| `shadow-dialog` | Diyalog, çekmece (drawer) |

Kart, panel, tablo ve giriş alanlarında gölge kullanılmaz.

---

## Odak

Klavye odağı her zaman görünür olmalıdır.

```
outline: 2px solid accent-400;
outline-offset: 2px;
```

Odak halkası kaldırılmaz. `:focus-visible` kullanılır, böylece fare
tıklamalarında görünmez ama klavye gezintisinde görünür.

---

## Bileşen kuralları

### Buton

Varyantlar: `primary` (petrol dolgu), `secondary` (kenarlıklı, beyaz zemin),
`ghost` (kenarlıksız), `danger` (hata rengi).

Bir ekranda tek bir birincil eylem bulunur. İkiden fazla birincil buton varsa
hiyerarşi bozulmuştur.

### Form

Yapı her zaman aynıdır:

```
Etiket
[Girdi]
Yardımcı metin
Hata mesajı
```

- Dekoratif kayan etiket (floating label) kullanılmaz.
- Yer tutucu (placeholder) etiket yerine geçmez; yalnızca örnek gösterir.
- Zorunlu alanlar işaretlenir.
- Hata mesajı alanın hemen altında, hata renginde ve Türkçe'dir.
- Her girdi `<label>` ile `for`/`id` üzerinden ilişkilendirilir.

### Tablo

Veri tabloları ürünün kimliğidir. TanStack Table kullanılır.

- Satır yüksekliği 44 px.
- Başlık satırı `neutral-50` zeminli, 36 px, 12 px etiket tipografisi.
- Sayısal sütunlar sağa hizalı.
- Satır hover'ında `neutral-50`, seçili satırda `accent-50`.
- Satır eylemleri sağda, satır hover'ında belirginleşir ancak klavye odağıyla
  da erişilebilir olur.
- Sıralama, filtreleme, sayfalama, arama, sütun görünürlüğü ve seçim
  desteklenir.
- Semantik `<table>`, `<thead>`, `<th scope="col">` kullanılır.

### Rozet (durum göstergesi)

Küçük yarıçap, 12 px metin, 20 px yükseklik, ince kenarlık, açık zemin.
Metin her zaman bulunur.

### Kenar çubuğu

```
FLOWDESK
[Çalışma alanı seçici]

GENEL BAKIŞ
  Dashboard

MÜŞTERİ OPERASYONU
  Müşteriler
  Talepler
  Görevler

YÖNETİM
  Ekip
  Etkinlik

Ayarlar
[Kullanıcı kimliği]
```

- Grup başlıkları küçük, büyük harf, harf aralıklı, nötr renkte.
- Gezinme öğeleri 32 px yüksekliğinde, 6 px yarıçaplı, küçük ikonlu.
- Seçili durum **sade**dir: `neutral-100` zemin, `neutral-900` metin ve solda
  2 px petrol şerit. Büyük yuvarlatılmış dolgu blokları kullanılmaz.
- Kullanıcı kimliği en altta.

### Sayfa başlığı

Başlık solda, birincil eylem sağda, altında ince ayraç. Başlığın yanına
dekoratif ikon konmaz.

### Boş durum

Kısa başlık, bir cümlelik açıklama ve tek bir eylem. Büyük illüstrasyon
kullanılmaz. Metin Türkçe ve yönlendiricidir; "Veri yok" gibi kuru ifade
yerine ne yapılabileceği söylenir.

---

## Asenkron durumlar

Her anlamlı asenkron ekran dört durumu ele alır:

| Durum | Davranış |
|---|---|
| Yükleniyor | İçeriğin şeklini taklit eden iskelet (skeleton) |
| Boş | Hiç kayıt yok; yönlendirici boş durum bileşeni |
| Sonuç yok | Kayıt var ama filtre eşleşmedi; filtreyi temizleme önerisi |
| Hata | Türkçe hata mesajı ve yeniden deneme eylemi |

"Boş" ile "sonuç yok" ayrı durumlardır ve ayrı metin gösterirler.

İskelet her yerde kullanılmaz; yalnızca düzen kaymasını önlediği yerlerde.

---

## Animasyon

Süre 100–180 ms, yumuşak `ease-out`.

Kullanılan: hover ve odak geçişleri, açılır menü/diyalog giriş-çıkışı, akordeon
açılması.

Kullanılmayan: zıplama, yay (spring) efekti, sayfa uçuşu, gereksiz ölçek
değişimi, dikkat çekmek için döngüsel animasyon.

`prefers-reduced-motion` tercihine saygı gösterilir.

---

## İkonografi

Lucide. 16 px varsayılan, kenar çubuğunda 16 px, buton içinde 14–16 px.
İkonlar eylemi netleştirmek için kullanılır; başlıkları süslemek için değil.

Yalnızca ikon içeren butonlar `aria-label` taşır ve tooltip ile desteklenir.

---

## Duyarlılık

Masaüstü öncelikli. Mobilde:

- Kenar çubuğu çekmeceye (drawer) dönüşür.
- Tablolarda daha az sütun gösterilir; kalanlar kontrollü yatay kaydırma ile
  erişilir.
- Tüm tablolar kart duvarına çevrilmez; bu, veri tarama yeteneğini bozar.
- Formlar tek sütuna iner.

---

## Erişilebilirlik

Tamamlanma tanımının parçasıdır, sonradan eklenmez.

- Tam klavye gezintisi; tüm etkileşimli öğeler sekme sırasında.
- Görünür odak halkası.
- Semantik HTML; `<div>` üzerine tıklama davranışı kurulmaz.
- Diyalog odak tuzağı ve `Esc` ile kapanma.
- Menülerde ok tuşu gezintisi.
- Her girdi etiketli.
- Metin kontrastı en az WCAG AA (4.5:1); büyük metin 3:1.
- Tablolarda başlık ilişkilendirmesi.
- ARIA yalnızca semantik HTML yetmediğinde.

---

## Uygulama

Token'lar CSS değişkeni olarak `:root` üzerinde tanımlanır ve Tailwind teması
bunlara bağlanır. Rastgele renk değeri (`#hex`) bileşen dosyalarına yazılmaz.

Koyu tema çekirdek kapsam dışındadır (`docs/ROADMAP.md`). Token yapısı ileride
eklenebilecek şekilde kurulur, ancak ikinci bir tema Faz 23'e kadar
uygulanmaz.

`/design-system` sayfası tüm bileşenleri Türkçe içerikle gösterir. İş
sayfaları bu bileşenleri yeniden kullanır; her sayfada yeni bir tasarım icat
edilmez.
