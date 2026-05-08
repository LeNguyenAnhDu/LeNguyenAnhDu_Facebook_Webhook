using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Services;
using Microsoft.AspNetCore.HttpOverrides;
using DotNetEnv;

Env.Load("../.env");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Đăng ký dịch vụ DI
builder.Services.AddSingleton<IStateTracker, StateTrackerService>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducerService>();

// Cấu hình Forwarded Headers cho ngrok
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

// Cho phép buffer body để đọc body raw phục vụ VerifySignature
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next();
});

app.UseAuthorization();

app.MapControllers();

app.Run();
