using Cassandra;
using Elders.Cronus.EventStore;
using Elders.Cronus.Migrations;

namespace Elders.Cronus.Persistence.Cassandra.Migrator_v8_to_v9
{
    public class MigrationRunner<TSourceEventStorePlayer, TTargetEventStore> : MigrationRunnerBase<AggregateCommit, TSourceEventStorePlayer, IEventStore>
        where TSourceEventStorePlayer : class, IEventStorePlayer
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<MigrationRunner<TSourceEventStorePlayer, TTargetEventStore>> logger;

        public MigrationRunner(TSourceEventStorePlayer source, EventStoreFactory factory, IConfiguration configuration, ILogger<MigrationRunner<TSourceEventStorePlayer, TTargetEventStore>> logger) : base(source, factory.GetEventStore())
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        public override async Task RunAsync(IEnumerable<IMigration<AggregateCommit>> migrations)
        {
            bool dryRun = bool.Parse(configuration["dry-run"]);

            if (dryRun)
                logger.Info(() => "Running Migration in DRY-RUN mode...");

            CassandraEventStorePlayer_v8 castedSource = source as CassandraEventStorePlayer_v8;

            string paginationToken = configuration["paginationToken"];
            logger.LogInformation($"Initial Pagination Token => {paginationToken}");
            bool hasMore = true;
            while (hasMore)
            {
                LoadAggregateCommitsResult data = await castedSource.LoadAggregateCommitsAsync(paginationToken, 1000).ConfigureAwait(false);
                paginationToken = data.PaginationToken;
                hasMore = data.HasMoreResults;
                logger.LogInformation($"Pagination Token => {data.PaginationToken}");

                try
                {
                    foreach (AggregateCommit commit in data.Commits)
                    {
                        AggregateCommit migrated = commit;
                        foreach (var migration in migrations)
                        {
                            if (migration.ShouldApply(migrated))
                            {
                                migrated = migration.Apply(migrated);
                            }
                        }

                        if (ForSomeReasonTheAggregateCommitHasBeenDeleted(migrated))
                        {
                            logger.Error(() => $"An aggregate commit has been wiped from the face of the Earth. R.I.P."); // Bonus: How Пикасо is spelled in English => Piccasso, Picasso, Piccaso ?? I bet someone will git-blame me
                            continue;
                        }

                        if (dryRun == false)
                        {
                            await target.AppendAsync(migrated).ConfigureAwait(false);
                        }
                    }
                }

                catch (Exception ex)
                {
                    logger.ErrorException(ex, () => $"Something boom bam while runnning migration.");
                }
            }

        }
        private static bool ForSomeReasonTheAggregateCommitHasBeenDeleted(AggregateCommit aggregateCommit)
        {
            return aggregateCommit is null || aggregateCommit.Events.Any() == false;
        }
    }
}
