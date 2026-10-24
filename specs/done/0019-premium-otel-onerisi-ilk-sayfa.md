# Spec 0019 — Premium otel önerisi PDF tasarımı

- Status: Complete; R-10 and R-11 implemented and verified
- Mode: lite
- Plan: `specs/plans/0019-plan.md`

## Intent
Rezervasyon ekibi, PDF'nin ilk sayfasında önerilen oteli ve uygun alternatifi hızlıca anlayabilmeli; belge kurumsal ve yazdırmaya uygun görünmelidir. İlk sayfa etkinlik, talep sahibi, Türkiye saatinde belge zamanı, otel önerileri ve kısa sonuç özeti arasında net bir görsel sıra kurar. PDF'nin ikinci sayfası çalışma ağacındaki mevcut haliyle kalır. Öneri ve hesaplama verileri değişmez.

## Requirements
- İlk sayfa A4 dikey düzeninde, sade ve okunaklı bir başlık alanı, ince divider ve kompakt metadata sunar.
- Otel önerileri mevcut rapor modelinden dinamik olarak gösterilir; ulaşım öncelikli ve bütçe öncelikli seçenekler ilk bakışta ayırt edilir.
- Her otel kartı tutarlı, açık zeminli, ince kenarlıklı ve dengeli aralıklı olur; otel adı, strateji, belirgin yaklaşık gecelik fiyat, varsa mesafe/sürüş/yürüme metrikleri ve kısa gerekçe yer alır.
- Bulunmayan ve isteğe bağlı ulaşım metrikleri boş hücre veya teknik sentinel olarak gösterilmez; mevcut formatter'ın kullanıcıya dönük eksik değerleri kullanılır veya alan gizlenir.
- Kartlardan sonra kısa bir öneri özeti ve mevcut yaklaşık fiyat disclaimer'ı görünür.
- Belge üretim zamanı Türkiye yerel saatinde, Türkçe okunur tarih biçimi ve TSİ etiketiyle gösterilir. Türkçe karakterleri destekleyen mevcut gömülü font korunur.
- Sayfa 1 footer'ı mevcut sayfa numaralandırmasıyla uyumlu kalır.
- PDF'nin 2. sayfası bu çalışmanın başlangıcındaki çalışma ağacı görünümüyle aynı kalır: mevcut rapor header'ı ve UTC metadata'sı, harita ve başlıkları/legend'i, otel tablosu ve kolonları, sıralama/değerleri, fontlar, renkler, spacing, atıf ve footer dahil hiçbir görünür öğe değişmez.
- PDF rapor modeli, API/JSON, veri üretimi, sıralama, seçim, puanlama, fiyat/mesafe/süre hesapları ve harita üretimi değişmez.

## Constraints & out of scope
- Yalnızca PDF presentation/layout değişebilir; Domain, Application ve veri/iş kuralı davranışı kapsam dışıdır.
- Çalışma ağacındaki mevcut değişiklikler korunur; 0009 dosyalarındaki kullanıcı değişiklikleri ezilmez veya geri alınmaz.
- İkinci sayfa için ortak bileşen, stil veya yerleşim değişikliği yapılmaz. Birinci sayfa farklı footer gerektirirse değişiklik sayfa bazında yalıtılır ve ikinci sayfanın görünümü doğrulanır.
- Yeni bağımlılık veya ikon paketi eklenmez; mevcut vektör ikonlar ve Türkçe destekli gömülü font kullanılabilir.
- 2/3/5 otel, uzun otel adı ve uzun fiyat/mesafe gösterimlerinde sayfa dışına taşma olmamalıdır; gösterilecek öneri kartları mevcut rapor seçeneklerinden gelir.

## Acceptance criteria
- [ ] AC-1 — İlk sayfa başlık, etkinlik adı, talep sahibi ve TSİ biçiminde üretim zamanını kolay taranır hiyerarşide gösterir; Türkçe karakterler doğrudur.
- [ ] AC-2 — Ulaşım ve bütçe öncelikli otel kartları mevcut rapor verilerini gösterir; fiyat belirgindir, yaklaşık olduğu açıklanır, metrik ve gerekçeler eksik veri sentinel'ı veya taşma üretmez.
- [ ] AC-3 — Öneri özeti kısa ve kartlarla tutarlıdır; disclaimer görünür, sayfa numarası doğrudur.
- [ ] AC-4 — 2/3/5 otel, uzun adlar, farklı fiyat ve mesafe uzunlukları ile eksik yürüyüş verisi ilk sayfa düzenini bozmaz.
- [ ] AC-5 — İkinci sayfanın başlangıçtaki çalışma ağacı görünümü değişmemiştir; harita, tablo, tipografi, renkler, aralıklar, atıf, footer ve sayfa numarası vizüel olarak eşleşir.
- [ ] AC-6 — PDF verisi ve öneri hesaplamaları değişmez; mevcut PDF/JSON eşitliği korunur.

## Definition of Done
- [ ] Her kabul kriteri test veya tekrar edilebilir gözlemle kanıtlanır.
- [ ] `scripts/check` yeşildir veya ortam engeli ve doğrudan eşdeğer doğrulama kanıtı kaydedilir.
- [ ] Bağımsız inceleme tamamlanır; gerçek bulgular giderilir.
- [ ] Dokümanlar/ADR'ler yalnızca mimari veya davranış değişirse güncellenir.
- [ ] Spec `specs/done/` konumuna taşınır.

## Proposed amendment R-08 — Antetli arka plan ve ikinci sayfa uyumu

- Status: Superseded by the user on 2026-10-24; letterhead requirement withdrawn
- This approved amendment supersedes the earlier requirement and AC-5 that page 2 remain visually unchanged. The user now requests a page 2 visual refresh that matches page 1, while keeping the map structure intact.

### Requirements
- `assets/a4.png` is used as a full-page A4 background on every PDF page, including any continuation pages.
- Foreground content remains readable and does not overlap the letterhead logo or bottom contact/footer artwork. Page numbering remains visible in a clear area.
- Page 2 is visually modernized to match page 1's typography and restrained corporate colors.
- Page 2 map image, map dimensions/aspect ratio, map/panel order and structure, marker positions, legend content and attribution are preserved.
- Table column count/order, headings, row ordering, values, and footer text remain unchanged; only presentation styling may be refreshed.
- Report data, calculations, selection/ranking, JSON/API behavior, and map generation are unchanged.

### Constraints and acceptance criteria
- The letterhead file is embedded in the Infrastructure assembly so PDF generation does not depend on the process working directory.
- [ ] AC-7 — Every generated page uses the supplied A4 letterhead background, while foreground text avoids the logo and contact/footer artwork and remains printable.
- [ ] AC-8 — Page 2 matches page 1's visual style while retaining the existing map geometry/layout and table structure/data.
- [ ] AC-9 — A deterministic UTC fixture verifies the exact Türkiye-local timestamp, including a UTC date-boundary conversion.


## Proposed amendment R-09 ? Full PDF structure redesign without letterhead

- Status: Superseded by the new reference supplied on 2026-10-24
- The user now requests a complete PDF structure redesign and withdraws the `assets/a4.png` letterhead. This proposal supersedes the R-08 letterhead requirement and the earlier page-one/page-two-only scope.

### Recommended direction

Keep the PDF at two A4 portrait pages and retain the existing report information and data. Redesign both pages as a coordinated, plain-white corporate report using dark navy/gray text and restrained orange accents:

1. **Page 1 ? Recommendation brief:** compact event/request metadata, the transport-priority and budget-priority hotel cards, nightly approximate prices, available distance/transport metrics, one-sentence reasons, short recommendation summary, disclaimer, and page number.
2. **Page 2 ? Location and comparison:** modernized map and legend above/beside the ranked evaluated-hotels table, preserving map content and report row values/order. Keep the map, attribution, table fields, disclaimer, and page number legible for print.

No letterhead image or logo artwork will be used. Existing report data, ranking, pricing, route metrics, API behavior, and map generation remain outside the redesign.

### Decision required (R-10)

- **Option A ? Recommended:** retain two pages and all current report sections/data, redesign their layout and visual hierarchy. This preserves the report contract and keeps the PDF easy to scan and print.
- **Option B:** change page count, section set, or the order/content of the map and hotel table. This allows a larger structural departure but needs explicit decisions about which sections move, disappear, or gain pages.

### Proposed acceptance criteria if Option A is selected

- [ ] AC-10 ? Both pages use a coordinated modern white A4 layout without the letterhead image.
- [ ] AC-11 ? Page 1 clearly prioritizes the recommended and budget options, their prices, available metrics, short reasons, summary, and disclaimer.
- [ ] AC-12 ? Page 2 presents the map/legend and ranked comparison table in a readable layout while preserving all existing map/table data and ordering.
- [ ] AC-13 ? Turkish text, long names/prices, missing metrics, pagination, page numbers, and print bounds are verified.
- [ ] AC-14 ? Ranking, calculations, backend data, API response, and map generation remain unchanged.



## Approved amendment R-10 — Reference-based full report redesign

- Status: Implemented and verified; R-10 review finding rejected as noise by user
- The user approved retaining the two-page report, adopting the supplied visual direction, and using only real available data. The letterhead background is withdrawn.

### Data mapping and constraints

- Hotel names and nightly prices are dynamic report data and remain unchanged.
- Road distance/duration and optional walking metrics are rendered only from the existing report model. Missing optional walking data is omitted or shown as “Hesaplanamadı” when a requested route could not be calculated.
- No rating, amenity, comfort/service score, AI score, or other unsupported hotel fact is fabricated or displayed.
- Existing recommendation strategy/explanation supplies recommendation labels and concise reasons.
- Ranking, prices, route calculations, map generation, API response, table values/order, and report data contracts do not change.

### Approved visual structure

1. Page 1 presents a compact dark navy header with report title, venue, requester metadata, and Turkey-local generation time; recommendation cards emphasize hotel name, approximate nightly price, available route metrics, and existing reason text. A short summary and price disclaimer close the page.
2. Page 2 uses the same visual system for the map, legend, ranked hotel comparison table, attribution, and page number. Preserve map image content/aspect ratio and table columns, values, and row order.
3. Use a plain white A4 background, navy text/panels, light-blue card fills, subtle borders, and restrained orange accents. Do not use `assets/a4.png` as a background.

### Acceptance criteria

- [x] AC-15 — Both pages use a coordinated reference-style A4 layout without the letterhead background.
- [x] AC-16 — Hotel name and nightly price match report data; approximate-price disclaimer remains visible.
- [x] AC-17 — Route and walking metrics are rendered only from available report data; unsupported rating, amenity, and score claims are absent.
- [x] AC-18 — Recommendation labels/reasons use existing strategy and explanation data without adding unsupported hotel facts.
- [x] AC-19 — Map, legend, ranked table values/order, API data, ranking, and calculations remain unchanged.
- [x] AC-20 — Turkish text, page numbering, long names/prices, missing optional data, and print bounds are verified.
### Verification evidence

- Two-page preview: `specs/evidence/0019-reference-layout-preview.pdf`, `0019-reference-layout-page1.png`, `0019-reference-layout-page2.png`.
- Dynamic report previews: `specs/evidence/0019-reference-3-hotels.pdf`, `0019-reference-5-hotels.pdf`, and `0019-reference-20-hotels.pdf` with page PNGs.
- Build and full solution tests passed via `scripts/check` run with Git Bash. Independent review triage is recorded in `specs/plans/0019-plan.md`.
## Proposed amendment R-11 — Add the supplied logo

- Status: Implemented and independently reviewed
- The user requests `assets/logo.svg` as the PDF logo.

### Recommendation

Place the supplied white SVG logo at the upper-right of the existing navy report header on pages where the report header appears. Preserve its aspect ratio and original artwork. Embed the SVG in the Infrastructure assembly and load it from the assembly resource so rendering does not depend on the current working directory. Keep the approved R-10 layout, page structure, report data, map/table, and calculations unchanged.

### Acceptance criteria

- [x] AC-21 — The supplied logo is visible in the upper-right of the navy report header on the standard report pages, with its aspect ratio preserved.
- [x] AC-22 — The SVG is loaded from an embedded assembly resource; the supplied `assets/logo.svg` source is not modified.
- [x] AC-23 — PDF generation succeeds and page layout, content, and page numbering remain readable and unchanged apart from the logo.

The user approved the R-11 delta plan on 2026-10-24.
### R-11 verification evidence

- Logo preview: `specs/evidence/0019-logo-preview.pdf` and page PNGs.
- Dynamic previews: `specs/evidence/0019-logo-5-hotels.pdf` and `0019-logo-20-hotels.pdf` with page PNGs.
- The original `assets/logo.svg` SHA-256 remains `C55BFAA54A831C5938A77C7D250ECE778C6F3736F253E82AD88836834DEA848E`.
- `scripts/check` passed after the logo integration: build succeeded with 0 warnings/errors; 106 Unit, 49 Provider, 5 Architecture, and 57 Integration tests passed; 4 existing Mongo tests skipped.
- Logo display box is 90 x 24.8 pt, exactly matching the SVG viewBox ratio 450:124; the final 2-page render was visually checked at PDF raster output.
