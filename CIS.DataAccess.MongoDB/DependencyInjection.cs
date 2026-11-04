using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.MongoDB.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace CIS.DataAccess.MongoDB
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMongoDataAccess(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("MongoDatabase");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "MongoDatabase connection string is missing.");
            }

            var mongoUrl = new MongoUrl(connectionString);

            if (string.IsNullOrWhiteSpace(mongoUrl.DatabaseName))
            {
                throw new InvalidOperationException(
                    "MongoDB connection string must include a database name.");
            }

            services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoUrl));

            services.AddSingleton<IMongoDatabase>(sp =>
                sp.GetRequiredService<IMongoClient>()
                  .GetDatabase(mongoUrl.DatabaseName));

            // Identifier generation
            services.AddSingleton<MongoSequence>();

            services.AddScoped<ITopicRepository, MongoTopicRepository>();
            services.AddScoped<ITagRepository, MongoTagRepository>();
            services.AddScoped<IIdeaRepository, MongoIdeaRepository>();
            services.AddScoped<IVoteRepository, MongoVoteRepository>();
            services.AddScoped<IStatisticsRepository, MongoStatisticsRepository>();

            return services;
        }
    }
}
