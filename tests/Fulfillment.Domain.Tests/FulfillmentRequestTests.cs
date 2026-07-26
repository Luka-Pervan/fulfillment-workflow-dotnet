using Fulfillment.Domain;
using Xunit;

namespace Fulfillment.Domain.Tests;

public sealed class FulfillmentRequestTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_country_and_starts_submitted()
    {
        var request = Create();

        Assert.Equal(FulfillmentStatus.Submitted, request.Status);
        Assert.Equal("HR", request.DestinationCountryCode);
        Assert.Equal(1, request.Version);
        Assert.Equal(Now, request.CreatedAt);
    }

    [Theory]
    [InlineData("1!")]
    [InlineData("H1")]
    [InlineData("HRV")]
    public void Create_rejects_invalid_country_code(string countryCode)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FulfillmentRequest.Create(
                "request-12345678",
                new string('A', 64),
                "ORDER-42",
                countryCode,
                "21000",
                Now));

        Assert.Equal("destinationCountryCode", exception.ParamName);
    }

    [Theory]
    [InlineData(FulfillmentStatus.Processing)]
    [InlineData(FulfillmentStatus.Cancelled)]
    public void Submitted_request_allows_expected_transitions(
        FulfillmentStatus next)
    {
        var request = Create();

        request.TransitionTo(next, Now.AddMinutes(1));

        Assert.Equal(next, request.Status);
        Assert.Equal(2, request.Version);
        Assert.Equal(Now.AddMinutes(1), request.UpdatedAt);
    }

    [Fact]
    public void Processing_request_can_complete()
    {
        var request = Create();
        request.TransitionTo(FulfillmentStatus.Processing, Now.AddMinutes(1));

        request.TransitionTo(FulfillmentStatus.Completed, Now.AddMinutes(2));

        Assert.Equal(FulfillmentStatus.Completed, request.Status);
        Assert.Equal(3, request.Version);
    }

    [Theory]
    [InlineData(FulfillmentStatus.Submitted)]
    [InlineData(FulfillmentStatus.Completed)]
    public void Submitted_request_rejects_invalid_transition(
        FulfillmentStatus next)
    {
        var request = Create();

        var exception = Assert.Throws<InvalidStateTransitionException>(
            () => request.TransitionTo(next, Now.AddMinutes(1)));

        Assert.Equal(FulfillmentStatus.Submitted, exception.Current);
        Assert.Equal(next, exception.Requested);
        Assert.Equal(1, request.Version);
    }

    [Fact]
    public void Terminal_request_rejects_all_further_transitions()
    {
        var request = Create();
        request.TransitionTo(FulfillmentStatus.Cancelled, Now.AddMinutes(1));

        Assert.Throws<InvalidStateTransitionException>(
            () => request.TransitionTo(
                FulfillmentStatus.Processing,
                Now.AddMinutes(2)));
    }

    private static FulfillmentRequest Create() =>
        FulfillmentRequest.Create(
            "request-12345678",
            new string('A', 64),
            "ORDER-42",
            "hr",
            "21000",
            Now);
}
