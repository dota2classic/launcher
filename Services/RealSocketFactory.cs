namespace d2c_launcher.Services;

public sealed class RealSocketFactory : ISocketFactory
{
    public ISocket Create(string origin, string path, string token) =>
        new RealSocket(origin, path, token);
}
