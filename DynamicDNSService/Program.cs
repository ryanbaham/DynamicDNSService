using System.Threading.Channels;

namespace DynamicDNSService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"{new string('-',48)}{Environment.NewLine}" +
                $" |||||||||||| Dynamic DNS Service |||||||||||||{Environment.NewLine}" +
                $"{Environment.NewLine} A DNSimple A Record Updater Hack by Ryan Baham" +
                $"{Environment.NewLine}{new string('-', 48)}");
            var builder = Host.CreateApplicationBuilder(args);
            var channel = Channel.CreateUnbounded<string>();
            builder.Services.AddSingleton(channel.Reader);
            builder.Services.AddSingleton(channel.Writer);
            builder.Services.AddHostedService<PublicIPCheckWorker>();
            builder.Services.AddHostedService<DNSimpleZoneRecordUpdateWorker>();

            var host = builder.Build();
            host.Run();
        }
    }
}