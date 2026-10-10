namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §14. The file itself lives in <c>IFileStorage</c> under a Guid name.</summary>
public class FileAttachment : Entity
{
    public const long MaxSizeBytes = 25L * 1024 * 1024;

    public required string OriginalName { get; set; }
    public required string ContentType { get; set; }
    public required long SizeBytes { get; set; }

    /// <summary>Relative to the storage root; file name is a Guid, never the original name.</summary>
    public required string StoragePath { get; set; }

    /// <summary>Lowercase hex SHA-256 of the content.</summary>
    public required string Sha256 { get; set; }
}
