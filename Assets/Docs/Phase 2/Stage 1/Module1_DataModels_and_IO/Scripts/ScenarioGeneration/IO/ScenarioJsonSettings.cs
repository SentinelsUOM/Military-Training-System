// =============================================================================
// ScenarioJsonSettings.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Centralised Newtonsoft.Json serialisation settings used by both
// ScenarioConfigLoader and ScenarioExporter. Ensures consistent formatting,
// enum handling, and null value treatment across all JSON I/O operations.
//
// Newtonsoft.Json is used instead of Unity's built-in JsonUtility because:
//   1. JsonUtility does not support Dictionary<string, T> (required for
//      navigationContext's additionalProperties pattern).
//   2. JsonUtility does not support nullable value types (int?) for seed
//      and timeLimit fields.
//   3. JsonUtility does not support custom enum serialisation with
//      snake_case string values.
// =============================================================================

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace TeamSentinels.ScenarioGeneration.IO
{
    /// <summary>
    /// Provides shared <see cref="JsonSerializerSettings"/> for all scenario
    /// JSON serialisation and deserialisation operations.
    /// </summary>
    public static class ScenarioJsonSettings
    {
        /// <summary>
        /// Standard settings for reading ScenarioConfig.json and Scenario.json.
        /// Uses camelCase property naming and string enum conversion.
        /// </summary>
        public static JsonSerializerSettings ReaderSettings => new JsonSerializerSettings
        {
            // Match JSON schema's camelCase field names
            ContractResolver = new CamelCasePropertyNamesContractResolver(),

            // Deserialise enum strings to C# enum values
            Converters = { new StringEnumConverter() },

            // Fail on missing required members to catch schema violations early
            MissingMemberHandling = MissingMemberHandling.Ignore,

            // Allow null values for nullable fields (seed, timeLimit, customLabel)
            NullValueHandling = NullValueHandling.Include,

            // Use ISO 8601 date format for generationTimestamp
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        /// <summary>
        /// Standard settings for writing Scenario.json output files.
        /// Uses indented formatting for human readability and camelCase naming.
        /// </summary>
        public static JsonSerializerSettings WriterSettings => new JsonSerializerSettings
        {
            // Match JSON schema's camelCase field names
            ContractResolver = new CamelCasePropertyNamesContractResolver(),

            // Serialise enum values as snake_case strings
            Converters = { new StringEnumConverter() },

            // Pretty-print for readability during development and debugging
            Formatting = Formatting.Indented,

            // Include null values explicitly (schema requires null fields to be present)
            NullValueHandling = NullValueHandling.Include,

            // Use ISO 8601 date format
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        /// <summary>
        /// Compact writer settings (no indentation) for production builds
        /// where file size matters. Same serialisation rules as
        /// <see cref="WriterSettings"/> but without indentation.
        /// </summary>
        public static JsonSerializerSettings CompactWriterSettings => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };
    }
}
