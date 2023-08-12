using System.Threading.Tasks;

namespace TuneBot.Services.DataAccess;

public interface IDbService
{
    Task<int> SetLastFmUsername(ulong userId, string username);
    Task<int> SetSpotifyToken(ulong userId, string spotifyToken);
    Task<string> GetLastFmUsername(ulong userId);
    Task<string> GetSpotifyToken(ulong userId);
    Task<int> SetupDb();
}