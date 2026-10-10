using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Ideas.CreateIdea
{
    public interface ICreateIdeaService
    {
        Task<CreateIdeaResponse?> ExecuteAsync(
            long topicId,
            CreateIdeaRequest request,
            CancellationToken cancellationToken = default);
    }
}
