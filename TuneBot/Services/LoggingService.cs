using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TuneBot
{
    internal class LoggingService
    {
        private readonly DiscordSocketClient discord;
        private readonly CommandService commands;
        private string logDirectory;

        private string logFile => Path.Combine(logDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.txt");

        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

            this.discord = discord;
            this.commands = commands;

            this.discord.Log += OnLogAsync;
            this.commands.Log += OnLogAsync;
        }

        private Task OnLogAsync(LogMessage msg)
        {
            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            if (!File.Exists(logFile))
                File.Create(logFile).Dispose();

            var logText =
                $"{DateTime.UtcNow:hh:mm:ss} [{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";
            File.AppendAllText(logFile, logText + "\n");

            return Console.Out.WriteLineAsync(logText);
        }
    }
}