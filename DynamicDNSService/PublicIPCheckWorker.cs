namespace DynamicDNSService
{
    public class PublicIPCheckWorker : BackgroundService
    {
        private readonly ILogger<PublicIPCheckWorker> _logger;
        private readonly HttpClient _httpClient;
        private readonly ChannelWriter<string> _channelWriter;
        private string _lastIp;
        private string _separator = new string('-',48);

        public PublicIPCheckWorker(ILogger<PublicIPCheckWorker> logger, ChannelWriter<string> channelWriter)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _channelWriter = channelWriter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ipInfo = await _httpClient.GetFromJsonAsync<IPInfo>("https://jsonip.com");
                    _logger.LogInformation($"{_separator}{Environment.NewLine}jsonip.com returned {ipInfo.Ip}" +
                                           $"{Environment.NewLine}{_separator}");

                    if (ipInfo.Ip != _lastIp)
                    {
                        _logger.LogWarning($"{_separator}{Environment.NewLine}IP changed from {_lastIp} to {ipInfo.Ip}" +
                                           $"{Environment.NewLine}{_separator}");
                        await _channelWriter.WriteAsync(ipInfo.Ip, stoppingToken);
                        _lastIp = ipInfo.Ip;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking IP");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
