using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using FleetCards.Contracts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetCards.Api.Tests;

public sealed class PlatformTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public PlatformTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Api_root_returns_api_information()
    {
        using var response = await client.GetAsync("/api");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fleet Cards API", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/cards/123")]
    public async Task Spa_routes_return_the_web_application(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Fleet Cards", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/not-found")]
    [InlineData("/health/not-found")]
    [InlineData("/missing.js")]
    public async Task Api_health_and_asset_paths_do_not_fall_back_to_the_spa(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/client")]
    [InlineData("/api/cars")]
    [InlineData("/api/cards")]
    [InlineData("/api/cards/22222222-2222-2222-2222-222222222222/state-history")]
    [InlineData("/api/transactions/report?year=2026&month=9&timeZone=Europe%2FWarsaw")]
    [InlineData("/api/dashboard/summary?year=2026&month=9&timeZone=Europe%2FWarsaw")]
    [InlineData("/api/programs")]
    public async Task Anonymous_client_data_requests_are_rejected(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Csrf_is_required_for_login()
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("anna.nowak", "Anna123!")
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_csrf_tokens_fail_closed()
    {
        using var csrfClient = factory.CreateClient();
        csrfClient.DefaultRequestHeaders.Add("Cookie", "XSRF-TOKEN=malformed");
        csrfClient.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "different");

        using var response = await csrfClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("anna.nowak", "Anna123!")
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("anna.nowak", "Anna123!", "ReadWrite")]
    [InlineData("piotr.wisniewski", "Piotr123!", "ReadOnly")]
    public async Task Valid_login_returns_the_role_and_current_user(
        string userName,
        string password,
        string permission
    )
    {
        using var authenticatedClient = await CreateCsrfClient();

        using var login = await authenticatedClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(userName, password)
        );
        using var me = await authenticatedClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(
            new CurrentUserResponse(userName, permission),
            await me.Content.ReadFromJsonAsync<CurrentUserResponse>()
        );
    }

    [Fact]
    public async Task Invalid_login_responses_do_not_reveal_account_existence()
    {
        using var unknownClient = await CreateCsrfClient();
        using var knownClient = await CreateCsrfClient();
        using var unknown = await unknownClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown.user", "wrong-password")
        );
        using var known = await knownClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("anna.nowak", "wrong-password")
        );

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await known.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task Repeated_invalid_logins_are_throttled()
    {
        using var authenticatedClient = await CreateCsrfClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failedLogin = await authenticatedClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("throttle.user", "wrong-password")
            );
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        using var throttledLogin = await authenticatedClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("throttle.user", "wrong-password")
        );

        Assert.Equal(HttpStatusCode.TooManyRequests, throttledLogin.StatusCode);
    }

    [Fact]
    public async Task Authentication_logs_use_correlation_and_omit_sensitive_values()
    {
        var provider = new CapturingLoggerProvider();
        using var testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider))
        );
        using var testClient = await CreateCsrfClient(testFactory);

        using var response = await testClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("anna.nowak", "Anna123!")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authenticationLogs = provider
            .Messages.Where(message => message.Contains("Authentication", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(authenticationLogs);
        Assert.All(
            authenticationLogs,
            message =>
            {
                Assert.Contains("correlation", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Anna123!", message, StringComparison.Ordinal);
                Assert.DoesNotContain("FleetCards.Session", message, StringComparison.Ordinal);
                Assert.DoesNotContain("4242", message, StringComparison.Ordinal);
                Assert.DoesNotContain("CVV", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("PIN", message, StringComparison.OrdinalIgnoreCase);
            }
        );
    }

    [Fact]
    public async Task Read_only_users_can_read_but_cannot_access_programs()
    {
        using var authenticatedClient = await CreateCsrfClient();
        using var login = await authenticatedClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("piotr.wisniewski", "Piotr123!")
        );

        using var clientResponse = await authenticatedClient.GetAsync("/api/client");
        using var programsResponse = await authenticatedClient.GetAsync("/api/programs");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, programsResponse.StatusCode);
    }

    [Fact]
    public async Task Read_write_users_can_access_programs_and_logout_clears_session()
    {
        using var authenticatedClient = await CreateCsrfClient();
        using var login = await authenticatedClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("anna.nowak", "Anna123!")
        );
        using var programs = await authenticatedClient.GetAsync("/api/programs");
        using var logout = await authenticatedClient.PostAsync("/api/auth/logout", null);
        using var afterLogout = await authenticatedClient.GetAsync("/api/client");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, programs.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Session_cookie_uses_eight_hour_sliding_configuration()
    {
        var options = factory
            .Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Health_endpoints_remain_public(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Migration_startup_is_enabled_for_development_but_disabled_in_base_configuration()
    {
        var developmentConfiguration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.True(developmentConfiguration.GetValue<bool>("Database:ApplyMigrations"));

        var baseConfiguration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        Assert.False(baseConfiguration.GetValue<bool>("Database:ApplyMigrations"));
    }

    private async Task<HttpClient> CreateCsrfClient(
        WebApplicationFactory<Program>? clientFactory = null
    )
    {
        var csrfClient = (clientFactory ?? factory).CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        using var response = await csrfClient.GetAsync("/api/auth/csrf");
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        var token = setCookie[..setCookie.IndexOf(';')].Split('=', 2)[1];
        csrfClient.DefaultRequestHeaders.Add("Cookie", $"XSRF-TOKEN={token}");
        csrfClient.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        return csrfClient;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        internal ConcurrentBag<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentBag<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => messages.Add(formatter(state, exception));
    }
}
