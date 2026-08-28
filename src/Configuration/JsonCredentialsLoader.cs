using System.Text.Json;

namespace DLLNelogica.Configuration;

internal sealed class JsonCredentialsLoader
{
    private const string ConfigurationFileName = "appsettings.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private readonly string _baseDirectory;

    internal JsonCredentialsLoader(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    internal CredentialsOptions Load()
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
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException("A raiz do appsettings.json deve ser um objeto JSON.");
        }

        if (!root.TryGetProperty("Credenciais", out var credentialsElement))
        {
            throw new ConfigurationException("A seção Credenciais não foi encontrada no appsettings.json.");
        }

        if (credentialsElement.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException("A seção Credenciais deve ser um objeto JSON.");
        }

        EnsureRequiredStringProperty(credentialsElement, "Key");
        EnsureRequiredStringProperty(credentialsElement, "User");
        EnsureRequiredStringProperty(credentialsElement, "Password");

        var credentials = credentialsElement.Deserialize<CredentialsOptions>(SerializerOptions)
            ?? throw new ConfigurationException("A seção Credenciais não pôde ser interpretada.");

        var issues = ConfigurationValidator.GetIssues(credentials);
        if (issues.Count > 0)
        {
            throw new ConfigurationException(string.Join(' ', issues));
        }

        return credentials;
    }

    private static void EnsureRequiredStringProperty(JsonElement credentials, string propertyName)
    {
        if (!credentials.TryGetProperty(propertyName, out var property))
        {
            throw new ConfigurationException($"A propriedade Credenciais.{propertyName} não foi encontrada.");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new ConfigurationException($"A propriedade Credenciais.{propertyName} deve ser uma string.");
        }
    }
}
