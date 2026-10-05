using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Reporting;

/// <summary>
/// Rapor modelini QuestPDF ile Türkçe şablonlu PDF belgesine çevirir (ADR-0006, R-1..R-9). Metinler
/// çekirdekten gelir; bu adaptör iş kuralı içermez, yalnızca yerleşim ve biçimlendirme yapar.
/// Biçimlendirme kültürden bağımsızdır (docs/conventions.md).
/// </summary>
public sealed class QuestPdfRecommendationReportRenderer : IRecommendationReportRenderer
{
    private const string FontFamily = "Lato";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public Task<ReportDocument> RenderAsync(RecommendationReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = Document.Create(container => Compose(container, report)).GeneratePdf();

        return Task.FromResult(new ReportDocument(bytes, ReportDocument.PdfContentType, BuildFileName(report.VenueName)));
    }

    private static void Compose(IDocumentContainer container, RecommendationReport report)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(10);
                column.Item().Element(element => ComposeHeader(element, report));
                column.Item().Element(element => ComposeRecommendationOptions(element, report));
                column.Item().Element(element => ComposeRecommendationExplanation(element, report));
            });

            ComposeFooter(page);
        });

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken4));
            page.Header().Element(element => ComposeLegacyHeader(element, report));

            page.Content().PaddingTop(22).PaddingBottom(12).Column(column =>
            {
                column.Spacing(10);
                if (report.Map is not null)
                {
                    column.Item().Element(element => ComposeMap(element, report.Map));
                }
                else
                {
                    column.Item().Text("Coğrafi görsel bu belge için üretilemedi.").Italic().FontColor(Colors.Grey.Darken1);
                }

                column.Item().Element(element => ComposeTable(element, report));

                if (report.UnresolvedHotels.Count > 0)
                {
                    column.Item().Element(element => ComposeUnresolved(element, report));
                }
            });

            ComposeFooter(page);
        });
    }

    private static void ComposeFooter(PageDescriptor page) => page.Footer().AlignCenter().Text(text =>
    {
        text.Span("Sayfa ").FontSize(8).FontColor(Colors.Grey.Darken1);
        text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
        text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
        text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
    });

    private static void ComposeHeader(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().Text(report.Title.ToUpper(TurkishCulture)).FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
            column.Item().Text(report.VenueName).FontSize(13).SemiBold().FontColor(Colors.Grey.Darken2);
            if (report.Personnel is { } personnel)
            {
                column.Item().Text(text =>
                {
                    text.Span("Talep sahibi  ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.Span($"{personnel.FirstName} {personnel.LastName}").FontSize(9).SemiBold();
                    text.Span($"  ·  Sicil {personnel.RegistrationNumber}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
            var localGeneratedAt = TimeZoneInfo.ConvertTime(report.GeneratedAtUtc, TurkeyTimeZone);
            column.Item().Text($"{localGeneratedAt.ToString("dd MMMM yyyy · HH:mm", TurkishCulture)} TSİ")
                .FontSize(8.5f)
                .FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeLegacyHeader(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Item().Text(report.Title).FontSize(20).SemiBold();
            column.Item().Text(report.VenueName).FontSize(12).FontColor(Colors.Grey.Darken2);
            if (report.Personnel is { } personnel)
            {
                column.Item().Text($"Talep sahibi: {personnel.FirstName} {personnel.LastName} (Sicil: {personnel.RegistrationNumber})")
                    .FontSize(9);
            }
            column.Item().Text(string.Create(CultureInfo.InvariantCulture, $"Belge üretim zamanı: {report.GeneratedAtUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC"))
                .FontSize(8)
                .FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeRecommendationOptions(IContainer container, RecommendationReport report)
    {
        var visibleOptions = report.Options
            .Where(option => option.Strategy is RecommendationStrategy.BudgetPriority or RecommendationStrategy.TransportPriority)
            .OrderBy(option => option.Strategy == RecommendationStrategy.TransportPriority ? 0 : 1)
            .ToArray();

        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text("ÖNERİLEN OTELLER").FontSize(11).SemiBold().FontColor(Colors.Orange.Darken3);
            foreach (var option in visibleOptions)
            {
                if (option.Hotel is not { } hotel)
                {
                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).CornerRadius(8).Padding(12)
                        .Text(string.IsNullOrWhiteSpace(option.Explanation) ? "Bu seçenek için otel belirlenemedi." : option.Explanation)
                        .FontSize(9).FontColor(Colors.Grey.Darken2).ClampLines(2);
                    continue;
                }

                var roleLabel = option.Strategy == RecommendationStrategy.TransportPriority ? "ÖNERİLEN" : "ALTERNATİF";
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.White)
                    .CornerRadius(8).Padding(11).Column(card =>
                    {
                        card.Spacing(6);
                        card.Item().Row(top =>
                        {
                            top.RelativeItem().Column(identity =>
                            {
                                identity.Item().Row(badges =>
                                {
                                    badges.AutoItem().Background(option.Strategy == RecommendationStrategy.TransportPriority
                                            ? Colors.Orange.Lighten5 : Colors.Grey.Lighten4)
                                        .PaddingHorizontal(6).PaddingVertical(3)
                                        .Text(roleLabel).FontSize(7.5f).SemiBold()
                                        .FontColor(option.Strategy == RecommendationStrategy.TransportPriority
                                            ? Colors.Orange.Darken3 : Colors.Grey.Darken2);
                                    badges.AutoItem().PaddingLeft(6).AlignMiddle()
                                        .Text(StrategyLabel(option.Strategy)).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                                });
                                identity.Item().PaddingTop(4).Text(hotel.Name).FontSize(13).SemiBold()
                                    .FontColor(Colors.Grey.Darken4);
                            });
                            top.AutoItem().AlignRight().Column(price =>
                            {
                                price.Item().AlignRight().Text(FormatCardPrice(hotel.Price))
                                    .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                                price.Item().AlignRight().Text("tahmini gecelik").FontSize(7.5f)
                                    .FontColor(Colors.Grey.Darken1);
                            });
                        });
                        card.Item().PaddingTop(1).Row(metrics =>
                        {
                            metrics.Spacing(5);
                            Metric(metrics, MetricIcon.Location, "MESAFE", RecommendationMetricFormatter.Distance(hotel.DistanceMeters));
                            Metric(metrics, MetricIcon.Car, "ARAÇ", RecommendationMetricFormatter.RoadDuration(hotel));
                            if (hotel.WalkingRouteRequested)
                            {
                                var walkingValue = hotel.WalkingMetrics is null
                                    ? "Hesaplanamadı"
                                    : $"{RecommendationMetricFormatter.Distance(hotel.WalkingMetrics.DistanceMeters)} · {RecommendationMetricFormatter.WalkingDuration(hotel.WalkingMetrics)}";
                                Metric(metrics, MetricIcon.Walking, "YÜRÜME", walkingValue);
                            }
                        });
                        if (!string.IsNullOrWhiteSpace(option.Explanation))
                        {
                            card.Item().PaddingTop(1).Text(ShortReason(option.Explanation)).FontSize(8.5f)
                                .FontColor(Colors.Grey.Darken2).ClampLines(2);
                        }
                    });
            }
        });
    }

    private static string ShortReason(string reason)
    {
        var sentenceEnd = reason.IndexOf(". ", StringComparison.Ordinal);
        if (sentenceEnd >= 0)
        {
            reason = reason[..(sentenceEnd + 1)];
        }

        const int maxLength = 120;
        if (reason.Length <= maxLength)
        {
            return reason;
        }

        var lastSpace = reason.LastIndexOf(' ', maxLength - 1);
        return $"{reason[..(lastSpace > 0 ? lastSpace : maxLength - 1)].TrimEnd()}…";
    }

    private static void ComposeRecommendationExplanation(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("ÖNERİ ÖZETİ").FontSize(9.5f).SemiBold().FontColor(Colors.Grey.Darken2);
            foreach (var option in report.Options
                         .Where(option => option.Strategy is RecommendationStrategy.TransportPriority or RecommendationStrategy.BudgetPriority)
                         .OrderBy(option => option.Strategy == RecommendationStrategy.TransportPriority ? 0 : 1))
            {
                if (option.Hotel is not { } hotel)
                {
                    continue;
                }

                var summary = option.Strategy == RecommendationStrategy.TransportPriority
                    ? $"Ulaşım önceliğinde {hotel.Name} öne çıkıyor."
                    : $"Bütçe açısından {hotel.Name} daha avantajlı.";
                column.Item().Row(row =>
                {
                    row.Spacing(5);
                    row.AutoItem().Text("•").FontColor(Colors.Orange.Darken2);
                    row.RelativeItem().Text(summary).FontSize(8.5f).FontColor(Colors.Grey.Darken2).ClampLines(2);
                });
            }
            column.Item().PaddingTop(3)
                .Text("Fiyatlar istekte iletilen yaklaşık tutarlardır; rezervasyon teklifi veya teyidi değildir.")
                .FontSize(7.5f).Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Metric(RowDescriptor row, MetricIcon icon, string label, string value)
    {
        row.RelativeItem().Column(metric =>
        {
            metric.Spacing(1);
            metric.Item().Row(content =>
            {
                content.AutoItem().AlignMiddle().Width(13).Height(13).Svg(MetricIconSvg(icon));
                content.AutoItem().PaddingLeft(3).AlignMiddle().Text(label).FontSize(6.5f)
                    .SemiBold().FontColor(Colors.Grey.Darken1);
            });
            metric.Item().Text(value).FontSize(8.5f).SemiBold().FontColor(Colors.Grey.Darken4);
        });
    }

    private static string MetricIconSvg(MetricIcon icon)
    {
        var paths = icon switch
        {
            MetricIcon.Location => "<path d=\"M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z\"/><circle cx=\"12\" cy=\"10\" r=\"2.5\"/>",
            MetricIcon.Car => "<path d=\"m5 11 1.5-5h11l1.5 5M3 11h18v7H3z\"/><path d=\"M6 18v2m12-2v2M6 14h.01M18 14h.01\"/>",
            MetricIcon.Walking => "<circle cx=\"14\" cy=\"4\" r=\"2\"/><path d=\"m12 8-3 4 3 2-2 6m2-12 4 3 3-1m-7 4-4 5m6-6 4 5\"/>",
            _ => throw new ArgumentOutOfRangeException(nameof(icon)),
        };

        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#334155\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{paths}</svg>";
    }

    private enum MetricIcon
    {
        Location,
        Car,
        Walking,
    }

    private static string StrategyLabel(RecommendationStrategy strategy) => strategy switch
    {
        RecommendationStrategy.BudgetPriority => "BÜTÇE ÖNCELİKLİ",
        RecommendationStrategy.TransportPriority => "ULAŞIM ÖNCELİKLİ",
        RecommendationStrategy.Balanced => "DENGE",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    private static void ComposeMap(IContainer container, StaticMapImage map)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("HARİTA GÖRÜNÜMÜ").FontSize(11).SemiBold().FontColor(Colors.Grey.Darken2);
            column.Item().Image(map.Content.ToArray()).FitWidth();
            column.Item().Element(legend => ComposeMapLegend(legend, map));

            if (!string.IsNullOrWhiteSpace(map.Attribution))
            {
                column.Item().Text(map.Attribution!).FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeMapLegend(IContainer container, StaticMapImage map)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Row(row =>
            {
                row.Spacing(12);
                LegendItem(row, "#DC2626", "Varış: etkinlik alanı", LegendSymbol.DestinationPin);
                LegendItem(row, Colors.Grey.Darken4, "Numara: PDF sırası", LegendSymbol.Number);
            });
            column.Item().Text(MapLegend(map)).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    private enum LegendSymbol
    {
        Square,
        DestinationPin,
        Number,
    }

    private static void LegendItem(RowDescriptor row, string color, string label, LegendSymbol symbol)
    {
        row.AutoItem().Row(item =>
        {
            item.Spacing(4);
            item.AutoItem().Element(container => DrawLegendSymbol(container, color, symbol));
            item.AutoItem().Text(label).FontSize(8);
        });
    }

    private static void DrawLegendSymbol(IContainer container, string color, LegendSymbol symbol)
    {
        switch (symbol)
        {
            case LegendSymbol.Square:
                container.Width(8).Height(8).Background(color);
                break;
            case LegendSymbol.DestinationPin:
                container.Width(8).Height(11).Column(column =>
                {
                    column.Item().AlignCenter().Width(8).Height(8).Background(color).CornerRadius(4);
                    column.Item().AlignCenter().Width(2).Height(3).Background(color);
                });
                break;
            case LegendSymbol.Number:
                container.Width(12).Height(12).Background(color).AlignCenter().AlignMiddle()
                    .Text("1").FontSize(7).SemiBold().FontColor(Colors.White);
                break;
        }
    }

    private static string MapLegend(StaticMapImage map)
    {
        var source = map.Attribution is null ? "Şematik görünüm. " : string.Empty;
        return $"{source}Numaralar otel tablosundaki sırayı gösterir.";
    }
    private static void ComposeTable(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("Değerlendirilen oteller (sıralı)").FontSize(11).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    GroupHeaderCell(header, "OTEL", 2);
                    GroupHeaderCell(header, "YOL", 3);
                    GroupHeaderCell(header, "YÜRÜME", 2);
                    GroupHeaderCell(header, "GECELİK ÜCRET", 1);
                    HeaderCell(header, "#");
                    HeaderCell(header, "Otel");
                    HeaderCell(header, "Mesafe");
                    HeaderCell(header, "Süre");
                    HeaderCell(header, "Tür");
                    HeaderCell(header, "Mesafe");
                    HeaderCell(header, "Süre");
                    HeaderCell(header, "Tutar");
                });

                for (var index = 0; index < report.RankedHotels.Count; index++)
                {
                    var row = report.RankedHotels[index];

                    var alternate = index % 2 == 1;
                    var optionLabels = string.Join(", ", report.Options
                        .Where(option => option.Strategy is RecommendationStrategy.BudgetPriority or RecommendationStrategy.TransportPriority
                            && option.Hotel?.Name == row.Name)
                        .Select(option => StrategyLabel(option.Strategy)));
                    var hotelLabel = string.IsNullOrEmpty(optionLabels) ? row.Name : $"{row.Name} ({optionLabels})";
                    BodyCell(table, string.Create(CultureInfo.InvariantCulture, $"{index + 1}"), row.IsSelected, alternate);
                    BodyCell(table, hotelLabel, row.IsSelected, alternate);
                    BodyCell(table, FormatDistance(row.DistanceMeters), row.IsSelected, alternate);
                    BodyCell(table, FormatDuration(row), row.IsSelected, alternate);
                    BodyCell(table, FormatKind(row.DistanceKind), row.IsSelected, alternate);
                    BodyCell(table, FormatWalkingDistance(row), row.IsSelected, alternate);
                    BodyCell(table, FormatWalkingDuration(row), row.IsSelected, alternate);
                    BodyCell(table, FormatPrice(row.Price), row.IsSelected, alternate);
                }
            });
        });
    }

    private static void ComposeUnresolved(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().Text("Çözümlenemeyen oteller").FontSize(11).SemiBold();
            column.Item().Text(string.Join(", ", report.UnresolvedHotels)).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().Text("Bu oteller konuma çevrilemedi veya mesafesi ölçülemedi; öneri dışında bırakıldı.").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void GroupHeaderCell(TableCellDescriptor header, string text, int span) =>
        header.Cell().ColumnSpan((uint)span).Background(Colors.Blue.Darken3).Padding(5)
            .Text(text).FontSize(8).SemiBold().FontColor(Colors.White);

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(text).FontSize(9).SemiBold();

    private static void BodyCell(TableDescriptor table, string text, bool highlighted, bool alternate)
    {
        IContainer cell = table.Cell();

        if (highlighted)
        {
            cell = cell.Background(Colors.Orange.Lighten5);
        }
        else if (alternate)
        {
            cell = cell.Background(Colors.Grey.Lighten5);
        }

        cell.ShowEntire().Padding(5).Text(text).FontSize(9);
    }

    private static string FormatDistance(int meters) => RecommendationMetricFormatter.Distance(meters);

    private static string FormatDuration(HotelReportRow row) => RecommendationMetricFormatter.RoadDuration(row);

    private static string FormatWalkingDistance(HotelReportRow row) =>
        !row.WalkingRouteRequested ? "—"
        : row.WalkingMetrics is null ? "Alınamadı"
        : RecommendationMetricFormatter.Distance(row.WalkingMetrics.DistanceMeters);

    private static string FormatWalkingDuration(HotelReportRow row) =>
        !row.WalkingRouteRequested ? "—"
        : row.WalkingMetrics is null ? "Alınamadı"
        : RecommendationMetricFormatter.WalkingDuration(row.WalkingMetrics);

    private static string FormatKind(DistanceKind kind) =>
        kind == DistanceKind.Road ? "Yol" : "Düz çizgi";

    private static string FormatPrice(NightlyPrice price) => RecommendationMetricFormatter.Price(price);

    private static string FormatCardPrice(NightlyPrice price)
    {
        var amountFormat = price.Amount == decimal.Truncate(price.Amount) ? "#,##0" : "#,##0.00";
        return $"{price.Amount.ToString(amountFormat, TurkishCulture)} {price.Currency}";
    }

    /// <summary>Etkinlik alanı adından ASCII-güvenli, kültürden bağımsız bir dosya adı üretir.</summary>
    private static string BuildFileName(string venueName)
    {
        var builder = new StringBuilder("oneri-");

        foreach (var character in venueName.Trim().ToLowerInvariant())
        {
            var mapped = character switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ğ' => 'g',
                'ç' => 'c',
                'ö' => 'o',
                'ü' => 'u',
                >= 'a' and <= 'z' => character,
                >= '0' and <= '9' => character,
                _ => '-',
            };

            if (mapped == '-' && (builder.Length == 0 || builder[^1] == '-'))
            {
                continue;
            }

            builder.Append(mapped);
        }

        var slug = builder.ToString().TrimEnd('-');
        if (slug == "oneri")
        {
            slug = "oneri-belgesi";
        }

        return $"{slug}.pdf";
    }
}
