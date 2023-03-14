using Elders.Cronus.EventStore;
using Elders.Cronus.Migrations;

namespace Elders.Cronus.Persistence.Cassandra.Migrator_v8_to_v9
{
    public class EventStore_8_to_9_Migration : IMigration<AggregateCommit>
    {
        private readonly ILogger<EventStore_8_to_9_Migration> _logger;

        public EventStore_8_to_9_Migration(ILogger<EventStore_8_to_9_Migration> logger)
        {
            _logger = logger;
        }

        public AggregateCommit Apply(AggregateCommit current)
        {
            if (Counter.DevidsBy10000())
                _logger.LogInformation($"Proccessed: {Counter.ProccesedCount}");
            Counter.ProccesedCount++;

            return current;
        }

        public bool ShouldApply(AggregateCommit current) => true;
    }
}
