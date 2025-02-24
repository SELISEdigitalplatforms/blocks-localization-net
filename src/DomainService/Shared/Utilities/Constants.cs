using Blocks.Genesis;

namespace DomainService.Utilities
{
    public static class Constants
    {
        public const string UilmQueue = "blocks_uilm_listener";
        public static MessageConfiguration GetMessageConfiguration()
        {
            return new MessageConfiguration
            {
                Queues = new List<string> { UilmQueue },
                Topics = new List<string> { }
            };
        } 
    }
}
