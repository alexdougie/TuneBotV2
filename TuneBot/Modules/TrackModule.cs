#nullable enable
using Discord.Commands;
using Google.Apis.YouTube.v3;
using IF.Lastfm.Core.Api;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TuneBot.Modules
{
    public class TrackModule : ModuleBase<SocketCommandContext>
    {
        private readonly YouTubeService youtube;
        private readonly IConfiguration config;

        public TrackModule(YouTubeService youtube, IConfiguration config)
        {
            this.youtube = youtube;
            this.config = config;
        }

        [Command("track")]
        [Summary("Gets a track from Spotify and YouTube.")]
        public async Task GetTrack([Remainder] string details)
        {
            var spotifyResultTask = SearchSpotify(details);
            var youTubeResultTask = SearchYouTube(details);
            var spotifyResult = await spotifyResultTask;
            var youTubeResult = await youTubeResultTask;

            await ReplyAsync(spotifyResult ?? "No Spotify match found.");
            await ReplyAsync("\n" + youTubeResult ?? "No YouTube match found");
        }

        [Command("link")]
        [Summary("Converts either a YouTube or Spotify URL.")]
        public async Task GetLink([Remainder] string link)
        {
            var result = "No match found.";

            if (link.Contains("spotify", StringComparison.OrdinalIgnoreCase))
            {
                var spotifyTrack = await GetSpotifyTrack(link);

                if (spotifyTrack != null)
                    result = await SearchYouTube(spotifyTrack);
            }
            else if (link.Contains("youtube", StringComparison.OrdinalIgnoreCase) ||
                    link.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                var youTubeResult = await GetYoutubeTitle(link);

                if (youTubeResult != null)
                {
                    var spotifyResult = await SearchSpotify(youTubeResult);

                    if (spotifyResult != null)
                        result = spotifyResult;
                }
            }

            await ReplyAsync(result);
        }

        [Command("npset")]
        [Summary("Sets your Last.fm username. Must be done before !np can be used.")]
        public async Task NowPlayingSet([Remainder] string userName)
        {
            var userId = Context.User.Id;

            using (var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT OR REPLACE INTO users(id, lastfm_name) VALUES($id,$lastfm_name)";
                command.Parameters.AddWithValue("$id", userId);
                command.Parameters.AddWithValue("$lastfm_name", userName);

                await command.ExecuteNonQueryAsync();
            }
        }

        [Command("np")]
        [Summary("Gets current/last scrobbled track from Last.fm.")]
        public async Task NowPlaying()
        {
            var userId = Context.User.Id;

            string? lastFmUserName = null;

            using (var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM users WHERE id=$id";
                command.Parameters.AddWithValue("$id", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lastFmUserName = reader.GetString(1);
                    }
                }
            }

            if (lastFmUserName != null)
            {
                var client = new LastfmClient(config["LastFmKey"], config["LastFmSecret"]);

                var track = await client.User.GetRecentScrobbles(lastFmUserName, count: 1);

                var lastTrack = track.FirstOrDefault();

                if (lastTrack != null)
                {
                    var songName = lastTrack.ArtistName + " " + lastTrack.Name;

                    var spotifyResultTask = SearchSpotify(songName);
                    var youTubeResultTask = SearchYouTube(songName);
                    var spotifyResult = await spotifyResultTask;
                    var youTubeResult = await youTubeResultTask;

                    await ReplyAsync(spotifyResult ?? "No Spotify match found.");
                    await ReplyAsync(youTubeResult ?? "No YouTube match found");
                }
                else
                {
                    await ReplyAsync("No now playing data found for Last.fm user");
                }
            }
            else
            {
                await ReplyAsync("No Last.fm user name found. Use !npset to set your Last.fm user name");
            }
        }

        private async Task<string?> GetYoutubeTitle(string details)
        {
            var searchRequest = youtube.Search.List("snippet");
            searchRequest.Q = details;
            searchRequest.MaxResults = 5;

            var response = await searchRequest.ExecuteAsync();

            var searchResult = response.Items.Where(i => i.Id.Kind == "youtube#video").FirstOrDefault();

            var title = searchResult?.Snippet.Title;

            return title;
        }

        private async Task<string?> GetSpotifyTrack(string link)
        {
            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(
                    new ClientCredentialsAuthenticator(config["SpotifyId"], config["SpotifySecret"]));

            var spotify = new SpotifyClient(spotifyConfig);

            var id = link.Split("/").LastOrDefault();

            var track = await spotify.Tracks.Get(id ?? "");

            string? result = null;

            if (track != null)
            {
                result = "";
                foreach (var artist in track.Artists)
                {
                    result += artist.Name + " ";
                }

                result += " " + track.Name;
            }

            return result;
        }

        private async Task<string?> SearchSpotify(string details)
        {
            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(
                    new ClientCredentialsAuthenticator(config["SpotifyId"], config["SpotifySecret"]));

            var spotify = new SpotifyClient(spotifyConfig);

            var searchResult = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, details));

            var track = searchResult.Tracks?.Items?.FirstOrDefault();

            var result = track?.ExternalUrls["spotify"];

            return result;
        }

        private async Task<string> SearchYouTube(string details)
        {
            var searchRequest = youtube.Search.List("snippet");
            searchRequest.Q = details;
            searchRequest.MaxResults = 5;

            var response = await searchRequest.ExecuteAsync();

            var searchResult = response.Items.Where(i => i.Id.Kind == "youtube#video").FirstOrDefault();

            var result =
                searchResult != null ?
                    $"https://youtu.be/{searchResult.Id.VideoId}" :
                    "No YouTube result found";

            return result;
        }
    }
}
