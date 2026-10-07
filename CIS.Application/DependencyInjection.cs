using CIS.Application.Topics.CreateTopic;
using CIS.Application.Topics.GetTopic;
using CIS.Application.Topics.UpdateTopic;
using CIS.Application.Topics.DeleteTopic;
using Microsoft.Extensions.DependencyInjection;
using CIS.Application.Ideas.CreateIdea;

namespace CIS.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            //Topics
            services.AddScoped<ICreateTopicService, CreateTopicService>();

            services.AddScoped<IGetTopicService, GetTopicService>();

            services.AddScoped<IUpdateTopicService, UpdateTopicService>();

            services.AddScoped<IDeleteTopicService, DeleteTopicService>();

            //Ideas
            services.AddScoped<ICreateIdeaService, CreateIdeaService>();

            services.AddSingleton(TimeProvider.System);

            return services;
        }
    }
}
