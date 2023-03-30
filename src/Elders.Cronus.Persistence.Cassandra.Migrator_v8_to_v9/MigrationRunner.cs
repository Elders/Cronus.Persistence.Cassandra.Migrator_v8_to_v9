using Cassandra;
using Elders.Cronus.EventStore;
using Elders.Cronus.Migrations;
using System.Diagnostics;
using System.Text;

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
            try
            {
                bool dryRun = bool.Parse(configuration["dry-run"]);

                if (dryRun)
                    logger.Info(() => "Running Migration in DRY-RUN mode...");

                CassandraEventStorePlayer_v8 castedSource = source as CassandraEventStorePlayer_v8;

                string paginationToken = configuration["paginationToken"];
                logger.LogInformation($"Initial Pagination Token => {paginationToken}");
                bool hasMore = true;

                List<Task> tasks = new List<Task>();
                var stopwatch = new Stopwatch();

                while (hasMore)
                {
                    stopwatch.Start();
                    LoadAggregateCommitsResult data = await castedSource.LoadAggregateCommitsAsync(paginationToken, 5000).ConfigureAwait(false);
                    logger.LogInformation($"Load 5k Data -> {stopwatch.Elapsed}");

                    if (data.Commits.Any())
                        logger.LogInformation(Encoding.UTF8.GetString(data.Commits.Last().AggregateRootId));

                    hasMore = data.Commits.Any() && paginationToken != data.PaginationToken;
                    paginationToken = data.PaginationToken;

                    logger.LogInformation($"Pagination Token => {data.PaginationToken}");
                    logger.LogInformation($"Has more -> {hasMore}");

                    stopwatch.Restart();
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
                        logger.LogInformation(stopwatch.Elapsed.ToString());

                        if (ForSomeReasonTheAggregateCommitHasBeenDeleted(migrated))
                        {
                            logger.Error(() => $"An aggregate commit has been wiped from the face of the Earth. R.I.P."); // Bonus: How Пикасо is spelled in English => Piccasso, Picasso, Piccaso ?? I bet someone will git-blame me
                            continue;
                        }

                        stopwatch.Restart();
                        logger.LogInformation($"Starts appending tasks");
                        if (dryRun == false)
                        {
                            var appendTask = target.AppendAsync(migrated);

                            tasks.Add(appendTask);

                            if (tasks.Count > 100)
                            {
                                logger.LogInformation("popping task out");
                                Task completedTask = await Task.WhenAny(tasks);
                                if (completedTask.IsFaulted)
                                {
                                    logger.ErrorException(completedTask.Exception, () => "The fail");
                                }
                                tasks.Remove(completedTask);
                                logger.LogInformation("task popped outt");
                            }

                            logger.LogInformation("page done in: stopwatch.Elapsed.ToString()");
                        }
                    }
                    await Task.WhenAll(tasks);
                    logger.LogInformation($"5k Aggregate commits written to new cassandra for -> {stopwatch.Elapsed}");
                }
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

