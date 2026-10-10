using System.Security.Cryptography;

namespace Tds.Api.Infrastructure;

public sealed record StoredFile(string StoragePath, long SizeBytes, string Sha256);

/// <summary>File content storage. Names on disk are Guids, never the user's file name.</summary>
public interface IFileStorage
{
    /// <summary>Stores the stream. Throws <see cref="FileTooLargeException"/> above <paramref name="maxBytes"/>.</summary>
    Task<StoredFile> SaveAsync(Stream content, long maxBytes, CancellationToken ct);

    Stream OpenRead(string storagePath);

    /// <summary>Only for cleaning up content whose database row was never committed.</summary>
    void DeleteUncommitted(string storagePath);
}

public sealed class FileTooLargeException(long maxBytes)
    : Exception($"File is larger than {maxBytes} bytes.");

public sealed class FileStorageOptions
{
    public const string Section = "FileStorage";
    public string Root { get; set; } = "";
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly TimeProvider _time;

    public LocalFileStorage(string root, TimeProvider time)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("FileStorage:Root is not configured.");
        _root = Path.GetFullPath(root);
        _time = time;
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, long maxBytes, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var relative = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), Guid.CreateVersion7().ToString("N"));
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        var buffer = new byte[81920];
        try
        {
            await using var file = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                if (size > maxBytes)
                    throw new FileTooLargeException(maxBytes);
                sha.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch
        {
            File.Delete(full);
            throw;
        }

        return new StoredFile(relative.Replace('\\', '/'), size, Convert.ToHexStringLower(sha.GetHashAndReset()));
    }

    public Stream OpenRead(string storagePath) =>
        new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public void DeleteUncommitted(string storagePath) => File.Delete(Resolve(storagePath));

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Storage path is outside the storage root.");
        return full;
    }
}
