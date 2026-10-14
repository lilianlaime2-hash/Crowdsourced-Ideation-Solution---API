using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Ideas.CreateIdea
{
    public sealed class CreateIdeaService : ICreateIdeaService
    {
        private readonly IIdeaRepository _ideaRepository;
        private readonly ITopicRepository _topicRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUsersApiClient _usersApiClient;
        private readonly TimeProvider _timeProvider;

        public CreateIdeaService(
            IIdeaRepository ideaRepository,
            ITopicRepository topicRepository,
            ICurrentUser currentUser,
            IUsersApiClient usersApiClient,
            TimeProvider timeProvider)
        {
            _ideaRepository = ideaRepository;
            _topicRepository = topicRepository;
            _currentUser = currentUser;
            _usersApiClient = usersApiClient;
            _timeProvider = timeProvider;
        }

        public async Task<CreateIdeaResponse?> ExecuteAsync(
            long topicId,
            CreateIdeaRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var userId = _currentUser.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException(
                    "An authenticated user is required.");
            }

            var topic = await _topicRepository.GetByIdAsync(
                topicId,
                cancellationToken);

            if (topic is null || topic.Status == TopicStatus.Deleted)
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var idea = new Idea(
                topicId: topic.Id,
                title: request.Title,
                description: request.Description,
                userId: userId,
                createdAt: now);

            var authorNames = await _usersApiClient.GetNamesByIdsAsync(
                [userId],
                cancellationToken);

            var createdIdea = await _ideaRepository.AddAsync(
                idea,
                cancellationToken);

            return new CreateIdeaResponse
            {
                Id = createdIdea.Id,
                TopicId = createdIdea.TopicId,
                AuthorName = authorNames.TryGetValue(createdIdea.UserId, out var authorName)
                                ? authorName: "Unknown user",
                Title = createdIdea.Title,
                Description = createdIdea.Description,
                Status = createdIdea.Status,
                CreatedAt = createdIdea.CreatedAt,
                UpdatedAt = createdIdea.UpdatedAt
            };
        }
    }
}
