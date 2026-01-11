using System.Text;
using System.Text.RegularExpressions;

namespace BoilerPlate.ServiceBus.RabbitMq.Helpers;

/// <summary>
///     Helper class for sanitizing names to be valid RabbitMQ queue/exchange names
/// </summary>
public static class RabbitMqNameSanitizer
{
    private const int MaxLength = 255;

    // RabbitMQ allows: letters, digits, hyphens, underscores, periods, colons
    // Maximum length: 255 bytes (UTF-8)
    private static readonly Regex InvalidCharacters = new(@"[^a-zA-Z0-9._:-]", RegexOptions.Compiled);

    /// <summary>
    ///     Sanitizes a name to be valid for RabbitMQ queues and exchanges
    /// </summary>
    /// <param name="name">The name to sanitize</param>
    /// <returns>A sanitized name that is valid for RabbitMQ</returns>
    /// <remarks>
    ///     Rules applied:
    ///     - Removes or replaces invalid characters (keeps only letters, digits, hyphens, underscores, periods, colons)
    ///     - Replaces invalid characters with hyphens
    ///     - Removes leading/trailing periods
    ///     - Truncates to 255 bytes if necessary
    ///     - Ensures name is not empty
    /// </remarks>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or empty", nameof(name));

        // Replace invalid characters with hyphens
        var sanitized = InvalidCharacters.Replace(name, "-");

        // Remove consecutive hyphens
        sanitized = Regex.Replace(sanitized, @"-+", "-");

        // Remove leading and trailing periods, hyphens, and underscores
        sanitized = sanitized.Trim('.', '-', '_');

        // Ensure not empty after sanitization
        if (string.IsNullOrEmpty(sanitized)) sanitized = "default";

        // Truncate to max length (255 bytes)
        // We need to count bytes, not characters, for UTF-8
        var bytes = Encoding.UTF8.GetBytes(sanitized);
        if (bytes.Length > MaxLength)
        {
            // Truncate byte array and convert back to string
            var truncatedBytes = new byte[MaxLength];
            Array.Copy(bytes, truncatedBytes, MaxLength);

            // Find the last valid UTF-8 character boundary
            var validLength = MaxLength;
            while (validLength > 0 && (truncatedBytes[validLength - 1] & 0xC0) == 0x80) validLength--;

            sanitized = Encoding.UTF8.GetString(truncatedBytes, 0, validLength);

            // Remove trailing invalid characters that might have been created
            sanitized = sanitized.TrimEnd('.', '-', '_');
        }

        // Final check - ensure not empty
        if (string.IsNullOrEmpty(sanitized)) sanitized = "default";

        return sanitized;
    }

    /// <summary>
    ///     Validates if a name is valid for RabbitMQ
    /// </summary>
    /// <param name="name">The name to validate</param>
    /// <returns>True if the name is valid, false otherwise</returns>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        // Check for invalid characters
        if (InvalidCharacters.IsMatch(name)) return false;

        // Check length (bytes, not characters)
        var bytes = Encoding.UTF8.GetBytes(name);
        if (bytes.Length > MaxLength) return false;

        // Check for leading/trailing periods
        if (name.StartsWith('.') || name.EndsWith('.')) return false;

        return true;
    }
}