using System.Collections.Generic;
using System.Threading.Tasks;

namespace d2c_launcher.Services;

public interface ISocketFactory
{
    /// <summary>Creates and returns a new unconnected socket to <paramref name="origin"/>.</summary>
    ISocket Create(string origin, string path, string token);
}
