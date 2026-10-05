# Spec 0018 — Etkinlik konumunu doğrula ve harita rotalarını gizle

- Status: Implemented; verified; awaiting ship gate
- Mode: lite
- Plan: `specs/plans/0018-plan.md`

## Intent
Rezervasyon ekibi PDF haritasında etkinlik alanını doğru yerde görmeli ve haritadaki otel-etkinlik çizgileri dikkat dağıtmamalıdır. Etkinlik adı için yanlış bir geocoding sonucu seçilirse sistem bunu sessizce doğru konummuş gibi göstermemelidir. Otel sıralaması, mesafe ve süre bilgileri öneri akışında kullanılmaya devam eder.

## Requirements
- Etkinlik koordinatı, girilen etkinlik alanıyla eşleştiği doğrulanabilen geocoding sonucundan alınır.
- Etkinlik adıyla eşleşen sonuç doğrulanamıyorsa sistem başka bir yeri etkinlik alanı olarak işaretlemez.
- PDF haritasında etkinlik ve otel işaretleri kalır; oteller ile etkinlik alanı arasındaki tüm rota çizgileri gösterilmez.
- Rota mesafeleri, süreleri, sıralama ve otel önerisi değişmez.
- Harita lejantı kaldırılmış rota çizgilerini açıklamaz.

## Constraints & out of scope
- Bu değişiklik yalnızca harita sunumunu değiştirir; routing sonuçları hesaplanmaya devam eder.
- Geocoding sağlayıcısı veya API sözleşmesi değiştirilmez.
- Haritada belirli bir etkinlik koordinatı sabit kodlanmaz.

## Acceptance criteria
- [x] AC-1 — Birden fazla geocoding sonucu geldiğinde yalnızca etkinlik adıyla eşleştiği doğrulanabilen sonuç konum olarak kullanılır.
- [x] AC-2 — Etkinlik adıyla doğrulanabilir eşleşme yoksa sonuç etkinlik konumu olarak kabul edilmez.
- [x] AC-3 — Hosted ve şematik PDF haritaları etkinlik/otel işaretlerini gösterir ve hiçbir otel-etkinlik rota çizgisi çizmez.
- [x] AC-4 — PDF harita lejantı görünür rota çizgisi bulunmadığında rota çizgisi açıklamalarını göstermez.
- [x] AC-5 — Harita çizgilerinin kaldırılması mesafe, süre, sıralama ve seçilen otel bilgisini değiştirmez.

## Verification evidence
- AC-1: `GeoapifyProviderTests.GeocodeAsync_skips_an_unrelated_first_result_and_uses_the_matching_venue`.
- AC-2: provider tests reject no-match, city-only, and abbreviated candidates.
- AC-3: hosted request/pixel assertions and schematic output equality prove paths are not rendered; both PDF provider paths retain markers.
- AC-4: PDF integration tests assert route legend entries are absent and venue/rank entries remain.
- AC-5: orchestrator ranking/metrics tests and PDF recommendation assertions pass; report mapping remains unchanged.
- Full `scripts/check`: build has 0 warnings and 0 errors; UnitTests 106 passed; ProviderTests 48 passed; ArchitectureTests 5 passed; IntegrationTests 52 passed, 4 Mongo tests skipped; exit 0 (`CHECK GREEN (2 steps)`).
- Independent final review: no concrete findings.

## Definition of Done
- [x] Her kabul kriteri test veya tekrar edilebilir gözlemle kanıtlanır.
- [x] `scripts/check` yeşildir.
- [x] Bağımsız inceleme tamamlanır ve gerçek bulgular giderilir.
- [ ] Spec `specs/done/` konumuna taşınır.
