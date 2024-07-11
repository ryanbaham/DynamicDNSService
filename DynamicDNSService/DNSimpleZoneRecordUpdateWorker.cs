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
            //worker service DI config, comes in the box
            _logger = logger;
            _channelReader = channelReader;
            _configuration = configuration;

            //configure DNSimple client and creds
            _credentials = new OAuth2Credentials(_apiKey);
            _dnsimpleClient.AddCredentials(_credentials);

            //retrieve and store basic account and zone info
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
                //^^^ Watching for an actionable data. 
                // Fires once at startup and then only if PublicIPCheckWorker pushes data into the channel.

                _logger.LogWarning("Public IP change detected, or this service just started.");

                while (_channelReader.TryRead(out var newIp))
                {
                    try
                    {
                        GetCurrentZoneRecord(); // retrieve and store locally the current A record specified in Environment Variable

                        if (newIp != null && _currentZoneRecord.Content != newIp) 
                        {
                            UpdateZoneRecord(newIp); // if newIP isn't null and differs from current A record content, update A record specified in Environment Variable
                        }
                        else
                        {
                            _logger.LogWarning("IP from Channel ({updatedIP}) is null or equals existing zone record value ({currentZoneRecordInfo}).", newIp,_currentZoneRecord.Content);
                        }
                        
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

            _currentZoneRecord =  _dnsimpleClient.Zones.GetZoneRecord(_accountId, _zoneID, _zoneRecordID).Data; // ask DNSimple for current A record content
            _logger.LogInformation("Zone Record {zoneRecordName} has following configuration:{currentZoneRecordContent}",_currentZoneRecord.Name, JsonSerializer.Serialize(_currentZoneRecord, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void UpdateZoneRecord(string ip)
        {

            var recordToUpdate = _currentZoneRecord;
            recordToUpdate.Content = ip;
            recordToUpdate.Regions = null; //DNSimple SDK doesn't handle this correctly for non-premium users, even when retrieved zone record already specifies "all regions".

            var response = _dnsimpleClient.Zones.UpdateZoneRecord(_accountId, _zoneID, _zoneRecordID, recordToUpdate);

            _logger.LogWarning("Updated Zone Record {response.Data.Name} with IP {ip}.", response.Data.Name, ip);
            
        }
    }
}
