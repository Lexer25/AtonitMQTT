using Eventor;
using Microsoft.Extensions.Logging;
using WorkerService2;

await Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
{
    IConfiguration configuration = context.Configuration;

    Eventor_Settings options = configuration.GetSection("Service").Get<Eventor_Settings>();

    services.AddSingleton(options);

    services.AddHostedService<Worker>();
}).ConfigureLogging((context, logging) =>
{
    logging.ClearProviders();
    logging.AddConfiguration(context.Configuration.GetSection("Logging"));
    logging.AddConsole();
    string path = context.Configuration.GetSection("Log").GetValue<string>("LogFolerPath");
    int? retainedFileCountLimit = context.Configuration.GetSection("Log").GetValue<int?>("retainedFileCountLimit");
    logging.AddFile(
        pathFormat: $"{path}\\ArtonitMQTT.log",
        minimumLevel: LogLevel.Trace,
        retainedFileCountLimit: retainedFileCountLimit,
        outputTemplate: "{Timestamp:dd-MM-yyyy HH:mm:ss.fff}\t-\t[{Level:u3}] {Message}{NewLine}{Exception}");

}).UseWindowsService().Build().RunAsync();