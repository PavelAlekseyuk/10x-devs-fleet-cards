using System.Globalization;
using System.Security.Cryptography;
using FleetCards.Application;

namespace FleetCards.Infrastructure;

public sealed class MockCardProvider : ICardProvider
{
    public Task<CardProviderIssueResult> IssueAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pan = GeneratePan();
        var result = new CardProviderIssueResult(
            $"**** **** **** {pan[^4..]}",
            $"mock-card-{Guid.NewGuid():N}",
            new CardProviderMetadata("FleetCards Mock", "Virtual")
        );
        return Task.FromResult(result);
    }

    private static string GeneratePan()
    {
        var digits = new int[16];
        digits[0] = 4;
        for (var index = 1; index < digits.Length - 1; index++)
        {
            digits[index] = RandomNumberGenerator.GetInt32(0, 10);
        }

        var sum = 0;
        for (var index = 0; index < digits.Length - 1; index++)
        {
            var value = digits[index];
            if (index % 2 == 0)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
        }

        digits[^1] = (10 - sum % 10) % 10;
        return string.Concat(
            digits.Select(digit => digit.ToString(CultureInfo.InvariantCulture))
        );
    }
}