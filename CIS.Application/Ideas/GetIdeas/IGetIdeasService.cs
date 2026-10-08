using CIS.Application.Ideas.CreateIdea;

namespace CIS.Application.Ideas.GetIdeas;

public interface IGetIdeasService
{
    Task<IReadOnlyList<CreateIdeaResponse>?> GetByTopicIdAsync(
        long topicId,
        CancellationToken cancellationToken = default);
}