using d2c_launcher.Models;
using d2c_launcher.Services;
using Xunit;
using d2c_launcher.Tests.Fakes;
using NSubstitute;

namespace d2c_launcher.Tests;

public sealed class EmoticonSnapshotBuilderTests
{
    private static EmoticonData E(int id, string code) => new(id, code, "");

    private static EmoticonLoadResult ResultWith(params EmoticonData[] emoticons)
        => new() { Ordered = emoticons, Images = new Dictionary<string, byte[]>() };

    private static EmoticonLoadResult ResultWithImages(EmoticonData[] emoticons, Dictionary<string, byte[]> images)
        => new() { Ordered = emoticons, Images = images };

    // ── Concurrent calls load only once ──────────────────────────────────────

    [Fact]
    public async Task EnsureLoadedAsync_CalledConcurrently_LoadsOnlyOnce()
    {
        var tcs = new TaskCompletionSource<EmoticonLoadResult>();
        var service = Substitute.For<IEmoticonService>();
        service.LoadEmoticonsAsync().Returns(tcs.Task);

        var builder = new EmoticonSnapshotBuilder(service, new SyncDispatcher());

        var tasks = Enumerable.Range(0, 5).Select(_ => builder.EnsureLoadedAsync()).ToList();

        tcs.SetResult(ResultWith());
        await Task.WhenAll(tasks);

        await service.Received(1).LoadEmoticonsAsync();
    }

    // ── Second sequential call is a no-op ─────────────────────────────────────

    [Fact]
    public async Task EnsureLoadedAsync_CalledTwiceSequentially_LoadsOnlyOnce()
    {
        var service = Substitute.For<IEmoticonService>();
        service.LoadEmoticonsAsync().Returns(Task.FromResult(ResultWith()));

        var builder = new EmoticonSnapshotBuilder(service, new SyncDispatcher());

        await builder.EnsureLoadedAsync();
        await builder.EnsureLoadedAsync();

        await service.Received(1).LoadEmoticonsAsync();
    }

    // ── Top3 / All counts ─────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureLoadedAsync_FiveEmoticons_Top3HasThreeAllHasFive()
    {
        var emoticons = new[] { E(1,"a"), E(2,"b"), E(3,"c"), E(4,"d"), E(5,"e") };
        var service = Substitute.For<IEmoticonService>();
        service.LoadEmoticonsAsync().Returns(Task.FromResult(ResultWith(emoticons)));

        var builder = new EmoticonSnapshotBuilder(service, new SyncDispatcher());
        await builder.EnsureLoadedAsync();

        Assert.Equal(5, builder.All.Count);
        Assert.Equal(3, builder.Top3.Count);
        Assert.Equal(builder.All[0], builder.Top3[0]);
        Assert.Equal(builder.All[1], builder.Top3[1]);
        Assert.Equal(builder.All[2], builder.Top3[2]);
    }

    // ── Images linked into list entries ───────────────────────────────────────

    [Fact]
    public async Task EnsureLoadedAsync_ImagesLinkedToListEntries()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var emoticons = new[] { E(1, "wave"), E(2, "fire") };
        var images = new Dictionary<string, byte[]> { ["wave"] = bytes };

        var service = Substitute.For<IEmoticonService>();
        service.LoadEmoticonsAsync().Returns(Task.FromResult(ResultWithImages(emoticons, images)));

        var builder = new EmoticonSnapshotBuilder(service, new SyncDispatcher());
        await builder.EnsureLoadedAsync();

        Assert.Same(bytes, builder.All[0].GifBytes);  // "wave" → bytes present
        Assert.Null(builder.All[1].GifBytes);          // "fire" → no bytes
    }

    // ── Exception resets task so next call retries ────────────────────────────

    [Fact]
    public async Task EnsureLoadedAsync_FirstCallThrows_SecondCallRetries()
    {
        var service = Substitute.For<IEmoticonService>();
        service.LoadEmoticonsAsync().Returns(
            Task.FromException<EmoticonLoadResult>(new Exception("network error")),
            Task.FromResult(ResultWith(E(1, "ok"))));

        var builder = new EmoticonSnapshotBuilder(service, new SyncDispatcher());

        await builder.EnsureLoadedAsync(); // swallowed internally
        Assert.False(builder.IsLoaded);

        await builder.EnsureLoadedAsync(); // retries
        Assert.True(builder.IsLoaded);
        Assert.Single(builder.All);

        await service.Received(2).LoadEmoticonsAsync();
    }

    // ── SnapshotReady fires on dispatcher after success ───────────────────────

    [Fact]
    public async Task EnsureLoadedAsync_Success_RaisesSnapshotReady()
    {
        var service = Substitute.For<IEmoticonService>();
        service.LoadEmoticonsAsync().Returns(Task.FromResult(ResultWith()));

        var builder = new EmoticonSnapshotBuilder(service, new SyncDispatcher());
        var fired = 0;
        builder.SnapshotReady += () => fired++;

        await builder.EnsureLoadedAsync();

        Assert.Equal(1, fired);
    }
}
