using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Services;
using Microsoft.AspNetCore.HttpOverrides;
using DotNetEnv;

Env.Load("../.env");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "FB Webhook & Proxy API", Version = "v1" });
    c.AddSecurityDefinition("AdminToken", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Admin-Token",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập secret key (vd: admin_secret_2026) để gọi các API Proxy quản trị"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "AdminToken"
                }
            },
            Array.Empty<string>()
        }
    });
});

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
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FB Webhook & Proxy API v1");
});

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
