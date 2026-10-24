using System.ComponentModel.DataAnnotations;

namespace RPAOtelRezervasyon.Application.Modules.TaxiFares;

public sealed class TaxiFareOptions
{
    public const string SectionName = "TaxiFare";

    public decimal DefaultOpeningFare { get; set; } = 71.94m;

    public decimal DefaultFarePerKm { get; set; } = 47.92m;

    [RegularExpression("^[A-Z]{3}$")]
    public string DefaultCurrency { get; set; } = "TRY";

    /// <summary>Fallback tariffs for hotel currencies other than DefaultCurrency.</summary>
    public Dictionary<string, TaxiFareRateOptions> DefaultRates { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EUR"] = new() { OpeningFare = 1.29m, FarePerKm = 0.86m },
        ["USD"] = new() { OpeningFare = 1.45m, FarePerKm = 0.97m },
    };

    [Range(1, 365)]
    public int MaxFareAgeDays { get; set; } = 180;
}

public sealed class TaxiFareRateOptions
{
    public decimal OpeningFare { get; set; }

    public decimal FarePerKm { get; set; }
}
