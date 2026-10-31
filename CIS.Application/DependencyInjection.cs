using CIS.Application.Topics.CreateTopic;
using CIS.Application.Topics.GetTopic;
using CIS.Application.Topics.UpdateTopic;
using CIS.Application.Topics.DeleteTopic;
using Microsoft.Extensions.DependencyInjection;
using CIS.Application.Ideas.CreateIdea;
using CIS.Application.Ideas.GetIdeas;
using CIS.Application.Ideas.DeleteIdea;
using CIS.Application.Ideas.DeleteVote;
using CIS.Application.Ideas.VoteIdea;
using CIS.Application.Statistics.GetMostVotedIdeas;
using CIS.Application.Statistics.GetMostActiveTopics;

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

            services.AddScoped<IGetIdeasService, GetIdeasService>();

            services.AddScoped<IDeleteIdeaService, DeleteIdeaService>();

            services.AddScoped<IVoteIdeaService, VoteIdeaService>();

            services.AddScoped<IDeleteVoteService, DeleteVoteService>();

            services.AddScoped<IGetMostVotedIdeasService, GetMostVotedIdeasService>();

            services.AddScoped<IGetMostActiveTopicsService, GetMostActiveTopicsService>();

            services.AddSingleton(TimeProvider.System);

            return services;
        }
    }
}
