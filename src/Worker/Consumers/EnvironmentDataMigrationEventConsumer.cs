using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared.Events;
using DomainService.Shared.Entities;
using DomainService.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using DomainService.Utilities;

namespace Worker.Consumers
{
    public class EnvironmentDataMigrationEventConsumer : IConsumer<EnvironmentDataMigrationEvent>
    {
        private readonly IKeyManagementService _keyManagementService;
        private readonly IEnvironmentDataMigrationRepository _migrationRepository;
        private readonly ILogger<EnvironmentDataMigrationEventConsumer> _logger;
        private readonly IMessageClient _messageClient;

        public EnvironmentDataMigrationEventConsumer(
            IKeyManagementService keyManagementService,
            IEnvironmentDataMigrationRepository migrationRepository,
            ILogger<EnvironmentDataMigrationEventConsumer> logger,
            IMessageClient messageClient)
        {
            _keyManagementService = keyManagementService;
            _migrationRepository = migrationRepository;
            _logger = logger;
            _messageClient = messageClient;
        }

        public async Task Consume(EnvironmentDataMigrationEvent @event)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                _logger.LogInformation("Starting environment data migration from {ProjectKey} to {TargetedProjectKey}. OverwriteExisting: {ShouldOverwrite}",
                    @event.ProjectKey, @event.TargetedProjectKey, @event.ShouldOverWriteExistingData);

                // Migrate BlocksLanguageModule first (as Keys depend on Modules)
                await MigrateModulesAsync(@event);

                // Then migrate BlocksLanguageKey
                await MigrateKeysAsync(@event);

                // Update migration tracker for LanguageService completion
                if (!string.IsNullOrEmpty(@event.TrackerId))
                {
                    var languageServiceStatus = new ServiceMigrationStatus
                    {
                        ShouldOverWriteExistingData = @event.ShouldOverWriteExistingData,
                        IsCompleted = true,
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        QueueName = Constants.EnvironmentDataMigrationQueue
                    };

                    // await _migrationRepository.UpdateMigrationTrackerAsync(@event.TrackerId, languageServiceStatus);
                    await NotifyMigrationCompletion(@event.TrackerId, isSuccess: true);
                    _logger.LogInformation("Updated migration tracker {TrackerId} for LanguageService completion", @event.TrackerId);
                }

                // Send notification for successful migration
                // await _keyManagementService.PublishEnvironmentDataMigrationNotification(
                //     response: true,
                //     messageCoRelationId: @event.TrackerId,
                //     projectKey: @event.ProjectKey,
                //     targetedProjectKey: @event.TargetedProjectKey);

                

                _logger.LogInformation("Environment data migration completed successfully from {ProjectKey} to {TargetedProjectKey}",
                    @event.ProjectKey, @event.TargetedProjectKey);
            }
            catch (Exception ex)
            {
                // Update migration tracker with error status if TrackerId is provided
                if (!string.IsNullOrEmpty(@event.TrackerId))
                {
                    try
                    {
                        var languageServiceErrorStatus = new ServiceMigrationStatus
                        {
                            ShouldOverWriteExistingData = @event.ShouldOverWriteExistingData,
                            IsCompleted = false,
                            StartedAt = startTime,
                            ErrorMessage = ex.Message,
                            QueueName = Constants.EnvironmentDataMigrationQueue
                        };

                        // await _migrationRepository.UpdateMigrationTrackerAsync(@event.TrackerId, languageServiceErrorStatus);
                        _logger.LogInformation("Updated migration tracker {TrackerId} with error status", @event.TrackerId);
                    }
                    catch (Exception trackerEx)
                    {
                        _logger.LogError(trackerEx, "Failed to update migration tracker {TrackerId} with error status", @event.TrackerId);
                    }
                }

                // Send notification for failed migration
                await NotifyMigrationCompletion(@event.TrackerId, isSuccess: false, errorMessage: ex.Message);
                await _keyManagementService.PublishEnvironmentDataMigrationNotification(
                    response: false,
                    messageCoRelationId: @event.TrackerId,
                    projectKey: @event.ProjectKey,
                    targetedProjectKey: @event.TargetedProjectKey);

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

            // Get existing keys from target environment for PreviousData in timeline
            var sourceKeyIds = sourceKeys.Select(k => k.ItemId).ToList();
            var existingTargetKeys = await _migrationRepository.GetExistingKeysByItemIdsAsync(sourceKeyIds, @event.TargetedProjectKey);

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

            // Bulk upsert keys using repository and get information about what was actually affected
            var upsertResult = await _migrationRepository.BulkUpsertKeysAsync(targetKeys, @event.TargetedProjectKey, @event.ShouldOverWriteExistingData);

            var operationType = @event.ShouldOverWriteExistingData ? "upserted" : "inserted new";
            var affectedKeysCount = @event.ShouldOverWriteExistingData ? targetKeys.Count : upsertResult.InsertedKeys.Count;
            _logger.LogInformation("Bulk {OperationType} {Count} keys into target project {TargetedProjectKey}",
                operationType, affectedKeysCount, @event.TargetedProjectKey);

            // Create timeline entries based on the ShouldOverWriteExistingData flag
            if (@event.ShouldOverWriteExistingData)
            {
                // When overwriting, create timeline entries for all keys with their previous data
                await _keyManagementService.CreateBulkKeyTimelineEntriesAsync(targetKeys, existingTargetKeys, "EnvironmentDataMigration", @event.TargetedProjectKey);
            }
            else
            {
                // When not overwriting, only create timeline entries for keys that were actually inserted (new keys)
                if (upsertResult.InsertedKeys.Any())
                {
                    await _keyManagementService.CreateBulkKeyTimelineEntriesAsync(upsertResult.InsertedKeys, "EnvironmentDataMigration", @event.TargetedProjectKey);
                }
            }

            _logger.LogInformation("BlocksLanguageKey migration completed from {ProjectKey} to {TargetedProjectKey}",
                @event.ProjectKey, @event.TargetedProjectKey);
        }

        private async Task NotifyMigrationCompletion(string trackerId, bool isSuccess, string? errorMessage = null)
        {
            var completionEvent = new MigrationCompletionEvent
            {
                TrackerId = trackerId,
                ServiceName = "Language",
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage
            };

            await _messageClient.SendToConsumerAsync(new ConsumerMessage<MigrationCompletionEvent>
            {
                ConsumerName = "blocks_migration_completion_listener",
                Payload = completionEvent
            });
        }
    }
}
