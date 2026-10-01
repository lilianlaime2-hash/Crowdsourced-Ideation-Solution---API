using CIS.Application.Topics.CreateTopic;
using Microsoft.Extensions.DependencyInjection;

namespace CIS.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            services.AddScoped<
                ICreateTopicService,
                CreateTopicService>();

            services.AddSingleton(TimeProvider.System);

            return services;
        }
    }
}
