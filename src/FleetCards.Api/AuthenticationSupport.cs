using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FleetCards.Api;

internal static class CsrfProtection
{
    internal const string CookieName = "XSRF-TOKEN";
    internal const string HeaderName = "X-XSRF-TOKEN";

    internal static string Issue(HttpContext context)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        context.Response.Cookies.Append(
            CookieName,
            token,
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                IsEssential = true,
            }
        );
        return token;
    }

    internal static bool IsValid(HttpContext context)
    {
        if (
            !context.Request.Cookies.TryGetValue(CookieName, out var cookieToken)
            || !context.Request.Headers.TryGetValue(HeaderName, out var headerToken)
        )
        {
            return false;
        }

        try
        {
            var cookieBytes = Encoding.ASCII.GetBytes(cookieToken);
            var headerBytes = Encoding.ASCII.GetBytes(headerToken.ToString());
            return CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal sealed class LoginAttemptTracker
{
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(1);
    private const int MaxFailures = 5;
    private readonly ConcurrentDictionary<string, AttemptState> attempts = new(
        StringComparer.OrdinalIgnoreCase
    );

    internal bool IsThrottled(string key)
    {
        if (!attempts.TryGetValue(key, out var state))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (state.BlockedUntil > now)
        {
            return true;
        }

        if (now - state.WindowStarted > AttemptWindow)
        {
            attempts.TryRemove(key, out _);
        }

        return false;
    }

    internal void RecordFailure(string key)
    {
        var now = DateTimeOffset.UtcNow;
        attempts.AddOrUpdate(
            key,
            _ => new AttemptState(now, 1, DateTimeOffset.MinValue),
            (_, previous) =>
            {
                if (now - previous.WindowStarted > AttemptWindow)
                {
                    return new AttemptState(now, 1, DateTimeOffset.MinValue);
                }

                var failures = previous.Failures + 1;
                var blockedUntil =
                    failures >= MaxFailures ? now.Add(LockoutDuration) : previous.BlockedUntil;
                return previous with { Failures = failures, BlockedUntil = blockedUntil };
            }
        );
    }

    internal void Reset(string key) => attempts.TryRemove(key, out _);

    private sealed record AttemptState(
        DateTimeOffset WindowStarted,
        int Failures,
        DateTimeOffset BlockedUntil
    );
}
