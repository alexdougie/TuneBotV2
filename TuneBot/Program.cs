using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace TuneBot
{
    class Program
    {
        private DiscordSocketClient client;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            client = new DiscordSocketClient();

            client.Log += Log;
            client.MessageReceived += MessageRecieved;

            var json = File.ReadAllText("ApiKeys.json");

            var config = JsonSerializer.Deserialize<Config>(json);

            await client.LoginAsync(TokenType.Bot, config.BotToken);

            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task MessageRecieved(SocketMessage message)
        {
            if (message.Content == "!ping")
            {
                await message.Channel.SendMessageAsync("Pong!");
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
