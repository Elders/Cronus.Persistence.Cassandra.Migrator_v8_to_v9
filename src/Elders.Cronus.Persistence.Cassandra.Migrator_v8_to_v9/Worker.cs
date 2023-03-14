using Elders.Cronus.EventStore;
using Elders.Cronus.MessageProcessing;
using Elders.Cronus.Migrations;
using Elders.Cronus.Multitenancy;
using Microsoft.Extensions.Options;

namespace Elders.Cronus.Persistence.Cassandra.Migrator_v8_to_v9
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ICronusHost _cronusHost;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration configuration;
        private readonly TenantsOptions tenants;

        public Worker(ILogger<Worker> logger, ICronusHost cronusHost, IServiceProvider serviceProvider, IOptions<TenantsOptions> tenantOptions, IConfiguration configuration)
        {
            _logger = logger;
            _cronusHost = cronusHost;
            _serviceProvider = serviceProvider;
            this.configuration = configuration;
            tenants = tenantOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Migration service...");
            _cronusHost.Start();

            foreach (string tenant in tenants.Tenants)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var cronusContext = scope.ServiceProvider.GetRequiredService<CronusContext>();
                    cronusContext.Tenant = tenant;

                    IMigrationEventStorePlayer migrationEventStorePlayer = scope.ServiceProvider.GetRequiredService<CassandraEventStorePlayer_v8>();
                    MigrationRunner<CassandraEventStorePlayer_v8, IEventStore> migrationRunner = scope.ServiceProvider.GetRequiredService<MigrationRunner<CassandraEventStorePlayer_v8, IEventStore>>();

                    ILogger<EventStore_8_to_9_Migration> logger = scope.ServiceProvider.GetRequiredService<ILogger<EventStore_8_to_9_Migration>>();

                    _logger.Info(() => $"Migration started for tenant {tenant}...");
                    await migrationRunner.RunAsync(new List<IMigration<AggregateCommit>>() { new EventStore_8_to_9_Migration(logger) }).ConfigureAwait(false);
                    _logger.Info(() => $"Migration finished for tenant {tenant}!");
                }
            }

            await Task.CompletedTask;

            await StopAsync(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Relations Migration service...");
            _cronusHost.Stop();
            _logger.LogInformation("Relations Migration Service stopped!");
            return Task.CompletedTask;
        }
    }
}
