using Discord.Commands;
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

            var track = await spotify.SearchItemsAsync(details, SearchType.Track, 1);

            //todo: nullable reference types!
            await ReplyAsync(track.Tracks.Items.First().ExternUrls["spotify"]);
        }
    }
}
