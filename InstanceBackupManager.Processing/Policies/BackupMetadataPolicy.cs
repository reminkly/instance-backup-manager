namespace InstanceBackupManager.Processing.Policies;

/// <summary>
/// Normalizes and validates optional notes and tags stored as backup presentation metadata.
/// </summary>
public static class BackupMetadataPolicy
{
    #region Constants

    public const int MaximumNotesLength = 500;

    public const int MaximumTagCount = 10;

    public const int MaximumTagLength = 30;

    #endregion

    #region Public Methods

    /// <summary>
    /// Normalizes optional notes and rejects unsupported control characters or excessive length.
    /// </summary>
    public static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var normalizedNotes = notes.Trim();

        if (normalizedNotes.Length > MaximumNotesLength)
        {
            throw new ArgumentException(
                $"Backup notes cannot exceed {MaximumNotesLength} characters.",
                nameof(notes)
            );
        }

        if (normalizedNotes.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException(
                "Backup notes cannot contain control characters.",
                nameof(notes)
            );
        }

        return normalizedNotes;
    }

    /// <summary>
    /// Trims, validates, and case-insensitively deduplicates optional backup tags.
    /// </summary>
    public static IReadOnlyCollection<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        var normalizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTags.Count > MaximumTagCount)
        {
            throw new ArgumentException(
                $"A backup cannot contain more than {MaximumTagCount} tags.",
                nameof(tags)
            );
        }

        var invalidTag = normalizedTags.FirstOrDefault(
            tag => tag.Length > MaximumTagLength
                || tag.Any(character => char.IsControl(character) || character == ',')
        );

        if (invalidTag is not null)
        {
            throw new ArgumentException(
                $"Backup tag '{invalidTag}' is invalid. Tags cannot exceed {MaximumTagLength} characters or contain commas or control characters.",
                nameof(tags)
            );
        }

        return normalizedTags.AsReadOnly();
    }

    #endregion
}
