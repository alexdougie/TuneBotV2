using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace TuneBot.Services
{
    internal sealed class InteractionHandler
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _serviceProvider;

        public InteractionHandler(
            DiscordSocketClient discordSocketClient,
            InteractionService commands,
            IServiceProvider serviceProvider)
        {
            _discordSocketClient = discordSocketClient;
            _commands = commands;
            _serviceProvider = serviceProvider;
        }
        
        public async Task InitializeAsync()
        {
            // add the public modules that inherit InteractionModuleBase<T> to the InteractionService
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);

            // process the InteractionCreated payloads to execute Interactions commands
            _discordSocketClient.InteractionCreated += HandleInteraction;
        }



        private async Task HandleInteraction(SocketInteraction arg)
        {
            try
            {
                var ctx = new SocketInteractionContext(_discordSocketClient, arg);
                await _commands.ExecuteCommandAsync(ctx, _serviceProvider);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                if(arg.Type == InteractionType.ApplicationCommand)
                {
                    await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
                }
            }
        }
    }
}