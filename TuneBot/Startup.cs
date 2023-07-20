using Discord.WebSocket;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using TuneBot.Services;
using TuneBot.Services.DataAccess;
using Discord.Commands;

namespace TuneBot
{
    public class Startup
    {
        public Startup()
        {
            Console.WriteLine(Directory.GetCurrentDirectory());
            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("ApiKeys.json");

            _configuration = builder.Build();
        }

        private readonly IConfiguration _configuration;

        public static async Task RunAsync(string[] args)
        {
            var startup = new Startup();

            await startup.RunAsync();
        }

        private async Task RunAsync()
        {
            var provider = ConfigureServices();
            
            var client = provider.GetRequiredService<DiscordSocketClient>();
            var slashCommands = provider.GetRequiredService<InteractionService>();
            await provider.GetRequiredService<InteractionHandler>().InitializeAsync();
            
            var loggingService = provider.GetRequiredService<LoggingService>();

            var dbService = provider.GetRequiredService<DbService>();
            
            await client.LoginAsync(TokenType.Bot, _configuration["BotToken"]);
            await client.StartAsync();

            client.Ready += async () =>
            {
                Console.WriteLine("Bot Ready!");
                await slashCommands.RegisterCommandsGloballyAsync();
            };
            
            await dbService.SetupDb();

            
            await Task.Delay(-1);
        }

        private ServiceProvider ConfigureServices()
        {
            var youtubeService = new YouTubeService(
                new BaseClientService.Initializer()
                {
                    ApiKey = _configuration["YouTubeKey"]
                });

            return new ServiceCollection()
                .AddSingleton(_configuration)
                .AddTransient<DbService>()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<InteractionHandler>()
                .AddSingleton<CommandService>()
                .AddSingleton<LoggingService>()
                .AddSingleton(youtubeService)
                .BuildServiceProvider();

        }
    }
}
