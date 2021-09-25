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
            using var typing = Context.Channel.EnterTypingState();

            var spotifyResultTask = SearchSpotify(details);
            var youTubeResultTask = SearchYouTube(details);
            await Task.WhenAll(spotifyResultTask, youTubeResultTask);
           
            await ReplyAsync(spotifyResultTask.Result ?? "No Spotify match found.");
            await ReplyAsync("\n" + youTubeResultTask.Result ?? "No YouTube match found");
        }

        [Command("link")]
        [Summary("Converts either a YouTube or Spotify URL.")]
        public async Task GetLink([Remainder] string link)
        {
            using var typing = Context.Channel.EnterTypingState();
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
        [Summary("Sets your Last.fm username. Must be done before !np can be used. Usage: !npset <username>")]
        public async Task NowPlayingSet([Remainder] string userName)
        {
            using var typing = Context.Channel.EnterTypingState();

            var userId = Context.User.Id;

            using (var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT OR REPLACE INTO users(id, lastfm_name) VALUES($id,$lastfm_name)";
                    command.Parameters.AddWithValue("$id", userId);
                    command.Parameters.AddWithValue("$lastfm_name", userName);

                    await command.ExecuteNonQueryAsync();
                }
            }

            await ReplyAsync("Last.fm username saved");
        }

        [Command("npsetspotify")]
        [Summary("Sets your spotify token. Must be done before !nps can be used.")]
        public async Task NowPlayingSetSpotify([Remainder] string token)
        {
            using var typing = Context.Channel.EnterTypingState();
            var userId = Context.User.Id;

            using (var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT OR REPLACE INTO users(id, spotify_token) VALUES($id,$spotify_token)";
                    command.Parameters.AddWithValue("$id", userId);
                    command.Parameters.AddWithValue("$spotify_token", token);

                    await command.ExecuteNonQueryAsync();
                }
            }

            await ReplyAsync("Spotify token saved");
        }

        [Command("np")]
        [Summary("Gets current/last scrobbled track from Last.fm.")]
        public async Task NowPlaying()
        {
            using var typing = Context.Channel.EnterTypingState();
            var userId = Context.User.Id;

            string? lastFmUserName = null;

            using (var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM users WHERE id=$id";
                    command.Parameters.AddWithValue("$id", userId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var isNull = await reader.IsDBNullAsync(1);

                            lastFmUserName = isNull == true ? null : reader.GetString(1);
                        }
                    }
                }
            }

            if (lastFmUserName != null)
            {
                using var client = new LastfmClient(config["LastFmKey"], config["LastFmSecret"]);

                var track = await client.User.GetRecentScrobbles(lastFmUserName, count: 1);

                var lastTrack = track.FirstOrDefault();

                if (lastTrack != null)
                {
                    var songName = lastTrack.ArtistName + " " + lastTrack.Name;

                    var spotifyResultTask = SearchSpotify(songName);
                    var youTubeResultTask = SearchYouTube(songName);
                    await Task.WhenAll(spotifyResultTask, youTubeResultTask);

                    await ReplyAsync(spotifyResultTask.Result ?? "No Spotify match found.");
                    await ReplyAsync(youTubeResultTask.Result ?? "No YouTube match found");
                }
                else
                {
                    await ReplyAsync("No now playing data found for Last.fm user");
                }
            }
            else
            {
                await ReplyAsync("No Last.fm user name found. Use !npset <username> to set your Last.fm user name");
            }
        }

        [Command("nps")]
        [Summary("Gets currently playing spotify song")]
        public async Task NowPlayingSpotify()
        {
            using var typing = Context.Channel.EnterTypingState();

            var userId = Context.User.Id;

            string? token = null;

            using (var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM users WHERE id=$id";
                    command.Parameters.AddWithValue("$id", userId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var isNull = await reader.IsDBNullAsync(2);

                            token = isNull == true ? null : reader.GetString(2);
                        }
                    }
                }
            }

            if (token != null)
            {
                try
                {
                    string? trackUri = null;
                    string? trackDetails = null;
                    var spotifyConfig = SpotifyClientConfig
                        .CreateDefault()
                        .WithAuthenticator(
                            new ClientCredentialsAuthenticator(config["SpotifyId"], config["SpotifySecret"]));

                    var spotify = new SpotifyClient(token);

                    var currentlyPlaying =
                        await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest(PlayerCurrentlyPlayingRequest.AdditionalTypes.Track));

                    if (currentlyPlaying.Item is FullTrack fullTrack)
                    {
                        trackUri = fullTrack.ExternalUrls["spotify"];
                        trackDetails = string.Empty;

                        foreach (var artist in fullTrack.Artists)
                        {
                            trackDetails += artist.Name + " ";
                        }

                        trackDetails += " - " + fullTrack.Name;
                    }

                    await ReplyAsync(trackUri ?? "No Spotify match found.");

                    if (string.IsNullOrEmpty(trackDetails) == false)
                    {
                        var youTubeResult = await SearchYouTube(trackDetails);
                        await ReplyAsync(youTubeResult ?? "No YouTube match found");
                    }
                }
                catch(APIUnauthorizedException)
                {
                    await ReplyAsync("Your Spotify API token has expired.");
                    return;
                }
            }
            else
            {
                await ReplyAsync("Spotify token invalid. Use !npsetspotify <token> in a private message to TuneBot to set your Spotify token.");
                await ReplyAsync("Do not send this token in a public chanel.");
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
            var idWithoutQueryString = id?.Split("?").FirstOrDefault();

            if(idWithoutQueryString != null)
            {
                id = idWithoutQueryString;
            }

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

        private async Task<string?> SearchYouTube(string details)
        {
            var searchRequest = youtube.Search.List("snippet");
            searchRequest.Q = details;
            searchRequest.MaxResults = 5;

            var response = await searchRequest.ExecuteAsync();

            var searchResult = response.Items.Where(i => i.Id.Kind == "youtube#video").FirstOrDefault();

            return searchResult != null ?
                    $"https://youtu.be/{searchResult.Id.VideoId}" :
                    null;
        }
    }
}
