using System.Text.Json;

namespace DLLNelogica.Configuration;

internal sealed class JsonConfigurationLoader
{
    private const string ConfigurationFileName = "appsettings.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private readonly string _baseDirectory;

    internal JsonConfigurationLoader(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    internal ApplicationOptions Load()
    {
        var configurationPath = Path.Combine(_baseDirectory, ConfigurationFileName);
        var json = File.ReadAllText(configurationPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ConfigurationException("O arquivo appsettings.json está vazio.");
        }

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        ValidateShape(document.RootElement);

        var options = document.RootElement.Deserialize<ApplicationOptions>(SerializerOptions)
            ?? throw new ConfigurationException("O appsettings.json não pôde ser interpretado.");
        var issues = ConfigurationValidator.GetIssues(options);
        if (issues.Count > 0)
        {
            throw new ConfigurationException(string.Join(' ', issues));
        }

        return options;
    }

    private static void ValidateShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException("A raiz do appsettings.json deve ser um objeto JSON.");
        }

        var credentials = GetRequiredObject(root, "Credenciais");
        EnsureRequiredStringProperty(credentials, "Credenciais", "Key");
        EnsureRequiredStringProperty(credentials, "Credenciais", "User");
        EnsureRequiredStringProperty(credentials, "Credenciais", "Password");

        var marketData = GetRequiredObject(root, "MarketData");
        EnsureRequiredIntegerProperty(marketData, "MarketData", "ChannelCapacity");
        EnsureRequiredIntegerProperty(marketData, "MarketData", "HistoryCapacityPerInstrument");
        EnsureRequiredIntegerProperty(marketData, "MarketData", "ReportIntervalSeconds");
        ValidateInstruments(marketData);
    }

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new ConfigurationException($"A seção {propertyName} não foi encontrada no appsettings.json.");
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException($"A seção {propertyName} deve ser um objeto JSON.");
        }

        return property;
    }

    private static void EnsureRequiredStringProperty(
        JsonElement parent,
        string sectionName,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new ConfigurationException(
                $"A propriedade {sectionName}.{propertyName} não foi encontrada.");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new ConfigurationException(
                $"A propriedade {sectionName}.{propertyName} deve ser uma string.");
        }
    }

    private static void EnsureRequiredIntegerProperty(
        JsonElement parent,
        string sectionName,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new ConfigurationException(
                $"A propriedade {sectionName}.{propertyName} não foi encontrada.");
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out _))
        {
            throw new ConfigurationException(
                $"A propriedade {sectionName}.{propertyName} deve ser um inteiro.");
        }
    }

    private static void ValidateInstruments(JsonElement marketData)
    {
        if (!marketData.TryGetProperty("Instruments", out var instruments))
        {
            throw new ConfigurationException("A propriedade MarketData.Instruments não foi encontrada.");
        }

        if (instruments.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigurationException("A propriedade MarketData.Instruments deve ser uma lista.");
        }

        var index = 0;
        foreach (var instrument in instruments.EnumerateArray())
        {
            if (instrument.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigurationException(
                    $"O item MarketData.Instruments[{index}] deve ser um objeto JSON.");
            }

            EnsureRequiredStringProperty(instrument, $"MarketData.Instruments[{index}]", "Ticker");
            EnsureRequiredStringProperty(instrument, $"MarketData.Instruments[{index}]", "Exchange");
            index++;
        }
    }
}
