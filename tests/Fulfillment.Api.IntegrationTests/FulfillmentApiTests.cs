using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Fulfillment.Api;
using Fulfillment.Application;
using Fulfillment.Domain;
using Fulfillment.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fulfillment.Api.IntegrationTests;

public sealed class FulfillmentApiTests(FulfillmentApiFactory factory)
    : IClassFixture<FulfillmentApiFactory>
{
    [Fact]
    public async Task Request_without_identity_is_rejected()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/fulfillment-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Responses_include_browser_security_headers()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            "no-referrer",
            response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(
            "nosniff",
            response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains(
            "default-src 'self'",
            response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains(
            "frame-ancestors 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Create_is_idempotent_and_persists_outbox_event()
    {
        using var client = CreateClient("Operator");
        var idempotencyKey = $"test-{Guid.NewGuid():N}";
        var body = new CreateFulfillmentApiRequest(
            $"ORDER-{Guid.NewGuid():N}",
            "HR",
            "21000");

        using var first = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/fulfillment-requests")
        {
            Content = JsonContent.Create(body),
        };
        first.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstResponse = await client.SendAsync(first);
        var firstRequest = await firstResponse.Content
            .ReadFromJsonAsync<FulfillmentResponse>();

        using var replay = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/fulfillment-requests")
        {
            Content = JsonContent.Create(body),
        };
        replay.Headers.Add("Idempotency-Key", $"  {idempotencyKey}  ");

        var replayResponse = await client.SendAsync(replay);
        var replayedRequest = await replayResponse.Content
            .ReadFromJsonAsync<FulfillmentResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.NotNull(firstRequest);
        Assert.Equal(firstRequest.Id, replayedRequest?.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
        Assert.Contains(
            dbContext.OutboxMessages,
            message => message.Type == "fulfillment.requested.v1");
    }

    [Fact]
    public async Task Reusing_idempotency_key_for_different_payload_returns_conflict()
    {
        using var client = CreateClient("Operator");
        var idempotencyKey = $"test-{Guid.NewGuid():N}";
        var original = new CreateFulfillmentApiRequest(
            $"ORDER-{Guid.NewGuid():N}",
            "hr",
            "21000");

        using var first = CreateRequest(original, idempotencyKey);
        var firstResponse = await client.SendAsync(first);
        firstResponse.EnsureSuccessStatusCode();

        using var changed = CreateRequest(
            original with { DestinationPostalCode = "10000" },
            idempotencyKey);
        var response = await client.SendAsync(changed);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Idempotency key conflict", problem?.Title);
        Assert.Contains("different request payload", problem?.Detail);
    }

    [Fact]
    public async Task Create_metric_is_observable_with_bounded_outcome()
    {
        var measurement = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == Telemetry.MeterName
                    && instrument.Name == "fulfillment.create.requests")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, state) =>
            {
                var outcome = tags
                    .ToArray()
                    .Single(tag => tag.Key == "outcome")
                    .Value?
                    .ToString();
                if (instrument.Name == "fulfillment.create.requests"
                    && value == 1
                    && outcome is not null)
                {
                    measurement.TrySetResult(outcome);
                }
            });
        listener.Start();

        await CreateRequestAsync("Operator");

        Assert.Equal(
            "created",
            await measurement.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Operator_cannot_transition_request()
    {
        var created = await CreateRequestAsync("Operator");
        using var client = CreateClient("Operator");

        var response = await client.PostAsJsonAsync(
            $"/api/fulfillment-requests/{created.Id}/transitions",
            new TransitionFulfillmentApiRequest(
                FulfillmentStatus.Processing,
                created.Version));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Created_request_appears_in_paged_list()
    {
        var created = await CreateRequestAsync("Operator");
        using var client = CreateClient("Operator");

        var page = await client.GetFromJsonAsync<PagedResult<FulfillmentResponse>>(
            "/api/fulfillment-requests");

        Assert.NotNull(page);
        Assert.Contains(page.Items, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Supervisor_can_transition_and_stale_version_conflicts()
    {
        var created = await CreateRequestAsync("Operator");
        using var supervisor = CreateClient("Supervisor");

        var transitioned = await supervisor.PostAsJsonAsync(
            $"/api/fulfillment-requests/{created.Id}/transitions",
            new TransitionFulfillmentApiRequest(
                FulfillmentStatus.Processing,
                created.Version));

        var stale = await supervisor.PostAsJsonAsync(
            $"/api/fulfillment-requests/{created.Id}/transitions",
            new TransitionFulfillmentApiRequest(
                FulfillmentStatus.Cancelled,
                created.Version));

        Assert.Equal(HttpStatusCode.OK, transitioned.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Invalid_idempotency_key_returns_problem_details()
    {
        using var client = CreateClient("Operator");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/fulfillment-requests")
        {
            Content = JsonContent.Create(new CreateFulfillmentApiRequest(
                "ORDER-INVALID",
                "HR",
                "21000")),
        };
        request.Headers.Add("Idempotency-Key", "short");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("1!")]
    [InlineData("H1")]
    public async Task Invalid_country_code_returns_problem_details(
        string countryCode)
    {
        using var client = CreateClient("Operator");
        using var request = CreateRequest(
            new CreateFulfillmentApiRequest(
                "ORDER-INVALID-COUNTRY",
                countryCode,
                "21000"),
            $"test-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Invalid request", problem?.Title);
        Assert.Contains("two-letter ASCII country code", problem?.Detail);
    }

    [Fact]
    public async Task Rate_limit_is_partitioned_by_subject_and_returns_problem_details()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:PermitLimit", "1");
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
        });
        using var firstSubject = CreateClient(
            limitedFactory,
            "Operator",
            $"subject-a-{Guid.NewGuid():N}");
        using var secondSubject = CreateClient(
            limitedFactory,
            "Operator",
            $"subject-b-{Guid.NewGuid():N}");

        var accepted = await firstSubject.GetAsync("/api/fulfillment-requests");
        var rejected = await firstSubject.GetAsync("/api/fulfillment-requests");
        var otherSubject = await secondSubject.GetAsync("/api/fulfillment-requests");
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherSubject.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.True(rejected.Headers.RetryAfter?.Delta >= TimeSpan.FromSeconds(1));
        Assert.Equal(
            "application/problem+json",
            rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Rate limit exceeded", problem?.Title);
        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            problem?.Status);
        Assert.True(problem?.Extensions.ContainsKey("retryAfterSeconds"));
    }

    [Fact]
    public void Invalid_rate_limit_configuration_fails_during_startup()
    {
        using var invalidFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:PermitLimit", "0");
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
        });

        var exception = Assert.Throws<InvalidOperationException>(
            invalidFactory.CreateClient);

        Assert.Contains(
            "must both be greater than zero",
            exception.Message);
    }

    private HttpClient CreateClient(
        string role,
        string subject = "integration-test") =>
        CreateClient(factory, role, subject);

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> webFactory,
        string role,
        string subject)
    {
        var client = webFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, subject);
        return client;
    }

    private async Task<FulfillmentResponse> CreateRequestAsync(string role)
    {
        using var client = CreateClient(role);
        using var request = CreateRequest(
            new CreateFulfillmentApiRequest(
                $"ORDER-{Guid.NewGuid():N}",
                "HR",
                "21000"),
            $"test-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FulfillmentResponse>()
            ?? throw new InvalidOperationException("Response body was empty.");
    }

    private static HttpRequestMessage CreateRequest(
        CreateFulfillmentApiRequest body,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/fulfillment-requests")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }
}
