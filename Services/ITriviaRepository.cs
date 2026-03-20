using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public interface ITriviaRepository
{
    Task<TriviaQuestion[]> LoadAsync();
}
