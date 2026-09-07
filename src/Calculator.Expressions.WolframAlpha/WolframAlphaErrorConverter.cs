using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Reads the one field of an answer that is not always the same shape.
/// </summary>
/// <remarks>
/// The service writes <c>error</c> two ways under the one name: as the flag
/// <see langword="false"/> on an answer where nothing went wrong, and as an object saying what
/// where something did. Only the object is an error, and the flag is read as none rather than as
/// something that could not be read.
/// </remarks>
internal sealed class WolframAlphaErrorConverter : JsonConverter<WolframAlphaError?>
{
    public override WolframAlphaError? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        // This converter is asked for by the one property that needs it rather than registered for
        // the type, so reading the object is the ordinary reading of it and not this again.
        return JsonSerializer.Deserialize<WolframAlphaError>(ref reader, options);
    }

    public override void Write(
        Utf8JsonWriter writer,
        WolframAlphaError? value,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
