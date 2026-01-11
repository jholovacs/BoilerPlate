namespace BoilerPlate.Observability.Abstractions;

/// <summary>
///     Common metric units for standardized measurements
/// </summary>
public static class MetricUnits
{
    /// <summary>
    ///     Time units
    /// </summary>
    public static class Time
    {
        public const string Nanoseconds = "ns";
        public const string Microseconds = "μs";
        public const string Milliseconds = "ms";
        public const string Seconds = "s";
        public const string Minutes = "min";
        public const string Hours = "h";
        public const string Days = "d";
    }

    /// <summary>
    ///     Data size units
    /// </summary>
    public static class Data
    {
        public const string Bytes = "bytes";
        public const string Kilobytes = "KB";
        public const string Megabytes = "MB";
        public const string Gigabytes = "GB";
        public const string Terabytes = "TB";
    }

    /// <summary>
    ///     Network units
    /// </summary>
    public static class Network
    {
        public const string BitsPerSecond = "bps";
        public const string BytesPerSecond = "B/s";
        public const string Packets = "packets";
        public const string Connections = "connections";
    }

    /// <summary>
    ///     Count units
    /// </summary>
    public static class Count
    {
        public const string Requests = "requests";
        public const string Errors = "errors";
        public const string Operations = "operations";
        public const string Items = "items";
        public const string Events = "events";
        public const string Messages = "messages";
        public const string Transactions = "transactions";
    }

    /// <summary>
    ///     Rate units
    /// </summary>
    public static class Rate
    {
        public const string PerSecond = "/s";
        public const string PerMinute = "/min";
        public const string PerHour = "/h";
        public const string PerDay = "/d";
    }

    /// <summary>
    ///     Percentage unit
    /// </summary>
    public const string Percent = "%";

    /// <summary>
    ///     Dimensionless unit (no unit)
    /// </summary>
    public const string Dimensionless = "1";
}
