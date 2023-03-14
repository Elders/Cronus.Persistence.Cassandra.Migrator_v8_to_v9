namespace Elders.Cronus.Persistence.Cassandra.Migrator_v8_to_v9
{
    public static class Counter
    {
        public static int ProccesedCount { get; set; } = 0;

        public static bool DevidsBy10000() => ProccesedCount % 10000 == 0;
    }
}
