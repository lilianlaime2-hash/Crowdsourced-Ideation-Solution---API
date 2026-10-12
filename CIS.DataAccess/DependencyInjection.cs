using CIS.Application.Abstractions.Persistence;
using CIS.DataAccess.Context;
using CIS.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CIS.Application.Ideas.UpdateIdea;


namespace CIS.DataAccess
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration
                .GetConnectionString("LegacyDatabase");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("LegacyDatabase connection string is missing.");
            }

            services.AddDbContext<CisDbContext>(options => options.UseMySQL(connectionString, mysqlOptions =>
            {
                mysqlOptions.MigrationsHistoryTable("__CIS_EFMigrationsHistory");
            }));

            services.AddScoped<ITopicRepository, TopicRepository>();
            services.AddScoped<ITagRepository, TagRepository>();
            services.AddScoped<IIdeaRepository, IdeaRepository>();
            services.AddScoped<IVoteRepository, VoteRepository>();
            services.AddScoped<IUpdateIdeaService, UpdateIdeaService>();
            services.AddScoped<IStatisticsRepository, StatisticsRepository>();

            return services;
        }
    }
}
