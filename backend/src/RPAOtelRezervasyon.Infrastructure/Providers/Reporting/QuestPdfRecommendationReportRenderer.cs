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
    private const string Navy = "#092C5C";
    private const string AccentBlue = "#1769AA";
    private const string PaleBlue = "#F2F7FC";
    private const string Border = "#DCE6F0";
    private const float SpacingXs = 2;
    private const float SpacingSm = 4;
    private const float SpacingMd = 6;
    private const float SpacingLg = 8;
    private const float SpacingXl = 12;
    private const float Spacing2Xl = 16;
    private const float Spacing3Xl = 20;
    private const float PageGutter = 24;
    private const float HeaderPadding = 22;
    private const string LogoResourceName = "RPAOtelRezervasyon.Infrastructure.Resources.logo.svg";
    private const float LogoWidth = 90;
    private const float LogoHeight = 24.8f;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly string LogoSvg = LoadLogoSvg();

    public Task<ReportDocument> RenderAsync(RecommendationReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = Document.Create(container => Compose(container, report)).GeneratePdf();

        return Task.FromResult(new ReportDocument(bytes, ReportDocument.PdfContentType, BuildFileName(report)));
    }

    private static void Compose(IDocumentContainer container, RecommendationReport report)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(24);
            page.MarginBottom(32);
            page.MarginHorizontal(0);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Content().Column(column =>
            {
                column.Item().PaddingHorizontal(SpacingXl).Element(element => ComposeHeader(element, report));
                column.Item().PaddingHorizontal(PageGutter).PaddingTop(Spacing2Xl).Column(content =>
                {
                    content.Spacing(Spacing2Xl);
                    content.Item().Element(element => ComposeRecommendationOptions(element, report));
                });
            });

            ComposeFooter(page);
        });

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Content().PaddingVertical(SpacingXl).Column(column =>
            {
                column.Spacing(Spacing3Xl);
                if (report.Map is not null)
                {
                    column.Item().Element(element => ComposeMap(element, report.Map, report));
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

    private static void ComposeFooter(PageDescriptor page) => page.Footer().PaddingTop(SpacingMd).BorderTop(0.6f).BorderColor(Border).AlignCenter().Text(text =>
    {
        text.Span("Sayfa ").FontSize(9).FontColor(Colors.Grey.Darken1);
        text.CurrentPageNumber().FontSize(9).FontColor(Navy);
        text.Span(" / ").FontSize(9).FontColor(Colors.Grey.Darken1);
        text.TotalPages().FontSize(9).FontColor(Navy);
    });

    private static void ComposeHeader(IContainer container, RecommendationReport report)
    {
        container.Background(Navy).CornerRadius(10).Padding(HeaderPadding).Column(column =>
        {
            column.Spacing(SpacingSm);
            column.Item().Row(brand =>
            {
                brand.RelativeItem().Column(identity =>
                {
                    identity.Item().Text(report.Title.ToUpper(TurkishCulture)).FontSize(20).Bold().FontColor(Colors.White);
                    identity.Item().PaddingTop(SpacingSm).Text(report.EventCenterName).FontSize(12).SemiBold().FontColor("#DDEBFA");
                });
                brand.AutoItem().AlignMiddle().PaddingLeft(SpacingXl).Width(LogoWidth).Height(LogoHeight).Svg(LogoSvg);
            });
            column.Item().PaddingTop(SpacingLg).LineHorizontal(0.7f).LineColor("#8795A8");
            column.Item().PaddingTop(SpacingLg).Row(meta =>
            {
                meta.AutoItem().AlignMiddle().Text(text =>
                {
                    if (report.Personnel is { } personnel)
                    {
                        text.Span($"{personnel.RegistrationNumber} · {personnel.FirstName} {personnel.LastName}")
                            .FontSize(9).FontColor(Colors.White);
                    }
                });
                meta.RelativeItem();
                var localGeneratedAt = TimeZoneInfo.ConvertTime(report.GeneratedAtUtc, TurkeyTimeZone);
                meta.AutoItem().AlignRight().AlignMiddle().Text(localGeneratedAt.ToString("dd MMMM yyyy · HH:mm", TurkishCulture))
                    .FontSize(9).FontColor("#DDEBFA");
            });
        });
    }

    private static string LoadLogoSvg()
    {
        using var stream = typeof(QuestPdfRecommendationReportRenderer).Assembly
            .GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException($"Embedded logo resource '{LogoResourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var svg = reader.ReadToEnd();
        if (!svg.Contains("class=\"cls-1\"", StringComparison.Ordinal)
            || !svg.Contains("class=\"cls-2\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The embedded logo SVG no longer has the expected fill classes.");
        }

        return svg
            .Replace("class=\"cls-1\"", "fill=\"#FFFFFF\"", StringComparison.Ordinal)
            .Replace("class=\"cls-2\"", "fill=\"#FFFFFF\" fill-rule=\"evenodd\"", StringComparison.Ordinal);
    }

    private static void ComposeRecommendationOptions(IContainer container, RecommendationReport report)
    {
        var visibleOptions = report.Options
            .Where(option => option.Strategy is RecommendationStrategy.BudgetPriority or RecommendationStrategy.TransportPriority)
            .OrderBy(option => option.Strategy == RecommendationStrategy.TransportPriority ? 0 : 1)
            .ToArray();

        container.Column(column =>
        {
            column.Spacing(SpacingXl);
            column.Item().Row(targetLine =>
            {
                targetLine.AutoItem().Background("#E8F1FF").Border(0.6f).BorderColor("#C9DCF8")
                    .CornerRadius(7).PaddingHorizontal(SpacingLg).PaddingVertical(SpacingSm).Row(target =>
                    {
                        target.AutoItem().AlignMiddle().Width(11).Height(11).Svg(MetricIconSvg(MetricIcon.Location));
                        target.AutoItem().PaddingLeft(SpacingSm).AlignMiddle().Text(report.PreferCityCenter
                                ? "Hedef: Şehir Merkezi"
                                : "Hedef: Etkinlik Merkezi")
                            .FontSize(9.5f).SemiBold().FontColor(Navy);
                    });
                if (report.TaskStartDate is { } taskStart && report.TaskEndDate is { } taskEnd)
                {
                    var taskPeriod = $"Görev: {taskStart.ToString("dd MMMM yyyy", TurkishCulture)} - {taskEnd.ToString("dd MMMM yyyy", TurkishCulture)}";
                    targetLine.RelativeItem();
                    targetLine.AutoItem().AlignMiddle().Text(taskPeriod).FontSize(9).FontColor("#52657D");
                }
            });
            foreach (var option in visibleOptions)
            {
                if (option.Hotel is not { } hotel)
                {
                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).CornerRadius(8).Padding(SpacingXl)
                        .Text(string.IsNullOrWhiteSpace(option.Explanation) ? "Bu seçenek için otel belirlenemedi." : option.Explanation)
                        .FontSize(9).FontColor(Colors.Grey.Darken2).ClampLines(2);
                    continue;
                }

                var tripCost = hotel.TripCostEstimate;

                column.Item().Border(0.8f).BorderColor("#D7E5F7").Background("#FFFFFF")
                    .CornerRadius(10).Padding(SpacingXl).Column(card =>
                    {
                        card.Spacing(SpacingLg);
                        card.Item().Row(top =>
                        {
                            top.RelativeItem().Column(identity =>
                            {
                                identity.Item().Text("ÖNERİLEN OTEL").FontSize(9).SemiBold().FontColor("#52657D");
                                identity.Item().PaddingTop(SpacingSm).Text(hotel.Name).FontSize(17).SemiBold().FontColor(Navy);
                                if (!string.IsNullOrWhiteSpace(report.CityName))
                                {
                                    identity.Item().PaddingTop(SpacingSm).Row(location =>
                                    {
                                        location.AutoItem().AlignMiddle().Width(10).Height(10).Svg(MetricIconSvg(MetricIcon.Location));
                                        var locationName = string.IsNullOrWhiteSpace(report.CountryName)
                                            ? report.CityName
                                            : $"{report.CityName}, {report.CountryName}";
                                        location.AutoItem().PaddingLeft(SpacingSm).Text(locationName).FontSize(9).FontColor("#455B75");
                                    });
                                }
                            });
                            top.AutoItem().AlignMiddle().Background("#E8F7EF").CornerRadius(10).PaddingHorizontal(SpacingLg).PaddingVertical(SpacingSm)
                                .Row(badge =>
                                {
                                    badge.AutoItem().AlignMiddle().Width(10).Height(10).Svg(MetricIconSvg(MetricIcon.Star));
                                    badge.AutoItem().PaddingLeft(SpacingSm).AlignMiddle().Text("Önerilen").FontSize(9).SemiBold().FontColor("#176B46");
                                });
                        });
                        card.Item().LineHorizontal(0.6f).LineColor(Border);
                        card.Item().Background("#EFF5FD").CornerRadius(9).Padding(SpacingXl).Row(total =>
                        {
                            total.RelativeItem().Column(amount =>
                            {
                                amount.Item().Text("Tahmini toplam konaklama ve ulaşım maliyeti").FontSize(10).FontColor("#40556E");
                                amount.Item().PaddingTop(SpacingXs).Text(tripCost is null
                                        ? FormatCardPrice(hotel.TotalNightlyCost ?? hotel.Price)
                                        : tripCost.TotalCost is { } totalAmount
                                            ? FormatCardPrice(totalAmount)
                                            : "Hesaplanamadı")
                                    .FontSize(23).Bold().FontColor("#173B70");
                                amount.Item().PaddingTop(SpacingXs).Text(tripCost is { } estimate
                                        ? $"Konaklama ({estimate.StayNights} gece) + şehir içi + Havalimanı transferi"
                                        : "Otel + şehir içi ulaşım")
                                    .FontSize(9).FontColor("#455B75");
                            });
                            total.ConstantItem(1).Height(48).Background("#DDE7F2");
                            total.RelativeItem().PaddingLeft(SpacingXl).AlignMiddle().Column(reason =>
                            {
                                if (tripCost?.TotalCost is not null)
                                {
                                    reason.Item().Text("Neden bu otel?").FontSize(10).SemiBold().FontColor(Navy);
                                    reason.Item().PaddingTop(SpacingSm).Text(option.Explanation)
                                        .FontSize(9.5f).FontColor("#40556E");
                                }
                                else
                                {
                                    reason.Item().Text(option.Explanation).FontSize(9).FontColor("#40556E").ClampLines(3);
                                }
                            });
                        });
                        card.Item().Row(costs =>
                        {
                            costs.Spacing(SpacingLg);
                            CostItem(costs, MetricIcon.Hotel, "Otel konaklama", tripCost is { } stay
                                    ? FormatCardPrice(stay.LodgingSubtotal) : FormatCardPrice(hotel.Price),
                                tripCost is { } nights ? $"{nights.StayNights} gece konaklama" : "Gecelik ücret");
                            CostItem(costs, MetricIcon.Car, "Şehir içi", tripCost?.LocalTransportSubtotal is { } localTransport
                                    ? FormatCardPrice(localTransport)
                                    : tripCost is null && hotel.TaxiFareEstimate is { } taxi
                                        ? FormatCardPrice(taxi.RoundTripCost)
                                        : "Hesaplanamadı",
                                tripCost is { } days ? $"{days.CommuteDays} gün · gidiş-dönüş" : "Etkinlik alanı gidiş-dönüş");
                            CostItem(costs, MetricIcon.Airport, "Havalimanı", report.NearestAirport?.AirportHotelRoundTripFare is { } airportFare
                                    ? FormatCardPrice(tripCost?.AirportRoundTripCost ?? airportFare.RoundTripCost)
                                    : tripCost?.AirportRoundTripCost is { } airportCost ? FormatCardPrice(airportCost) : "Hesaplanamadı",
                                "1 gidiş + 1 dönüş");
                        });
                        if (hotel.TaxiFareEstimate?.UsedDefaultFare == true)
                        {
                            var fallbackFare = hotel.TaxiFareEstimate;
                            var currency = fallbackFare.RoundTripCost.Currency;
                            var openingFare = FormatCardPrice(new NightlyPrice(fallbackFare.OpeningFare, currency));
                            var farePerKm = FormatCardPrice(new NightlyPrice(fallbackFare.FarePerKm, currency));
                            card.Item().Background("#FFF5DF").CornerRadius(6).Padding(SpacingMd).Column(fallback =>
                            {
                                fallback.Item().Text($"Şehir tarifesi doğrulanamadığı için yapılandırılmış varsayılan ulaşım bedeli ({openingFare} açılış + {farePerKm}/km) kullanıldı.")
                                    .FontSize(9.5f).SemiBold().FontColor("#76500D");
                            });
                        }
                        if (hotel.TaxiFareEstimate is { DistanceMeters: <= 1500 }
                            && hotel.WalkingRouteRequested
                            && hotel.WalkingMetrics is { } nearbyWalkingRoute)
                        {
                            var walkingMinutes = Math.Max(1, (int)Math.Round(nearbyWalkingRoute.DurationSeconds / 60.0));
                            card.Item().Background("#F2EEFA").CornerRadius(6).Padding(SpacingMd)
                                .Text($"Yürüyüş süresi {walkingMinutes} dakikadır. " +
                                    "Yürüyerek ulaşım tercih edilmesi, şehir içi transfer maliyetlerinin azaltılmasına katkı sağlayabilir.")
                                .FontSize(9.5f).FontColor("#51436F");
                        }
                        card.Item().Row(heading =>
                        {
                            heading.AutoItem().AlignMiddle().Width(15).Height(15).Svg(MetricIconSvg(MetricIcon.Location));
                            heading.RelativeItem().PaddingLeft(SpacingMd).Column(title =>
                            {
                                title.Item().Text("Ulaşım Bilgileri").FontSize(11).SemiBold().FontColor(Navy);
                                title.Item().PaddingTop(SpacingXs).Text("Otel konumu ve çevre noktalara ulaşım detayları")
                                    .FontSize(9).FontColor("#455B75");
                            });
                        });
                        card.Item().Element(row => ComposeTravelInfoRow(row,
                            report.PreferCityCenter ? MetricIcon.Location : MetricIcon.Route,
                            report.PreferCityCenter ? "Şehir merkezine rota" : "Etkinlik merkezine rota",
                            $"{FormatDistance(hotel.DistanceMeters)} mesafe", $"Araçla · {RecommendationMetricFormatter.RoadDuration(hotel)}", null));
                        if (hotel.WalkingRouteRequested)
                        {
                            card.Item().Element(row => ComposeTravelInfoRow(row, MetricIcon.Walking, "Yürüyüş",
                                hotel.WalkingMetrics is { } walk ? $"{FormatDistance(walk.DistanceMeters)} mesafe" : "Yürüyüş rotası alınamadı",
                                hotel.WalkingMetrics is { } walking ? $"Yürüyerek · {RecommendationMetricFormatter.WalkingDuration(walking)}" : "Yürüyüş süresi yok", null));
                        }
                        if (report.NearestAirport is { } airport)
                        {
                            var airportRoute = airport.Route.Kind == DistanceKind.Road
                                ? $"{FormatDistance(airport.Route.DistanceMeters)} · {Math.Max(1, airport.Route.DurationSeconds / 60)} dk"
                                : $"{FormatDistance(airport.Route.DistanceMeters)} düz çizgi tahmini";
                            card.Item().Element(row => ComposeTravelInfoRow(row, MetricIcon.Airport, airport.Name,
                                "En Yakın Havalimanı", airportRoute, null));
                        }
                        card.Item().PaddingTop(SpacingXs).Text("Otel fiyatı ve ulaşım bedelleri tahminidir; rezervasyon teklifi veya teyidi değildir.")
                            .FontSize(9).Italic().FontColor("#53657A");
                    });
            }
        });
    }

    private static void CostItem(RowDescriptor row, MetricIcon icon, string title, string value, string note)
    {
        var background = icon switch
        {
            MetricIcon.Hotel => "#F3F7FC",
            MetricIcon.Car => "#F2F8F5",
            MetricIcon.Airport => "#F5F5FC",
            _ => "#F7F9FC",
        };
        row.RelativeItem().Background(background).Border(0.6f).BorderColor("#E1EAF7").CornerRadius(8).Padding(SpacingLg).Column(item =>
        {
            item.Item().Row(label =>
            {
                label.AutoItem().AlignMiddle().Width(24).Height(24).Background(IconBackground(icon)).CornerRadius(12)
                    .AlignCenter().AlignMiddle().Width(13).Height(13).Svg(MetricIconSvg(icon));
                label.RelativeItem().PaddingLeft(SpacingMd).AlignMiddle().Text(title).FontSize(9).FontColor("#455B75");
            });
            item.Item().PaddingLeft(24 + SpacingMd).PaddingTop(SpacingSm)
                .Text(value).FontSize(9.5f).SemiBold().FontColor("#23466F");
            item.Item().PaddingLeft(24 + SpacingMd).PaddingTop(SpacingXs)
                .Text(note).FontSize(9).FontColor("#455B75");
        });
    }

    private static void ComposeTravelInfoRow(IContainer container, MetricIcon icon, string title, string subtitle, string right, string? rightSub)
    {
        container.Background(Colors.White).Border(0.6f).BorderColor("#E9EEF4").CornerRadius(8)
            .PaddingHorizontal(SpacingLg).PaddingVertical(SpacingMd).Row(row =>
        {
            row.AutoItem().AlignMiddle().Width(18).Height(18).Svg(MetricIconSvg(icon));
            row.RelativeItem().PaddingLeft(SpacingLg).Column(info =>
            {
                info.Item().Text(title).FontSize(10.5f).SemiBold().FontColor("#173B70");
                info.Item().PaddingTop(SpacingSm).Text(subtitle).FontSize(9).FontColor("#455B75");
            });
            row.AutoItem().AlignMiddle().Column(value =>
            {
                value.Item().AlignRight().Text(right).FontSize(10.5f).SemiBold().FontColor("#173B70");
                if (!string.IsNullOrWhiteSpace(rightSub))
                    value.Item().PaddingTop(SpacingSm).AlignRight().Text(rightSub).FontSize(9).FontColor("#455B75");
            });
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
        container.Background(PaleBlue).CornerRadius(8).Padding(SpacingXl).Column(column =>
        {
            column.Spacing(SpacingSm);
            column.Item().Text("TOPLAM MALİYET ÖZETİ").FontSize(9.5f).SemiBold().FontColor(Navy);
            column.Item().Text(report.Explanation).FontSize(9).FontColor("#455B75").ClampLines(3);
            if (report.SelectedHotel.TaxiFareEstimate?.UsedDefaultFare == true)
            {
                column.Item().Text("Şehir tarifesi doğrulanamadığı için yapılandırılmış varsayılan ulaşım bedeli kullanıldı.")
                    .FontSize(9).FontColor("#76500D");
            }
            column.Item().PaddingTop(SpacingSm)
                .Text("Otel fiyatı ve ulaşım bedeli tahminidir; rezervasyon teklifi veya teyidi değildir.")
                .FontSize(9).Italic().FontColor("#53657A");
        });
    }
    private static void Metric(RowDescriptor row, MetricIcon icon, string label, string value)
    {
        row.RelativeItem().Background(PaleBlue).CornerRadius(6).Padding(SpacingMd).Column(metric =>
        {
            metric.Spacing(SpacingXs);
            metric.Item().Row(content =>
            {
                content.AutoItem().AlignMiddle().Width(13).Height(13).Svg(MetricIconSvg(icon));
                content.AutoItem().PaddingLeft(SpacingXs).AlignMiddle().Text(label).FontSize(6.5f)
                    .SemiBold().FontColor(AccentBlue);
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
            MetricIcon.Hotel => "<path d=\"M3 21V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v16M3 10h18M8 7h.01M12 7h.01M16 7h.01M8 13h.01M12 13h.01M16 13h.01M10 21v-4h4v4\"/>",
            MetricIcon.Airport => "<path d=\"m2 16 20-8-5-5-3 1 2 4-6 2-4-5-2 1 2 6-4 2z\"/><path d=\"M4 21h16\"/>",
            MetricIcon.Star => "<path d=\"m12 3 2.8 5.7 6.2.9-4.5 4.4 1.1 6.2-5.6-3-5.6 3 1.1-6.2L3 9.6l6.2-.9L12 3z\" fill=\"#176B46\" stroke=\"#176B46\"/>",
            MetricIcon.Route => "<circle cx=\"6\" cy=\"19\" r=\"2\"/><circle cx=\"18\" cy=\"5\" r=\"2\"/><path d=\"M8 19h2a4 4 0 0 0 4-4v-2a4 4 0 0 1 4-4\"/>",
            _ => throw new ArgumentOutOfRangeException(nameof(icon)),
        };

        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"{IconColor(icon)}\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{paths}</svg>";
    }

    private static string IconColor(MetricIcon icon) => icon switch
    {
        MetricIcon.Hotel => "#3478B8",
        MetricIcon.Car => "#168765",
        MetricIcon.Airport => "#5966C8",
        MetricIcon.Star => "#176B46",
        MetricIcon.Walking => "#3478B8",
        MetricIcon.Route => "#3478B8",
        _ => "#173B70",
    };

    private static string IconBackground(MetricIcon icon) => icon switch
    {
        MetricIcon.Hotel => "#E8F0FA",
        MetricIcon.Car => "#E7F2EC",
        MetricIcon.Airport => "#ECECFA",
        _ => "#EDF2F8",
    };

    private enum MetricIcon
    {
        Location,
        Car,
        Walking,
        Hotel,
        Airport,
        Star,
        Route,
    }

    private static string StrategyLabel(RecommendationStrategy strategy) => strategy switch
    {
        RecommendationStrategy.BudgetPriority => "BÜTÇE ÖNCELİKLİ",
        RecommendationStrategy.TransportPriority => "ULAŞIM ÖNCELİKLİ",
        RecommendationStrategy.Balanced => "DENGE",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    private static void ComposeMap(IContainer container, StaticMapImage map, RecommendationReport report)
    {
        container.Border(0.8f).BorderColor(Border).Background(Colors.White).CornerRadius(8).Padding(SpacingLg).Column(column =>
        {
            column.Spacing(SpacingMd);
            column.Item().Row(row =>
            {
                row.AutoItem().AlignMiddle().Width(16).Height(16).Svg(MetricIconSvg(MetricIcon.Location));
                row.RelativeItem().PaddingLeft(SpacingMd).Column(title =>
                {
                    title.Item().Text(report.PreferCityCenter
                            ? "Şehir Merkezi, Önerilen Otel ve Diğer Oteller"
                            : "Etkinlik Merkezi, Önerilen Otel ve Diğer Oteller")
                        .FontSize(11).SemiBold().FontColor(Navy);
                    title.Item().PaddingTop(SpacingXs).Text("Önerilen otel ve yakın çevredeki alternatif oteller")
                        .FontSize(9).FontColor("#455B75");
                });
            });
            column.Item().Image(map.Content.ToArray()).FitWidth();
            column.Item().Element(legend => ComposeMapLegend(legend, map, report));
        });
    }
    private static void ComposeMapLegend(IContainer container, StaticMapImage map, RecommendationReport report)
    {
        container.Row(row =>
        {
            row.Spacing(Spacing2Xl);
            LegendItem(row, "#DC2626", report.PreferCityCenter ? "Şehir merkezi" : "Etkinlik merkezi", LegendSymbol.DestinationPin);
            LegendItem(row, "#059669", "Önerilen otel", LegendSymbol.Star);
            LegendItem(row, Navy, "Diğer oteller", LegendSymbol.Number);
            row.RelativeItem();
            var attribution = string.IsNullOrWhiteSpace(map.Attribution) ? "Powered by Geoapify" : map.Attribution;
            row.AutoItem().AlignMiddle().Text(attribution).FontSize(8.5f).FontColor("#718096");
        });
    }

    private enum LegendSymbol
    {
        Square,
        DestinationPin,
        Number,
        Star,
    }

    private static void LegendItem(RowDescriptor row, string color, string label, LegendSymbol symbol)
    {
        row.AutoItem().Row(item =>
        {
            item.Spacing(SpacingSm);
            item.AutoItem().AlignMiddle().Element(container => DrawLegendSymbol(container, color, symbol));
            item.AutoItem().AlignMiddle().Text(label).FontSize(9);
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
                container.Width(20).Height(20).Svg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"20\" viewBox=\"0 0 20 20\"><path d=\"M10 19s7-6.2 7-11a7 7 0 1 0-14 0c0 4.8 7 11 7 11Z\" fill=\"" + color + "\"/><circle cx=\"10\" cy=\"8\" r=\"2.2\" fill=\"#fff\"/></svg>");
                break;
            case LegendSymbol.Number:
                container.Width(20).Height(20).Svg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"20\" viewBox=\"0 0 20 20\"><circle cx=\"10\" cy=\"10\" r=\"8\" fill=\"" + color + "\"/><path d=\"M9 7.5 10.6 6.7V13m-1.6 0h3.2\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.25\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>");
                break;
            case LegendSymbol.Star:
                container.Width(20).Height(20).Svg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"20\" viewBox=\"0 0 20 20\"><path d=\"m10 0 2.55 5.2 5.75.84-4.16 4.05 0.98 5.72L10 13.1l-5.12 2.71.98-5.72L1.7 6.04l5.75-.84L10 0Z\" fill=\"#059669\"/><path d=\"M9 7.5 10.6 6.7V13m-1.6 0h3.2\" fill=\"none\" stroke=\"#fff\" stroke-width=\"1.15\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>");
                break;
        }
    }

    private static void ComposeTable(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(SpacingSm);
            column.Item().Text("Oteller · Toplam konaklama ve ulaşım maliyetine göre sıralı")
                .FontSize(10).SemiBold().FontColor(Navy);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn(2.25f);
                    columns.RelativeColumn(0.95f);
                    columns.RelativeColumn(0.85f);
                    columns.RelativeColumn(0.65f);
                    columns.RelativeColumn(1.15f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.05f);
                    columns.RelativeColumn(1.35f);
                });

                table.Header(header =>
                {
                    GroupHeaderCell(header, "OTEL", 2);
                    GroupHeaderCell(header, report.PreferCityCenter ? "ŞEHİR MERKEZİ ROTASI" : "ETKİNLİK MERKEZİ ROTASI", 2);
                    GroupHeaderCell(header, "KONAKLAMA VE ULAŞIM MALİYETİ", 5);
                    HeaderCell(header, "#");
                    HeaderCell(header, "Otel");
                    HeaderCell(header, "Mesafe");
                    HeaderCell(header, "Süre");
                    HeaderCell(header, "Gece");
                    HeaderCell(header, "Gecelik ücret");
                    HeaderCell(header, "Şehir içi");
                    HeaderCell(header, "Havalimanı");
                    HeaderCell(header, "Toplam");
                });

                var tableRows = report.RankedHotels
                    .OrderBy(row => row.TripCostEstimate?.TotalCost?.Amount ?? decimal.MaxValue)
                    .ThenBy(row => row.DistanceKind)
                    .ThenBy(row => row.DistanceKind == DistanceKind.Road ? row.DurationSeconds : 0)
                    .ThenBy(row => row.DistanceMeters)
                    .ThenBy(row => row.Name, StringComparer.Ordinal)
                    .ToArray();

                for (var index = 0; index < tableRows.Length; index++)
                {
                    var row = tableRows[index];
                    var trip = row.TripCostEstimate;

                    var alternate = index % 2 == 1;
                    BodyCell(table, string.Create(CultureInfo.InvariantCulture, $"{index + 1}"), row.IsSelected, alternate);
                    BodyCell(table, row.Name, row.IsSelected, alternate);
                    BodyCell(table, FormatDistance(row.DistanceMeters), row.IsSelected, alternate);
                    BodyCell(table, FormatDuration(row), row.IsSelected, alternate);
                    BodyCell(table, trip is { } stay ? stay.StayNights.ToString(CultureInfo.InvariantCulture) : "—", row.IsSelected, alternate);
                    BodyCell(table, FormatTableAmount(row.Price), row.IsSelected, alternate);
                    BodyCell(table, trip?.LocalTransportSubtotal is { } local
                        ? FormatTableAmount(local)
                        : trip is null && row.TaxiFareEstimate is { } taxi ? FormatTableAmount(taxi.RoundTripCost) : "Hesaplanamadı",
                        row.IsSelected, alternate);
                    BodyCell(table, trip?.AirportRoundTripCost is { } airport ? FormatTableAmount(airport) : "Hesaplanamadı", row.IsSelected, alternate);
                    var total = trip is null ? row.TotalNightlyCost : trip.TotalCost;
                    BodyCell(table, total is { } totalCost ? FormatCardPrice(totalCost) : "Hesaplanamadı", row.IsSelected, alternate);
                }
            });
        });
    }

    private static void ComposeUnresolved(IContainer container, RecommendationReport report)
    {
        container.Column(column =>
        {
            column.Spacing(SpacingSm);
            column.Item().Text("Konumları belirlenemeyen oteller").FontSize(11).SemiBold().FontColor(Navy);
            if (report.UnresolvedHotelDetails.Count > 0)
            {
                foreach (var hotel in report.UnresolvedHotelDetails)
                {
                    column.Item().Background("#F7F9FC").CornerRadius(6).PaddingHorizontal(SpacingLg)
                        .PaddingVertical(SpacingSm).Row(row =>
                        {
                            row.RelativeItem().Text(hotel.Name).FontSize(9.5f).SemiBold().FontColor("#334B68");
                            row.AutoItem().AlignRight().Text(hotel.NightlyPrice is { } price
                                    ? $"Gecelik ücret: {FormatCardPrice(price)}"
                                    : "Gecelik ücret: Hesaplanamadı")
                                .FontSize(9).FontColor("#53657A");
                        });
                }
            }
            else
            {
                foreach (var hotelName in report.UnresolvedHotels)
                {
                    column.Item().Text($"{hotelName} · Gecelik ücret bilgisi alınamadı")
                        .FontSize(9).FontColor("#53657A");
                }
            }

            column.Item().Text("Bu otellerin konumu belirlenemediği için toplam maliyet sıralamasına dahil edilmedi.")
                .FontSize(9).FontColor("#53657A");
        });
    }

    private static void GroupHeaderCell(TableCellDescriptor header, string text, int span) =>
        header.Cell().ColumnSpan((uint)span).Background(Navy).Padding(SpacingSm)
            .Text(text).FontSize(9).SemiBold().FontColor(Colors.White);

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(PaleBlue).Padding(SpacingSm).Text(text).FontSize(9).SemiBold()
            .FontColor(Navy);

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

        cell.ShowEntire().Padding(SpacingSm).Text(text).FontSize(9);
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

    private static string FormatTableAmount(NightlyPrice price)
    {
        var amountFormat = price.Amount == decimal.Truncate(price.Amount) ? "#,##0" : "#,##0.00";
        return price.Amount.ToString(amountFormat, TurkishCulture);
    }

    /// <summary>Etkinlik alanı adından ASCII-güvenli, kültürden bağımsız bir dosya adı üretir.</summary>
    private static string BuildFileName(RecommendationReport report)
    {
        var personnel = report.Personnel;
        var parts = new[]
        {
            personnel?.RegistrationNumber ?? "sicil-yok",
            personnel?.FirstName ?? "isim-yok",
            personnel?.LastName ?? "soyisim-yok",
            report.CityName ?? "sehir-belirtilmedi",
            "konaklama-onerisi",
        };

        return $"{string.Join('-', parts.Select(Slugify))}.pdf";
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace('ı', 'i');
        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        var result = builder.ToString().Trim('-');
        while (result.Contains("--", StringComparison.Ordinal))
        {
            result = result.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrEmpty(result) ? "bilinmiyor" : result;
    }
}
