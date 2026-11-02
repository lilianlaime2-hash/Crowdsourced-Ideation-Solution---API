using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Topics.CreateTopic
{
    public interface ICreateTopicService
    {
        Task<TopicResponse> ExecuteAsync(CreateTopicRequest request, CancellationToken cancellationToken = default);
    }
}
