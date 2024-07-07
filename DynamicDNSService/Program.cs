using System.Threading.Channels;

namespace DynamicDNSService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("<----- Dynamic DNS Service is Starting ----->");
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