using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationStudio.Core.Workflows;

/// <summary>
/// (De)serializes workflows to/from the JSON format fixed by section 9 of the design doc. This is
/// the one and only workflow file format — SQLite (Phase 2) stores the same JSON in a column
/// rather than re-modelling the workflow relationally, so import/export is always lossless.
/// </summary>
public static class WorkflowSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson(Workflow workflow) => JsonSerializer.Serialize(workflow, Options);

    public static Workflow FromJson(string json) =>
        JsonSerializer.Deserialize<Workflow>(json, Options)
        ?? throw new JsonException("Workflow JSON deserialized to null.");

    public static async Task<Workflow> LoadFromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Workflow>(stream, Options, cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException($"Workflow file '{path}' deserialized to null.");
    }

    public static async Task SaveToFileAsync(Workflow workflow, string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, workflow, Options, cancellationToken).ConfigureAwait(false);
    }
}
