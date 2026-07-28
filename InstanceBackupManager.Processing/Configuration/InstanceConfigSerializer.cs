using System.Text.Json;
using System.Text.Json.Serialization;
using InstanceBackupManager.Processing.Models.Configuration;

namespace InstanceBackupManager.Processing.Configuration;

/// <summary>
/// Reads and writes instance configuration files using the application's supported JSON format.
/// </summary>
internal sealed class InstanceConfigSerializer
{
    #region Properties

    /// <summary>
    /// Gets the serializer options used when reading and writing instance configuration files.
    /// </summary>
    private JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    #endregion

    #region Internal Methods

    /// <summary>
    /// Reads and deserializes an instance configuration file.
    /// </summary>
    /// <param name="configPath">The absolute path of the configuration file.</param>
    /// <returns>The deserialized instance configuration.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="configPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the configuration contains no deserializable value.</exception>
    internal InstanceConfig Load(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "The instance configuration file was not found.",
                configPath
            );
        }

        var json = File.ReadAllText(configPath);

        return JsonSerializer.Deserialize<InstanceConfig>(
            json,
            JsonOptions
        ) ?? throw new InvalidDataException(
            $"Configuration '{configPath}' contained no data."
        );
    }

    /// <summary>
    /// Serializes and writes an instance configuration without overwriting an existing file.
    /// </summary>
    /// <param name="configPath">The absolute destination path of the configuration file.</param>
    /// <param name="config">The configuration to serialize.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="configPath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="IOException">Thrown when the destination file already exists.</exception>
    internal void Create(
        string configPath,
        InstanceConfig config
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(config);

        if (File.Exists(configPath))
        {
            throw new IOException(
                $"Configuration already exists at '{configPath}'."
            );
        }

        var parentPath = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            Directory.CreateDirectory(parentPath);
        }

        var json = JsonSerializer.Serialize(
            config,
            JsonOptions
        );

        File.WriteAllText(
            configPath,
            json
        );
    }

    #endregion
}