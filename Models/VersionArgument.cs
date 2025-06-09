// Models/VersionArgument.cs

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianLauncher.Models;

/// <summary>
///     Represents a single game or JVM argument, which can be a plain string
///     or a conditional argument with rules.
///     This class uses a custom JsonConverter to handle the variant type.
/// </summary>
[JsonConverter(typeof(VersionArgumentConverter))]
public class VersionArgument
{
    // Private constructors, only used by the converter
    private VersionArgument(string? plainValue)
    {
        PlainStringValue = plainValue;
        ConditionalValue = null;
    }

    private VersionArgument(ConditionalArgumentValue? conditionalValue)
    {
        PlainStringValue = null;
        ConditionalValue = conditionalValue;
    }

    public string? PlainStringValue { get; }
    public ConditionalArgumentValue? ConditionalValue { get; }

    public bool IsPlainString => PlainStringValue != null;
    public bool IsConditional => ConditionalValue != null;

    // Static factory methods for the converter to use
    public static VersionArgument Create(string? plainValue)
    {
        return new VersionArgument(plainValue);
    }

    public static VersionArgument Create(ConditionalArgumentValue? conditionalValue)
    {
        return new VersionArgument(conditionalValue);
    }
}

public class VersionArgumentConverter : JsonConverter<VersionArgument>
{
    public override VersionArgument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return VersionArgument.Create(reader.GetString());

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Deserialize the object as ConditionalArgumentValue
            // We need to be careful here not to consume the reader in a way that breaks the outer deserializer
            // if options are passed that affect how objects are read.
            // A simpler way is to use JsonSerializer.Deserialize on the current element if possible.
            var conditionalValue = JsonSerializer.Deserialize<ConditionalArgumentValue>(ref reader, options);
            return VersionArgument.Create(conditionalValue);
        }

        throw new JsonException("Expected string or object for VersionArgument");
    }

    public override void Write(Utf8JsonWriter writer, VersionArgument value, JsonSerializerOptions options)
    {
        if (value.IsPlainString)
            writer.WriteStringValue(value.PlainStringValue);
        else if (value.IsConditional)
            JsonSerializer.Serialize(writer, value.ConditionalValue, options);
        else
            writer.WriteNullValue(); // Or throw exception if null is not valid
    }
}