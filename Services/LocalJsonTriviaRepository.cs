using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using d2c_launcher.Models;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

public sealed class LocalJsonTriviaRepository : ITriviaRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<TriviaQuestion[]> LoadAsync()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            const string resourceName = "d2c_launcher.Resources.trivia.json";
            await using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

            var root = await JsonSerializer.DeserializeAsync<TriviaRoot>(stream, Options)
                ?? throw new InvalidOperationException("Failed to deserialize trivia.json");

            return root.Questions ?? [];
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load trivia questions.", ex);
            return [];
        }
    }

    private sealed class TriviaRoot
    {
        public TriviaQuestion[]? Questions { get; set; }
    }
}
