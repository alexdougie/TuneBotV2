using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace TuneBot
{
    internal class CommandHandler
    {
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private readonly IConfiguration config;
        private readonly IServiceProvider provider;

        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            IConfiguration config,
            IServiceProvider provider)
        {
            this.discord = discord;
            this.commands = commands;
            this.config = config;
            this.provider = provider;

            this.discord.MessageReceived += OnMessageReceivedAsync;
        }

        private async Task OnMessageReceivedAsync(SocketMessage s)
        {
            if (!(s is SocketUserMessage msg)) return;
            if (msg.Author.Id == discord.CurrentUser.Id) return;

            var context = new SocketCommandContext(discord, msg);

            int argPos = 0;
            if (msg.HasStringPrefix("!", ref argPos) ||
                msg.HasMentionPrefix(discord.CurrentUser, ref argPos))
            {
                await commands.ExecuteAsync(context, argPos, provider);
            }
        }
    }
}