using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

public interface ITaxiFareProvider
{
    Task<TaxiFareQuote?> GetFareAsync(
        string city,
        string? country,
        string expectedCurrency,
        CancellationToken cancellationToken);
}
