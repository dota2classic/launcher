using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using d2c_launcher.Services;
using d2c_launcher.Tests.Fakes;
using NSubstitute;

namespace d2c_launcher.Tests;

public sealed class UserNameResolverTests
{
    // ── GetOrCreate returns same instance for same steamId ────────────────────

    [Fact]
    public void GetOrCreate_SameId_ReturnsSameInstance()
    {
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
           .Returns(System.Threading.Tasks.Task.FromResult<(string?, string?)?>(null));

        var resolver = new UserNameResolver(api, new SyncDispatcher());

        var vm1 = resolver.GetOrCreate("42");
        var vm2 = resolver.GetOrCreate("42");

        Assert.Same(vm1, vm2);
    }

    // ── Only one API call regardless of how many times GetOrCreate is called ──

    [Fact]
    public async Task GetOrCreate_SameId_CalledTwice_OnlyOneApiCall()
    {
        var tcs = new TaskCompletionSource<(string?, string?)?>();
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
           .Returns(tcs.Task);

        var resolver = new UserNameResolver(api, new SyncDispatcher());

        resolver.GetOrCreate("42");
        resolver.GetOrCreate("42");

        tcs.SetResult(("Alice", null));
        await Task.Delay(50);

        await api.Received(1).GetUserInfoAsync("42", Arg.Any<System.Threading.CancellationToken>());
    }

    // ── DisplayName updates to resolved name ──────────────────────────────────

    [Fact]
    public async Task GetOrCreate_ApiReturnsName_DisplayNameUpdates()
    {
        // Use a TCS so we can subscribe to PropertyChanged before the name resolves.
        var tcs = new TaskCompletionSource<(string?, string?)?>();
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync("7", Arg.Any<System.Threading.CancellationToken>())
           .Returns(tcs.Task);

        var resolver = new UserNameResolver(api, new SyncDispatcher());
        var vm = resolver.GetOrCreate("7");

        var nameChanged = new TaskCompletionSource();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerNameViewModel.DisplayName))
                nameChanged.TrySetResult();
        };

        tcs.SetResult(("Sasha", null));
        await nameChanged.Task.WaitAsync(System.TimeSpan.FromSeconds(2));

        Assert.Equal("@Sasha", vm.DisplayName);
    }

    // ── Null name field falls back to steamId ─────────────────────────────────

    [Fact]
    public async Task GetOrCreate_ApiReturnsNullName_FallsBackToSteamId()
    {
        var tcs = new TaskCompletionSource<(string?, string?)?>();
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync("55", Arg.Any<System.Threading.CancellationToken>())
           .Returns(tcs.Task);

        var resolver = new UserNameResolver(api, new SyncDispatcher());
        var vm = resolver.GetOrCreate("55");

        var nameChanged = new TaskCompletionSource();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerNameViewModel.DisplayName))
                nameChanged.TrySetResult();
        };

        tcs.SetResult((null, null));
        await nameChanged.Task.WaitAsync(System.TimeSpan.FromSeconds(2));

        Assert.Equal("@55", vm.DisplayName);
    }

    // ── Null API response leaves DisplayName as loading placeholder ───────────

    [Fact]
    public async Task GetOrCreate_ApiReturnsNull_DisplayNameRemainsLoading()
    {
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync("99", Arg.Any<System.Threading.CancellationToken>())
           .Returns(System.Threading.Tasks.Task.FromResult<(string?, string?)?>(null));

        var resolver = new UserNameResolver(api, new SyncDispatcher());
        var vm = resolver.GetOrCreate("99");

        // Give the async load time to complete (it will return null and do nothing).
        await Task.Delay(50);

        Assert.NotEqual("@99", vm.DisplayName); // should still be the loading placeholder
    }

    // ── Different steamIds get different instances ─────────────────────────────

    [Fact]
    public void GetOrCreate_DifferentIds_ReturnDifferentInstances()
    {
        var api = Substitute.For<IBackendApiService>();
        api.GetUserInfoAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
           .Returns(System.Threading.Tasks.Task.FromResult<(string?, string?)?>(null));

        var resolver = new UserNameResolver(api, new SyncDispatcher());

        var vm1 = resolver.GetOrCreate("1");
        var vm2 = resolver.GetOrCreate("2");

        Assert.NotSame(vm1, vm2);
    }
}
