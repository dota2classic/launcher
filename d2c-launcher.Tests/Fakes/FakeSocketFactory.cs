using d2c_launcher.Services;

namespace d2c_launcher.Tests.Fakes;

/// <summary>
/// Returns a pre-configured FakeSocket so tests can control the socket directly.
/// </summary>
public sealed class FakeSocketFactory : ISocketFactory
{
    public FakeSocket Socket { get; } = new FakeSocket();

    public ISocket Create(string origin, string path, string token) => Socket;
}
