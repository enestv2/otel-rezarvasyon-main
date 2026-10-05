# Spec 0009 — Profesyonel PDF rapor düzeni

- Status: Layout amendment implemented; verification gate pending
- Mode: lite
- Plan: `specs/plans/0009-plan.md`
- Supersedes: the 30%-size floating detail inset requirement in AC-4 of `specs/active/0008-yakin-harita-liste-sirasi.md`

## Intent
Rezervasyon ekibi PDF'nin ilk sayfasında bütçe ve ulaşım açısından önemli otel seçeneklerini hızlıca karşılaştırabilmeli, ikinci sayfada harita ve ayrıntılı otel tablosuna bakabilmelidir. Belge A4 boyutunda okunaklı kalmalı; otel değerleri simgelerle kolay taranmalıdır. Otel sırası, seçim kuralları, fiyatlar ve JSON öneri verileri değişmez.

## Requirements
- PDF'nin ilk sayfasında yalnızca bütçe öncelikli (en düşük gecelik tutar) ve ulaşım öncelikli (en uygun ölçülmüş ulaşım) seçenekleri sunulur; dengeli seçenek PDF'de gösterilmez.
- Turuncu özet kartları otel/seçenek adını ve yol mesafesi, yol süresi, yürüme mesafesi, yürüme süresi ve gecelik tutar değerlerini simge + değer biçiminde gösterir; bu alanda açıklama paragrafları bulunmaz.
- Özet kartlarının ardından LLM destekli açıklama, daha ekonomik oteli ve ulaşım açısından uygun seçeneği gerekçeleriyle açıklar.
- Harita raporun ikinci sayfasında üstte yer alır; değerlendirme tablosu aynı sayfada haritanın altından başlar ve gerekirse sonraki sayfalara taşar.
- Genel harita ve yakın plan ayrı panellerde gösterilir; yakın plan genel haritanın üstünü kapatmaz.
- Her harita panelinin başlığı ne gösterdiğini doğru açıklar. Yakın plan, listedeki ilk en fazla iki uygun otel ve yürüme rotalarını gösterir.
- Harita işaretleri ve PDF tablo sıraları eşleşir. Seçili otel ile diğer oteller renk ve çizgi örüntüsüyle ayırt edilir.
- Harita açıklaması kısa ve görsel anahtarla anlaşılır; uzun, sıkışık paragraf haritanın altında kullanılmaz.
- Otel tablosu rahat taranır; yol ölçümleri, yürüme ölçümleri ve gecelik fiyat görsel olarak gruplanır.
- Belgedeki tüm mevcut rapor bilgileri, atıflar, erişilebilir harita yedeği ve sayfa numaraları korunur.

## Constraints & out of scope
- Öneri sırası, otel seçimi, yürüyüş uygunluk kuralı, mesafe/süre/fiyat değerleri ve JSON sözleşmesi değişmez. Dengeli tercihi yalnızca PDF görünümünden çıkar; iç politika/JSON davranışı korunur.
- Yeni harita sağlayıcısı veya yeni veri kaynağı eklenmez.
- Marka kılavuzu verilmediği için mevcut kurumsal lacivert/turuncu renk paleti korunur.
- Tasarım A4 dikey PDF ve mevcut Türkçe içerik için yapılır.

## Acceptance criteria
- [x] AC-1 — İlk sayfada yalnızca bütçe öncelikli ve ulaşım öncelikli özetler yer alır; dengeli seçenek PDF kartlarında ve tablo ek açıklamalarında görünmez. Kanıt: PDF integration assertions.
- [x] AC-2 — Turuncu özet kartlarında otel adı ve yol/yürüme mesafe-süreleri ile fiyat ilgili simge ve değerlerle gösterilir; uzun açıklama metni kartların içinde değildir. Ardından LLM destekli maliyet/uygunluk açıklaması yer alır. Kanıt: PDF integration test + rendered page 1 image.
- [x] AC-3 — Harita ikinci sayfanın üstünde, otel tablosu aynı sayfada haritanın altında başlar; uzun tablolar başlıklarını tekrarlayarak devam edebilir. Kanıt: PDF page-order integration test + 20-hotel pagination test.
- [x] AC-4 — Genel görünüm ve yakın plan ayrı, başlıklı panellerde yer alır; hiçbir harita diğerinin üstünü kapatmaz ve panel başlıkları içerikle eşleşir. Kanıt: static map provider tests + rendered page 2 image.
- [x] AC-5 — Rapor sıra numaraları harita rozetleriyle eşleşir; seçili otel/rota ile diğer oteller/rotalar açıklamadaki renk ve örüntüyle ayırt edilir. Kanıt: existing map/provider and PDF legend assertions.
- [x] AC-6 — Otel tablosu A4 sayfa genişliğinde başlık ve değerleri kırpmadan gösterir; yol, yürüme ve fiyat sütunları kolay taranır. Kanıt: PDF extraction/bounds and long-name pagination tests.
- [x] AC-7 — Mevcut rapor verileri, PDF/JSON öneri eşitliği, atıflar, yedek harita ve sayfa numaraları korunur. Kanıt: PDF integration suite.
- [x] AC-8 — Üretilen PDF'nin gerçek sayfa görüntüsü incelenir ve metin/harita örtüşmesi, okunamayan başlık veya kesilen içerik bulunmadığı kanıtlanır. Kanıt: `specs/evidence/0009-pdf-layout-page1.png` and `page2.png`.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique
- **Risk:** Yan yana harita panelleri genel görünümün yatay alanını daraltabilir. Önerim: birleşik harita genişliğinin %60'ını genel görünüme, %40'ını yakın plana ayırmak; yakın plan dikey boyunu koruduğu için ayrıntı ve rota takibi mevcut küçük iç pencereden daha rahat olur. Harita projeksiyonları her panelin kendi boyutuna göre yeniden sığdırılır.
- **Risk:** Uzun otel adları ve sütun başlıkları tabloyu daraltabilir. Önerim: tablo sütunlarını gruplamak ve otel adı sütununa en büyük esnek payı vermek; tüm fiyat/rota değerleri korunur.
- **Risk:** Görsel kalite yalnızca metin çıkarımıyla doğrulanamaz. Önerim: test kanıtına ek olarak PDF sayfasının render edilmiş görüntüsünü incelemek ve bu kanıtı AC-6'ya bağlamak.
