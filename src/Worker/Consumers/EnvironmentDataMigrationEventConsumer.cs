using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Events;
using DomainService.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Worker.Consumers
{
    public class EnvironmentDataMigrationEventConsumer : IConsumer<EnvironmentDataMigrationEvent>
    {
        private readonly IKeyManagementService _keyManagementService;
        private readonly IEnvironmentDataMigrationRepository _migrationRepository;
        private readonly ILogger<EnvironmentDataMigrationEventConsumer> _logger;

        public EnvironmentDataMigrationEventConsumer(
            IKeyManagementService keyManagementService,
            IEnvironmentDataMigrationRepository migrationRepository,
            ILogger<EnvironmentDataMigrationEventConsumer> logger)
        {
            _keyManagementService = keyManagementService;
            _migrationRepository = migrationRepository;
            _logger = logger;
        }

        public async Task Consume(EnvironmentDataMigrationEvent @event)
        {
            try
            {
                _logger.LogInformation("Starting environment data migration from {ProjectKey} to {TargetedProjectKey}. OverwriteExisting: {ShouldOverwrite}",
                    @event.ProjectKey, @event.TargetedProjectKey, @event.ShouldOverWriteExistingData);

                // Migrate BlocksLanguageModule first (as Keys depend on Modules)
                await MigrateModulesAsync(@event);

                // Then migrate BlocksLanguageKey
                await MigrateKeysAsync(@event);

                _logger.LogInformation("Environment data migration completed successfully from {ProjectKey} to {TargetedProjectKey}",
                    @event.ProjectKey, @event.TargetedProjectKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Environment data migration failed from {ProjectKey} to {TargetedProjectKey}",
                    @event.ProjectKey, @event.TargetedProjectKey);
                throw;
            }
        }

        private async Task MigrateModulesAsync(EnvironmentDataMigrationEvent @event)
        {
            _logger.LogInformation("Starting BlocksLanguageModule migration from {ProjectKey} to {TargetedProjectKey}",
                @event.ProjectKey, @event.TargetedProjectKey);

            // Get source modules from source project database
            var sourceModules = await _migrationRepository.GetAllModulesAsync(@event.ProjectKey);

            if (!sourceModules.Any())
            {
                _logger.LogInformation("No modules found in source project {ProjectKey}", @event.ProjectKey);
                return;
            }

            // Prepare modules for target environment
            var targetModules = sourceModules.Select(sourceModule => new BlocksLanguageModule
            {
                ItemId = sourceModule.ItemId, // Preserve original ItemId for same project across environments
                ModuleName = sourceModule.ModuleName,
                Name = sourceModule.Name,
                CreateDate = sourceModule.CreateDate, // Preserve original create date
                LastUpdateDate = DateTime.UtcNow,
                TenantId = @event.TargetedProjectKey,
                CreatedBy = sourceModule.CreatedBy,
                LastUpdatedBy = sourceModule.LastUpdatedBy
            }).ToList();

            // Bulk upsert modules using repository
            await _migrationRepository.BulkUpsertModulesAsync(targetModules, @event.TargetedProjectKey, @event.ShouldOverWriteExistingData);

            var operationType = @event.ShouldOverWriteExistingData ? "upserted" : "inserted new";
            _logger.LogInformation("Bulk {OperationType} {Count} modules into target project {TargetedProjectKey}",
                operationType, targetModules.Count, @event.TargetedProjectKey);

            _logger.LogInformation("BlocksLanguageModule migration completed from {ProjectKey} to {TargetedProjectKey}",
                @event.ProjectKey, @event.TargetedProjectKey);
        }

        private async Task MigrateKeysAsync(EnvironmentDataMigrationEvent @event)
        {
            _logger.LogInformation("Starting BlocksLanguageKey migration from {ProjectKey} to {TargetedProjectKey}",
                @event.ProjectKey, @event.TargetedProjectKey);

            // Get source keys from source project database
            var sourceKeys = await _migrationRepository.GetAllKeysAsync(@event.ProjectKey);

            if (!sourceKeys.Any())
            {
                _logger.LogInformation("No keys found in source project {ProjectKey}", @event.ProjectKey);
                return;
            }

            // Prepare keys for target environment
            var targetKeys = sourceKeys.Select(sourceKey => new BlocksLanguageKey
            {
                ItemId = sourceKey.ItemId, // Preserve original ItemId for same project across environments
                KeyName = sourceKey.KeyName,
                ModuleId = sourceKey.ModuleId, // ModuleId should be same across environments
                Value = sourceKey.Value,
                Resources = sourceKey.Resources,
                Routes = sourceKey.Routes,
                IsPartiallyTranslated = sourceKey.IsPartiallyTranslated,
                CreateDate = sourceKey.CreateDate, // Preserve original create date
                LastUpdateDate = DateTime.UtcNow,
                TenantId = @event.TargetedProjectKey,
                CreatedBy = sourceKey.CreatedBy,
                LastUpdatedBy = sourceKey.LastUpdatedBy
            }).ToList();

            // Bulk upsert keys using repository
            await _migrationRepository.BulkUpsertKeysAsync(targetKeys, @event.TargetedProjectKey, @event.ShouldOverWriteExistingData);

            var operationType = @event.ShouldOverWriteExistingData ? "upserted" : "inserted new";
            _logger.LogInformation("Bulk {OperationType} {Count} keys into target project {TargetedProjectKey}",
                operationType, targetKeys.Count, @event.TargetedProjectKey);

            _logger.LogInformation("BlocksLanguageKey migration completed from {ProjectKey} to {TargetedProjectKey}",
                @event.ProjectKey, @event.TargetedProjectKey);
        }
    }
}
