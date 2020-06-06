#nullable enable
using Discord.Commands;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using System.Linq;
using System.Threading.Tasks;

namespace TuneBot.Modules
{
    public class TrackModule : ModuleBase<SocketCommandContext>
    {
        private readonly IConfigurationRoot config;

        public TrackModule(IConfigurationRoot config) => this.config = config;

        [Command("track")]
        [Summary("Gets a track from Spotify and YouTube.")]
        public async Task GetTrack([Remainder]string details)
        {
            var spotifyResult = await SearchSpotify(details);

            var youTubeResult = await SearchYouTube(details);

            await ReplyAsync(spotifyResult + "\n" + youTubeResult);
        }

        private async Task<string> SearchSpotify(string details)
        {
            // todo: singleton spotify object
            var credentialsAuth = new CredentialsAuth(
                config["SpotifyId"],
                config["SpotifySecret"]);

            var token = await credentialsAuth.GetToken();
            var spotify = new SpotifyWebAPI
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };

            var searchResult = await spotify.SearchItemsAsync(details, SearchType.Track, 1);

            var track = searchResult.Tracks.Items.First();

            var result =
                track != null ?
                    track.ExternUrls["spotify"] :
                    "No Spotify result found";

            return result;
        }

        private async Task<string> SearchYouTube(string details)
        {
            var youtubeService = new YouTubeService(
                new BaseClientService.Initializer()
                {
                    ApiKey = config["YouTubeKey"]
                });

            var searchRequest = youtubeService.Search.List("snippet");
            searchRequest.Q = details;
            searchRequest.MaxResults = 5;

            var response = await searchRequest.ExecuteAsync();

            var searchResult = response.Items.Where(i => i.Id.Kind == "youtube#video").First();

            var result =
                searchResult != null ?
                    $"https://youtu.be/{searchResult.Id.VideoId}" :
                    "No YouTube result found";

            return result;
        }
    }
}
