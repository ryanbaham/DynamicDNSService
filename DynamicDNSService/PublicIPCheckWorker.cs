namespace DynamicDNSService
{
    public class PublicIPCheckWorker : BackgroundService
    {
        private readonly ILogger<PublicIPCheckWorker> _logger;
        private readonly HttpClient _httpClient;
        private readonly ChannelWriter<string> _channelWriter;

        private static string? _publicIPCheckFrequency = Environment.GetEnvironmentVariable("PUBLIC_IP_CHECK_FREQ_MINS");

        private string _lastIp;

        public PublicIPCheckWorker(ILogger<PublicIPCheckWorker> logger, ChannelWriter<string> channelWriter)
        {
            //worker service DI config, comes in the box
            _logger = logger;
            _httpClient = new HttpClient();
            _channelWriter = channelWriter;

            if (_publicIPCheckFrequency is null)
            {
                _logger.LogWarning("PUBLIC_IP_CHECK_FREQ_MINS is NULL, public IP check frequency will default to 5 minutes");
            }
            else
            {
                _logger.LogInformation("Public IP check frequency (PUBLIC_IP_CHECK_FREQ_MINS) is set to {_publicIPCheckFrequency} minutes.", _publicIPCheckFrequency);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ipInfo = await _httpClient.GetFromJsonAsync<IPInfo>("https://jsonip.com");
                    _logger.LogInformation("jsonip.com returned {ipInfo.Ip}", ipInfo.Ip);

                    if (ipInfo.Ip != _lastIp)
                    {
                        _logger.LogWarning("IP changed from {_lastIp} to {ipInfo.Ip}", _lastIp, ipInfo.Ip);
                        await _channelWriter.WriteAsync(ipInfo.Ip, stoppingToken);
                        _lastIp = ipInfo.Ip;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking IP");
                }

                if (int.TryParse(_publicIPCheckFrequency, out int checkFrequency))
                {
                    await Task.Delay(TimeSpan.FromMinutes(checkFrequency), stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }

            }
        }
    }
}
