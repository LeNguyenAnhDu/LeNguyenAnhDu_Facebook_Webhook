using FB.Webhook.Shared.Services;
using FB.Webhook.CoreService.Services;
using FB.Webhook.CoreService.Workers;
using FB.Webhook.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNetEnv;

Env.Load("../.env");

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    // Đăng ký HttpClient
    services.AddHttpClient<IAiService, GeminiAiService>();
    services.AddHttpClient<IFacebookApiService, FacebookApiService>();

    // Đăng ký Services
    services.AddSingleton<ISpamDetectorService, SpamDetectorService>();
    services.AddSingleton<IKafkaProducer, KafkaProducerService>(); // Dùng lại implementation đã viết (có thể phải duplicate hoặc move KafkaProducerService vào thư mục Shared. Ở đây move code qua API cho tiện, nhưng dùng chung class).
    services.AddSingleton<IStateTracker, StateTrackerService>();

    // Đăng ký Worker
    services.AddHostedService<CoreWorker>();
});

var host = builder.Build();
host.Run();
