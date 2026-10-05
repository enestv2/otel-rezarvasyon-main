# Spec 0019 — Premium otel önerisi ilk sayfa

- Status: Draft
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
