namespace DomainService.Shared.Events
{
    public class EnvironmentDataMigrationEvent
    {
        public required string ProjectKey { get; set; }
        public required string TargetedProjectKey { get; set; }
        public bool ShouldOverWriteExistingData { get; set; } = false;
    }
}