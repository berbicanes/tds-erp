using System.Security.Cryptography;
using System.Text;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Unit;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tds-storage-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests() =>
        _storage = new LocalFileStorage(_root, new FixedTimeProvider(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero)));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Save_stores_under_guid_name_with_size_and_sha256()
    {
        var bytes = Encoding.UTF8.GetBytes("crtež rev B");

        var stored = await _storage.SaveAsync(new MemoryStream(bytes), 1024, TestContext.Current.CancellationToken);

        Assert.Matches(@"^2026/10/[0-9a-f]{32}$", stored.StoragePath);
        Assert.Equal(bytes.Length, stored.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), stored.Sha256);
        await using var read = _storage.OpenRead(stored.StoragePath);
        using var ms = new MemoryStream();
        await read.CopyToAsync(ms, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, ms.ToArray());
    }

    [Fact]
    public async Task Save_above_limit_is_rejected_and_leaves_no_file()
    {
        await Assert.ThrowsAsync<FileTooLargeException>(() =>
            _storage.SaveAsync(new MemoryStream(new byte[11]), 10, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Save_exactly_at_limit_is_allowed()
    {
        var stored = await _storage.SaveAsync(new MemoryStream(new byte[10]), 10, TestContext.Current.CancellationToken);
        Assert.Equal(10, stored.SizeBytes);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/etc/passwd")]
    [InlineData("2026/../../outside")]
    public void Paths_outside_root_are_rejected(string path) =>
        Assert.Throws<InvalidOperationException>(() => _storage.OpenRead(path));

    [Fact]
    public async Task DeleteUncommitted_removes_file()
    {
        var stored = await _storage.SaveAsync(new MemoryStream([1, 2, 3]), 10, TestContext.Current.CancellationToken);
        _storage.DeleteUncommitted(stored.StoragePath);
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }
}
