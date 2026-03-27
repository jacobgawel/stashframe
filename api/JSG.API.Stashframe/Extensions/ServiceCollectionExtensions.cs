using Microsoft.Extensions.Caching.Hybrid;

namespace JSG.API.Stashframe.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public void AddCaching(IConfiguration configuration)
        {
            serviceCollection.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = typeof(Program).Assembly.GetName().Name!.ToLowerInvariant() + ":";
            });
            
            serviceCollection.AddHybridCache(options =>
            {
                options.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Flags = HybridCacheEntryFlags.DisableLocalCache
                };
            });
        }
    }
}