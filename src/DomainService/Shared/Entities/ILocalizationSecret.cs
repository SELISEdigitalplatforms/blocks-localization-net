namespace DomainService.Shared.Entities
{
    public interface ILocalizationSecret
    {
        public string ChatGptEncryptionSalt { get; set; }
        public string ChatGptEncryptionKey { get; set; }
    }
}