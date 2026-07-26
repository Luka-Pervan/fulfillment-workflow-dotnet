using System.Text.Json.Serialization;

namespace Fulfillment.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FulfillmentStatus
{
    Submitted = 1,
    Processing = 2,
    Completed = 3,
    Cancelled = 4,
}
