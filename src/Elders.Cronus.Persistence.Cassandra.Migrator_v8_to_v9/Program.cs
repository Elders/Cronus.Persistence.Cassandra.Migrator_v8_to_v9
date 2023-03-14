using Elders.Cronus;
using Elders.Cronus.Migrations;
using Logging;
using Serilog;

namespace Elders.Cronus.Persistence.Cassandra.Migrator_v8_to_v9
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string appName = Environment.GetEnvironmentVariable("app_name");

            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "migration", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "migration", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("log_name", appName + ".migration", EnvironmentVariableTarget.Process);

            Console.Write("Environment:");
            Console.WriteLine(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"));

            Console.WriteLine($"Application name: {appName}");

            Start.WithStartupDiagnostics(appName + ".Migration", () => CreateHostBuilder(args).Build().Run());
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(x => x.AddEnvironmentVariables())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();
                services.AddLogging();
                services.AddHostedService<Worker>();
                services.AddCronus(hostContext.Configuration);
                services.AddScoped(typeof(MigrationRunner<,>));
                services.AddScoped<IMigrationEventStorePlayer, CassandraEventStorePlayer_v8>();
                services.AddScoped<CassandraEventStorePlayer_v8>();
            })
            .UseSerilog(SerilogConfiguration.Configure);
    }
}
