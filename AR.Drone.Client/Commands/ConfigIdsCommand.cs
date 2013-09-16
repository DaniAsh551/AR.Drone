namespace AR.Drone.Client.Commands
{
    public class ConfigIdsCommand : ATCommand
    {
        private readonly string _sessionId;
        private readonly string _profileId;
        private readonly string _applicationId;

        public ConfigIdsCommand(string sessionId, string profileId, string applicationId)
        {
            _sessionId = sessionId;
            _profileId = _profileId;
            _applicationId = applicationId;
        }

        protected override string ToAt(int sequenceNumber)
        {
            return string.Format("AT*CONFIG_IDS={0},\"{1}\",\"{2}\",\"{3}\"\r", sequenceNumber, _sessionId, _profileId, _applicationId);
        }
    }
}