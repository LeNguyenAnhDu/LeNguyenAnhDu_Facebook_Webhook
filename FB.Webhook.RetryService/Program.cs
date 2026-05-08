using FB.Webhook.RetryService.Workers;
using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNetEnv;

Env.Load("../.env");

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    // Đăng ký Services
    services.AddSingleton<IKafkaProducer, KafkaProducerService>();

    // Đăng ký Worker
    services.AddHostedService<RetryWorker>();
});

var host = builder.Build();
host.Run();
