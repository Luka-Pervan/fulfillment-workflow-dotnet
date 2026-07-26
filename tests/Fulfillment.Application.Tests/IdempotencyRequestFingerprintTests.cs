using Fulfillment.Application;
using Xunit;

namespace Fulfillment.Application.Tests;

public sealed class IdempotencyRequestFingerprintTests
{
    [Fact]
    public void Equivalent_normalized_requests_have_the_same_fingerprint()
    {
        var first = IdempotencyRequestFingerprint.Compute(
            new CreateFulfillmentCommand(" ORDER-42 ", "hr", " 21000 "));
        var second = IdempotencyRequestFingerprint.Compute(
            new CreateFulfillmentCommand("ORDER-42", "HR", "21000"));

        Assert.Equal(second, first);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void A_changed_semantic_field_changes_the_fingerprint()
    {
        var first = IdempotencyRequestFingerprint.Compute(
            new CreateFulfillmentCommand("ORDER-42", "HR", "21000"));
        var second = IdempotencyRequestFingerprint.Compute(
            new CreateFulfillmentCommand("ORDER-42", "HR", "10000"));

        Assert.NotEqual(second, first);
    }
}
