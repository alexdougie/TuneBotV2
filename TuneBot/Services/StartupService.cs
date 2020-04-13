using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace TuneBot
{
    internal class StartupService
    {
        private readonly IServiceProvider provider;
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;

        public StartupService(
            IServiceProvider provider,
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config)
        {
            this.provider = provider;
            this.config = config;
            this.discord = discord;
            this.commands = commands;
        }

        public async Task StartAsync()
        {
            var discordToken = config["BotToken"];

            await discord.LoginAsync(TokenType.Bot, discordToken);
            await discord.StartAsync();

            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), provider);
        }
    }
}