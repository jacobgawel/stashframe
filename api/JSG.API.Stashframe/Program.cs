using Azure.Identity;
using Azure.Storage.Blobs;
using JSG.API.Stashframe.Core.Database;
using JSG.API.Stashframe.Core.Interfaces.Repositories;
using JSG.API.Stashframe.Core.Interfaces.Services;
using JSG.API.Stashframe.Core.Sagas.MediaProcessing;
using JSG.API.Stashframe.Extensions;
using JSG.API.Stashframe.Repositories;
using JSG.API.Stashframe.Services;
using JSG.API.Stashframe.Services.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

var credential = new DefaultAzureCredential();

// Add services to the container.

#region Cache

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = typeof(Program).Assembly.GetName().Name!.ToLowerInvariant() + ":";
});

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };
});

#endregion

#region Message Queue

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessImageConsumer>();

    x.UsingAzureServiceBus((context, cfg) =>
    {
        var sbNamespace = builder.Configuration["AzureServiceBus:Namespace"]
            ?? throw new InvalidOperationException("AzureServiceBus:Namespace is not configured.");

        cfg.Host(new Uri($"sb://{sbNamespace}.servicebus.windows.net"), h =>
        {
            h.TokenCredential = credential;
        });
        cfg.UseServiceBusMessageScheduler();
        cfg.ConfigureEndpoints(context);
    });

    x.AddSagaStateMachine<MediaProcessingSaga, MediaProcessingState>()
        .RedisRepository(r =>
        {
            r.ConnectionFactory(_ => 
                ConnectionMultiplexer.Connect(
                    builder.Configuration.GetConnectionString("Redis")!));
        });
});

#endregion

#region Blob Storage

var blobAccountName = builder.Configuration["AzureBlobStorage:AccountName"]
    ?? throw new InvalidOperationException("AzureBlobStorage:AccountName is not configured.");

var blobUri = new Uri($"https://{blobAccountName}.blob.core.windows.net");
builder.Services.AddSingleton(new BlobServiceClient(blobUri, credential));

#endregion

#region Database

builder.Services.AddDbContext<StashframeContext>(cfg =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    cfg.UseNpgsql(connectionString);
});

#endregion

builder.Services.AddScoped<IMediaStorageRepository, MediaStorageRepository>();
builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Fail fast if Redis is unreachable
await using (var redis = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!))
{
    redis.GetDatabase().Ping();
}

await app.EnsureBlobContainersAsync();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
