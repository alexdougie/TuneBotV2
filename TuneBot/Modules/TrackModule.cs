#nullable enable
using Discord.Commands;
using Google.Apis.YouTube.v3;
using IF.Lastfm.Core.Api;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using TuneBot.Services.DataAccess;

namespace TuneBot.Modules
{
    public class TrackModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly YouTubeService _youtubeService;
        private readonly IConfiguration _configuration;
        private readonly DbService _dbService;

        public TrackModule(YouTubeService youtubeService, IConfiguration configuration, DbService dbService)
        {
            _youtubeService = youtubeService;
            _configuration = configuration;
            _dbService = dbService;
        }

        [SlashCommand("track", "Gets a track from Spotify and YouTube.")]
        public async Task GetTrack([Remainder] string details)
        {
            using var typing = Context.Channel.EnterTypingState();

            var spotifyResultTask = SearchSpotifyAsync(details);
            var youTubeResultTask = SearchYouTubeAndGetLinkAsync(details);

            var (spotifyResult, youtubeResult) = (await spotifyResultTask, await youTubeResultTask);

            var spotifyResponse = spotifyResult is null ? "No Spotify result found" : spotifyResult.ExternalUrls["spotify"];
            var youTubeResponse = youtubeResult ?? "No YouTube result found";

            await RespondAsync($"{spotifyResponse}\n{youTubeResponse}");
        }

        [SlashCommand("link", "Converts either a YouTube or Spotify URL")]
        public async Task GetLink([Remainder] string link)
        {
            using var typing = Context.Channel.EnterTypingState();
            var result = "No match found.";

            if (link.Contains("spotify", StringComparison.OrdinalIgnoreCase))
            {
                var spotifyTrack = await GetSpotifyTrackNameFromLink(link);

                if (spotifyTrack != null)
                    result = await SearchYouTubeAndGetLinkAsync(spotifyTrack);
            }
            else if (link.Contains("youtube", StringComparison.OrdinalIgnoreCase) ||
                     link.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                var youTubeResult = await GetYoutubeTitle(link);

                if (youTubeResult != null)
                {
                    var spotifyResult = await SearchSpotifyAsync(youTubeResult);

                    if (spotifyResult != null)
                        result = spotifyResult.ExternalUrls["spotify"];
                }
            }

            await RespondAsync(result);
        }


        [SlashCommand("npset",
            "Sets your Last.fm username. Must be done before /np can be used. Usage: /npset <username>")]
        public async Task NowPlayingSet([Remainder] string userName)
        {
            using var typing = Context.Channel.EnterTypingState();

            var userId = Context.User.Id;

            var result = await _dbService.SetLastFmUsername(userId, userName);

            if (result < 1)
            {
                await RespondAsync(
                    "Something went wrong when setting lastFm username... Please reach out to developers");
                return;
            }

            await RespondAsync("Last.fm username saved");
        }

        [SlashCommand("npsetspotify", "Sets your spotify token. Must be done before /nps can be used.")]
        public async Task NowPlayingSetSpotify([Remainder] string token)
        {
            using var typing = Context.Channel.EnterTypingState();
            var userId = Context.User.Id;

            var result = await _dbService.SetSpotifyToken(userId, token);

            if (result < 1)
            {
                await RespondAsync("Something went wrong when setting spotify token... Please reach out to developers");
                return;
            }

            await RespondAsync("Spotify token saved");
        }

        [SlashCommand("np", "Gets current/last scrobbled track from Last.fm.")]
        public async Task NowPlaying()
        {
            var userId = Context.User.Id;

            string? lastFmUserName = await _dbService.GetLastFmUsername(userId);

            if (string.IsNullOrEmpty(lastFmUserName))
            {
                await RespondAsync(
                    "No Last.fm user name found. Use !npset <username> to set your Last.fm user name");
                return;
            }

            using var client = new LastfmClient(_configuration["LastFmKey"], _configuration["LastFmSecret"]);
            {
                try
                {
                    var track = await client.User.GetRecentScrobbles(lastFmUserName, count: 1);

                    var lastTrack = track.FirstOrDefault();

                    if (lastTrack is null)
                    {
                        await RespondAsync("No now playing data found for Last.fm user");
                        return;
                    }

                    var songName = lastTrack.ArtistName + " " + lastTrack.Name;

                    var youTubeTask = SearchYouTubeAndGetLinkAsync(songName);

                    var spotifyTrack = await SearchSpotifyAsync(songName);

                    var embed = CreateEmbedAsync(spotifyTrack!);

                    if (embed is null)
                    {
                        await RespondAsync("Data for now playing not found");
                        return;
                    }

                    var youtubeLink = await youTubeTask;

                    var components = CreateMessageComponentsForReply(spotifyTrack.ExternalUrls["spotify"], youtubeLink!, lastTrack.Url.ToString());

                    await RespondAsync(embed: embed, components: components);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        [SlashCommand("nps", "Gets currently playing spotify song")]
        public async Task NowPlayingSpotify()
        {
            using var typing = Context.Channel.EnterTypingState();

            var userId = Context.User.Id;

            var token = await _dbService.GetSpotifyToken(userId);

            if (string.IsNullOrEmpty(token))
            {
                await RespondAsync(
                    $"Spotify token invalid. Use /npsetspotify <token> in a private message to TuneBot to set your Spotify token.\n Do not send this token in a public channel. ");
                return;
            }

            try
            {
                var spotifyConfig = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(
                        new ClientCredentialsAuthenticator(_configuration["SpotifyId"],
                            _configuration["SpotifySecret"]));

                var spotify = new SpotifyClient(spotifyConfig);

                var currentlyPlaying =
                    await spotify.Player.GetCurrentlyPlaying(
                        new PlayerCurrentlyPlayingRequest(PlayerCurrentlyPlayingRequest.AdditionalTypes.Track));

                var spotifyTrack = currentlyPlaying.Item as FullTrack;

                if (spotifyTrack is null)
                {
                    await RespondAsync("No results found");
                    return;
                }

                var youtubeLink =
                    await SearchYouTubeAndGetLinkAsync(
                        $"{spotifyTrack.Name} + {spotifyTrack.Artists.FirstOrDefault().Name}");

                // // var embed = await CreateEmbedAsync(spotifyTrack);
                // // var messageComponents = CreateMessageComponentsForReply(spotifyTrack.Uri, youtubeLink);
                //
                // await RespondAsync(embed: embed, components: messageComponents);
            }
            catch (APIUnauthorizedException)
            {
                await RespondAsync("Your Spotify API token has expired.");
                return;
            }
        }

        [SlashCommand("goodbot", "Good bot.")]
        public async Task GoodBot()
        {
            using var typing = Context.Channel.EnterTypingState();
            var userId = Context.User.Id;

            string lastFmUserName = await _dbService.GetLastFmUsername(userId);

            if (string.IsNullOrEmpty(lastFmUserName))
            {
                await RespondAsync(
                    "No Last.fm user name found. Use /npset <username> to set your Last.fm user name");
                return;
            }

            if (lastFmUserName.Equals("rijads"))
            {
                await RespondAsync("Wurf. Borna loves Rijad :)");
                return;
            }

            await RespondAsync($"Go away, @{lastFmUserName}");
        }

        /// <summary>
        /// Gets youtube title
        /// </summary>
        /// <param name="query">Song name, video name, etc...</param>
        /// <returns>Youtube title string</returns>
        private async Task<string?> GetYoutubeTitle(string query)
        {
            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.Q = query;
            searchRequest.MaxResults = 5;

            var response = await searchRequest.ExecuteAsync();

            var searchResult = response.Items.FirstOrDefault(i => i.Id.Kind == "youtube#video");

            var title = searchResult?.Snippet.Title;

            return title;
        }

        /// <summary>
        /// Gets spotify track ID from link
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        private async Task<string?> GetSpotifyTrackNameFromLink(string link)
        {
            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(
                    new ClientCredentialsAuthenticator(_configuration["SpotifyId"], _configuration["SpotifySecret"]));

            var spotify = new SpotifyClient(spotifyConfig);

            var id = link.Split("/").LastOrDefault();
            var idWithoutQueryString = id?.Split("?").FirstOrDefault();

            if (idWithoutQueryString != null)
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

        /// <summary>
        /// Creates message components for a reply message
        /// </summary>
        /// <param name="spotifyLink">Spotify link</param>
        /// <param name="youtubeLink">Youtube link</param>
        /// <returns>A <see cref="MessageComponent"/> </returns>
        private MessageComponent? CreateMessageComponentsForReply(string spotifyLink, string youtubeLink, string lastFmLink)
        {
            var component =
                new ComponentBuilder()
                    .WithButton(url: spotifyLink, label: "Spotify", style: ButtonStyle.Link)
                    .WithButton(url: youtubeLink, label: "YouTube", style: ButtonStyle.Link)
                    .WithButton(url: lastFmLink, label: "Last.fm", style: ButtonStyle.Link)
                    .Build();

            return component;
        }

        /// <summary>
        /// Creates an embed for user display after using an /np or /nps command
        /// </summary>
        /// <param name="spotifyTrack">Spotify track object</param>
        /// <returns>A <see cref="Embed"/></returns>
        private Embed CreateEmbedAsync(FullTrack spotifyTrack)
        {
            var embed = new EmbedBuilder
            {
                Title = $"{Context.User.GlobalName} is now listening",
                Description = $"**{spotifyTrack.Artists.First().Name}**\n{spotifyTrack.Name}",
                ThumbnailUrl = $"{spotifyTrack.Album.Images.MinBy(x => x.Height).Url}",
                Color = new Color(0x5c7cfe),
            }.Build();

            return embed;
        }

        /// <summary>
        /// Searches spotify 
        /// </summary>
        /// <param name="songName">Song name, Keywords, etc...</param>
        /// <returns>Full Track Object</returns>
        private async Task<FullTrack?> SearchSpotifyAsync(string query)
        {
            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(
                    new ClientCredentialsAuthenticator(_configuration["SpotifyId"], _configuration["SpotifySecret"]));

            var spotify = new SpotifyClient(spotifyConfig);

            try
            {
                var searchResult = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, query));
                var track = searchResult.Tracks?.Items?.FirstOrDefault();
                return track;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        /// Searches youtube
        /// </summary>
        /// <param name="query">Song name, Video name, etc...</param>
        /// <returns>YouTube Link</returns>
        private async Task<string?> SearchYouTubeAndGetLinkAsync(string query)
        {
            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.Q = query;
            searchRequest.MaxResults = 5;

            var response = await searchRequest.ExecuteAsync();

            var searchResult = response.Items.FirstOrDefault(i => i.Id.Kind == "youtube#video");

            return searchResult != null ? $"https://youtu.be/{searchResult.Id.VideoId}" : null;
        }
    }
}