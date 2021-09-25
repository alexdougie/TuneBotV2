using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<LoggingService>();
            provider.GetRequiredService<CommandHandler>();

            await provider.GetRequiredService<StartupService>().StartAsync();

            using(var connection = new SqliteConnection("Data Source=data.db"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY, lastfm_name TEXT, spotify_token TEXT)";

                    command.ExecuteNonQuery();
                }
            }

            await Task.Delay(-1);
        }

        private void ConfigureServices(ServiceCollection services)
        {
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
                .AddSingleton(youtubeService)
                .AddSingleton(Configuration);
        }
    }
}
