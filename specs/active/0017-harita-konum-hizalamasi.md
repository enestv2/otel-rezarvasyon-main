# Spec 0017 - Harita konum hizalamasi

- Status: Amendment approved and implemented; verification gate pending
- Mode: lite
- Plan: `specs/plans/0017-plan.md`

## Intent
PDF haritasinda etkinlik ve otel isaretleri taban haritadaki kendi koordinatlarinda gorunmelidir. Hosted haritada tum isaretler Geoapify tarafinda konumlanir ve gorunum bu koordinatlara gore otomatik sigdirilir. Sematik fallback etkinlik ve otel koordinatlarini kendi projeksiyonuyla cizer.

## Requirements
- Hosted haritada etkinlik pini ve numarali otel isaretleri verilen koordinatlarla Geoapify tarafinda cizilmelidir; sematik haritada isaretler verilen koordinatlarla konumlanmalidir.
- Hosted haritanin gorunumu tum etkinlik ve otel marker koordinatlarini kapsamalidir; hosted isaretlerin konumu icin yerel projeksiyon kullanilmamalidir.
- Projeksiyon kullanan sematik haritada etkinlik, otel ve varsa gorunur rota noktalarinin tamami cizim alaninda kalmalidir.
- Farkli enlem ve boylam degerleri hem dogu-bati hem kuzey-guney yonunde beklenen piksel farkini olusturmalidir.
- Koordinat duzeltmesi hem hosted Geoapify haritasinda hem de sematik fallback haritasinda dogru calismalidir.

## Constraints & out of scope
- Geocoding veya routing saglayicilarinin koordinat cevabi degistirilmez.
- Harita saglayicisi, PDF yerlesimi, geocoding kapsam kalitesi ve rota secim politikasi degismez.
- Hosted etkinlik ve otel isaretleri icin Geoapify'nin native marker koordinatlamasi ve marker tabanli otomatik gorunum sigdirma davranisi kullanilir; Geoapify'nin belgelenmemis dahili piksel olcegi varsayilmaz.
- Hosted haritada rota cizgileri cizilmez; mevcut 0018 davranisi korunur.

## Acceptance criteria
- [x] AC-1 Hosted etkinlik ve tum otel koordinatlari Geoapify istegine native marker olarak eklenir. Kanit: `HostedStaticMapProviderTests.RenderAsync_sends_all_locations_as_native_markers_and_auto_fits_for_hotel_count`.
- [x] AC-2 Fit etkinlik, otel ve rota noktalarini 32 px ic boslukla cizim alaninda tutar. Kanit: `StaticMapProjectionTests.Fit_centers_venue_and_hotel_coordinates_in_the_map_viewport` her noktanin piksel sinirini denetler.
- [x] AC-3 Sematik harita mevcut koordinat projeksiyonunu korur. Kanit: Schematic provider testleri tam test kosusunda gecti.
- [x] AC-4 Hosted istekte sabit center/zoom gonderilmez ve hem tek hem cok otelli fixture'larda etkinlik/otel koordinatlari native marker olarak gonderilir. Kanit: `HostedStaticMapProviderTests.RenderAsync_sends_all_locations_as_native_markers_and_auto_fits_for_hotel_count`.
- [x] AC-5 Hosted overlay yerel etkinlik pini ve otel rozeti cizmez; otel numaralari renkli dolgu ve beyaz rakamla Geoapify marker JSON'unda bulunur. Beyaz icerik arkasindaki varsayilan beyaz daire ve golge kapatilidir. Kanit: HostedStaticMapProviderTests request assertion'lari ve mock basemap pixel assertion'lari.

## Definition of Done
- [ ] Her kabul kriteri test veya tekrar edilebilir gozlemle kanitlanir.
- [ ] `scripts/check` yesildir.
- [ ] Bagimsiz inceleme yapilir; gercek bulgular giderilir.
- [ ] Spec `specs/done/` konumuna tasinir.

## Diagnosis status
- Controlled reproduction: the user reran the single-hotel and multiple-hotel requests with identical venue name and country; the event pin remained wrong only in the multiple-hotel map. This excludes venue-name/country geocoding differences and points to the changed map viewport/zoom.
- The custom overlay uses a 256 logical world-pixel model. MapLibre's vector-tile default is 512 pixels, but Geoapify's Static Maps reference does not explicitly document its internal logical world-pixel scale. The proposed 512-pixel change only passed a self-referential unit test and was reverted after independent review found the provider alignment unproven.
- Implemented under the earlier approved amendment: hosted Geoapify draws the event pin from its exact coordinate, but local hotel badges still depend on a separate unverified projection.
- Earlier verification applied to the prior implementation only. After the current amendment: HostedStaticMapProviderTests 7/7; full provider tests 49/49; full solution build 0 warnings/errors; full solution tests 106 Unit, 49 Provider, 5 Architecture, 52 Integration passed, 4 Mongo tests skipped.
- Gercek Geoapify POST smoke testinde once HTTP 400 alindi: `markers[0].iconsize` sayisal gonderiliyordu, servis `small`, `medium` veya `large` bekliyor. Deger `medium` yapildiktan sonra ayni tur istek HTTP 200 ve gecerli PNG dondurdu.
- `scripts/check` bu Windows ortaminda WSL shim'ine yonleniyor ancak kurulu WSL dagitimi olmadigi icin calismiyor. Build ve test komutlari dogrudan basarili. Kod degisikligi icin bagimsiz inceleme ve API yeniden baslatilip PDF yeniden uretilerek etkinlik/otel pinlerinin canli Geoapify taban haritasindaki gorsel kontrolu bekliyor.
- Yeni PDF gorseli etkinlik pininin gorunmedigini ve yerel otel rozetlerinin basemap konumlariyla hizasizligini tekrar gosterdi. Onerilen duzeltme: hosted haritada etkinlik ve otel isaretlerini birlikte Geoapify native marker olarak gondermek ve sabit center/zoom degerlerini kaldirarak saglayicinin gorunumu marker koordinatlarina gore sigdirmasi. Bu degisiklik her otel icin ek marker kredisi kullanabilir; plan onayi bekliyor.
