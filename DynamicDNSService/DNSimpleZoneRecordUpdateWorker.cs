using dnsimple;
using dnsimple.Services;

namespace DynamicDNSService
{
    public class DNSimpleZoneRecordUpdateWorker : BackgroundService
    {
        private readonly ILogger<DNSimpleZoneRecordUpdateWorker> _logger;
        private readonly ChannelReader<string> _channelReader;
        private readonly IConfiguration _configuration;

        private static string _apiKey = Environment.GetEnvironmentVariable("DNSIMPLE_API_KEY");
        private static string _zone = Environment.GetEnvironmentVariable("DNSIMPLE_ZONE_NAME");
        private static string _zoneRecordName = Environment.GetEnvironmentVariable("DNSIMPLE_ZONE_RECORD_NAME");

        private Client _dnsimpleClient = new Client();
        private OAuth2Credentials _credentials;

        private long _accountId;
        private string _zoneID;
        private long _zoneRecordID;
        private ZoneRecord _currentZoneRecord;


        public DNSimpleZoneRecordUpdateWorker(ILogger<DNSimpleZoneRecordUpdateWorker> logger, ChannelReader<string> channelReader, IConfiguration configuration)
        {
            _logger = logger;
            _channelReader = channelReader;
            _configuration = configuration;

            _credentials = new OAuth2Credentials(_apiKey);
            _dnsimpleClient.AddCredentials(_credentials);

            _accountId = _dnsimpleClient.Identity.Whoami().Data.Account.Id;
            _zoneID = _dnsimpleClient.Zones.GetZone(_accountId, _zone).Data.Id.ToString();
            _zoneRecordID = _dnsimpleClient.Zones.ListZoneRecords(_accountId, _zoneID)
                                .Data.Where(x=>x.Name == _zoneRecordName)
                                .FirstOrDefault().Id;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _channelReader.WaitToReadAsync(stoppingToken))
            {
                _logger.LogWarning("Public IP change detected, or this is service just started.");
                while (_channelReader.TryRead(out var newIp))
                {
                    try
                    {
                        GetCurrentZoneRecord();
                        UpdateZoneRecord(newIp);
                        //_logger.LogInformation($"DNS record updated with new IP: {newIp} @ {DateTime.UtcNow}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating DNS record");
                    }
                }
            }
        }

        private void GetCurrentZoneRecord()
        {
            _currentZoneRecord =  _dnsimpleClient.Zones.GetZoneRecord(_accountId, _zoneID, _zoneRecordID).Data;
            _logger.LogInformation($"Zone Record {_currentZoneRecord.Name} has following configuration:{Environment.NewLine}"+JsonSerializer.Serialize(_currentZoneRecord, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void UpdateZoneRecord(string ip)
        {
            var recordToUpdate = _currentZoneRecord;
            recordToUpdate.Content = ip;
            recordToUpdate.Regions = null;
            var response = _dnsimpleClient.Zones.UpdateZoneRecord(_accountId, _zoneID, _zoneRecordID, recordToUpdate);
            _logger.LogWarning($"Updated Zone Record {response.Data.Name} with IP {ip}.");
            //var updatedRecord = new ZoneRecord() { Name = "", con };
            //_logger.LogInformation($"DNS Record Updated! ({ip})");
            // Implement your DNS update logic here
            // Use _configuration to get DNSimple settings
            //_logger.LogInformation(_apiKey);
            
        }
    }
}
