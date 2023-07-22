using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace TuneBot.Services.DataAccess;

public class DbService : IDbService
{
    public async Task<int> SetLastFmUsername(ulong userId, string username)
    {
        await using var connection = new SqliteConnection("Data Source=data.db");
        
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        
        command.CommandText = "INSERT OR REPLACE INTO users(id, lastfm_name) VALUES($id,$lastfm_name)";
        command.Parameters.AddWithValue("$id", userId);
        command.Parameters.AddWithValue("$lastfm_name", username);

        var result = await command.ExecuteNonQueryAsync();

        return result;
    }

    public async  Task<int> SetSpotifyToken(ulong userId, string spotifyToken)
    {
        await using var connection = new SqliteConnection("Data Source=data.db");
        
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        
        command.CommandText = "INSERT OR REPLACE INTO users(id, spotify_token) VALUES($id,$spotify_token)";
        command.Parameters.AddWithValue("$id", userId);
        command.Parameters.AddWithValue("$spotify_token", spotifyToken);

        var result = await command.ExecuteNonQueryAsync();

        return result;
    }

    public async Task<string> GetLastFmUsername(ulong userId)
    {
        string lastFmUserName = string.Empty;
        
        await using var connection = new SqliteConnection("Data Source=data.db");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM users WHERE id=$id";
        command.Parameters.AddWithValue("$id", userId);

        await using var reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            var isNull = await reader.IsDBNullAsync(1);

            lastFmUserName = isNull == true ? null : reader.GetString(1);
        }
        return lastFmUserName;
    }

    public async Task<string> GetSpotifyToken(ulong userId)
    {
        string token = string.Empty;

        await using var connection = new SqliteConnection("Data Source=data.db");
        
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        
        command.CommandText = "SELECT * FROM users WHERE id=$id";
        command.Parameters.AddWithValue("$id", userId);

        await using var reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            var isNull = await reader.IsDBNullAsync(2);

            token = isNull == true ? null : reader.GetString(2);
        }

        return token;
    }

    public async Task<int> SetupDb()
    {
        await using var connection = new SqliteConnection("Data Source=data.db");
        await connection.OpenAsync();
        
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY, lastfm_name TEXT, spotify_token TEXT)";

        var result = command.ExecuteNonQuery();

        return result;
    }
}