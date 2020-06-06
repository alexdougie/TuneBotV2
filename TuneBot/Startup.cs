using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Threading.Tasks;

namespace TuneBot
{
    public class Startup
    {
        public Startup()
        {
            var builder = new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("ApiKeys.json");

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }

        public static async Task RunAsync(string[] args)
        {
            var startup = new Startup();

            await startup.RunAsync();
        }

        private async Task RunAsync()
        {
            var services = new ServiceCollection();
            await ConfigureServicesAsync(services);

            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<LoggingService>();
            provider.GetRequiredService<CommandHandler>();

            await provider.GetRequiredService<StartupService>().StartAsync();

            using(var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY, lastfm_name TEXT)";

                command.ExecuteNonQuery();
            }

            await Task.Delay(-1);
        }

        private async Task ConfigureServicesAsync(ServiceCollection services)
        {
            var credentialsAuth = new CredentialsAuth(
                Configuration["SpotifyId"],
                Configuration["SpotifySecret"]);

            var token = await credentialsAuth.GetToken();

            var spotify = new SpotifyWebAPI
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };

            var youtubeService = new YouTubeService(
                new BaseClientService.Initializer()
                {
                    ApiKey = Configuration["YouTubeKey"]
                });

            services
                .AddSingleton(
                new DiscordSocketClient(
                    new DiscordSocketConfig()))
                .AddSingleton(
                    new CommandService())
                .AddSingleton<CommandHandler>()
                .AddSingleton<StartupService>()
                .AddSingleton<LoggingService>()
                .AddSingleton(spotify)
                .AddSingleton(youtubeService)
                .AddSingleton(Configuration);
        }
    }
}
