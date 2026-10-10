using CIS.Application.Abstractions.Persistence;
using CIS.Application.Ideas.CreateIdea;
using CIS.Domain.Enums;

namespace CIS.Application.Ideas.GetIdeas;

public sealed class GetIdeasService : IGetIdeasService
{
    private readonly IIdeaRepository _ideaRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IUsersApiClient _usersApiClient;

    public GetIdeasService(
        IIdeaRepository ideaRepository,
        ITopicRepository topicRepository,
        IUsersApiClient usersApiClient)
    {
        _ideaRepository = ideaRepository;
        _topicRepository = topicRepository;
        _usersApiClient = usersApiClient;
    }

    public async Task<IReadOnlyList<CreateIdeaResponse>?> GetByTopicIdAsync(
        long topicId,
        CancellationToken cancellationToken = default)
    {
        var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

        if (topic is null || topic.Status == TopicStatus.Deleted)
        {
            return null;
        }

        var ideas = await _ideaRepository.GetByTopicIdAsync(topicId, cancellationToken);

        if (ideas.Count == 0)
        {
            return [];
        }
        
        var authorNames = await _usersApiClient.GetNamesByIdsAsync(
            ideas.Select(i => i.UserId),
            cancellationToken);

        return ideas
            .Select(i => IdeaMapper.ToResponse(i, authorNames))
            .ToList();
    }
}