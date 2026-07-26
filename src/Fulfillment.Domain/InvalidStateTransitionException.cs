namespace Fulfillment.Domain;

public sealed class InvalidStateTransitionException(
    FulfillmentStatus current,
    FulfillmentStatus requested)
    : InvalidOperationException($"Cannot transition a fulfillment request from {current} to {requested}.")
{
    public FulfillmentStatus Current { get; } = current;

    public FulfillmentStatus Requested { get; } = requested;
}
