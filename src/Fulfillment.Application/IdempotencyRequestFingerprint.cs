using System.Security.Cryptography;
using System.Text;

namespace Fulfillment.Application;

/// <summary>
/// Produces a stable fingerprint for the normalized create-request semantics.
/// </summary>
public static class IdempotencyRequestFingerprint
{
    /// <summary>
    /// Computes a SHA-256 fingerprint after applying the same normalization as
    /// the fulfillment aggregate.
    /// </summary>
    public static string Compute(CreateFulfillmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fields = new[]
        {
            Normalize(command.ExternalReference, nameof(command.ExternalReference)),
            Normalize(
                command.DestinationCountryCode,
                nameof(command.DestinationCountryCode))
                .ToUpperInvariant(),
            Normalize(
                command.DestinationPostalCode,
                nameof(command.DestinationPostalCode)),
        };

        var canonical = string.Join(
            '\u001f',
            fields.Select(field => $"{field.Length}:{field}"));

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Fingerprint fields cannot be empty.",
                parameterName);
        }

        return value.Trim().Normalize(NormalizationForm.FormC);
    }
}
