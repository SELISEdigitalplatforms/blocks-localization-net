namespace DomainService.Shared.Events
{
    public class TranslateAllEvent
    {
        public string MessageCoRelationId { get; set; }
        public string? ProjectKey { get; set; }
        public string DefaultLanguage { get; set; }
    }
}
