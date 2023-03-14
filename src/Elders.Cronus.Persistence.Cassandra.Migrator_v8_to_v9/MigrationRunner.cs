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

            var data = source.LoadAggregateCommitsAsync();

            try
            {
                List<Task> tasks = new List<Task>();
                await foreach (AggregateCommit sourceCommit in data)
                {
                    AggregateCommit migrated = sourceCommit;
                    foreach (var migration in migrations)
                    {
                        if (migration.ShouldApply(sourceCommit))
                        {
                            migrated = migration.Apply(sourceCommit);
                        }
                    }

                    if (ForSomeReasonTheAggregateCommitHasBeenDeleted(migrated))
                    {
                        logger.Error(() => $"An aggregate commit has been wiped from the face of the Earth. R.I.P."); // Bonus: How Пикасо is spelled in English => Piccasso, Picasso, Piccaso ?? I bet someone will git-blame me
                        continue;
                    }

                    if (dryRun == false)
                    {
                        Task task = target.AppendAsync(migrated);
                        tasks.Add(task);

                        if (tasks.Count > 100)
                        {
                            Task finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                            tasks.Remove(finished);
                        }
                    }
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            catch (Exception ex)
            {
                logger.ErrorException(ex, () => $"Something boom bam while runnning migration.");
            }

        }
        private static bool ForSomeReasonTheAggregateCommitHasBeenDeleted(AggregateCommit aggregateCommit)
        {
            return aggregateCommit is null || aggregateCommit.Events.Any() == false;
        }
    }
}
