using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Ideas.CreateIdea;
using CIS.Domain.Constants;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CIS.Tests.Ideas
{
    public class CreateIdeaServiceTests
    {
        private const long TopicId = 1;
        private const long IdeaId = 10;

        private static readonly string UserId =
            Guid.NewGuid().ToString("D");

        private static readonly DateTime CreatedAt =
            new(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);

        private static readonly DateTime Now =
            new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IIdeaRepository> _ideaRepository = new();
        private readonly Mock<ITopicRepository> _topicRepository = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Mock<IUsersApiClient> _usersApiClient = new();
        private readonly Mock<TimeProvider> _timeProvider = new();

        public CreateIdeaServiceTests()
        {
            _currentUser
                .Setup(u => u.UserId)
                .Returns(UserId);

            _timeProvider
                .Setup(t => t.GetUtcNow())
                .Returns(new DateTimeOffset(Now));

            GivenTopic(NewTopic());

            GivenAuthorNames(new Dictionary<string, string>
            {
                [UserId] = "Alessandro"
            });

            _ideaRepository
                .Setup(r => r.AddAsync(
                    It.IsAny<Idea>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Idea idea, CancellationToken _) =>
                {
                    typeof(Idea)
                        .GetProperty(nameof(Idea.Id))!
                        .SetValue(idea, IdeaId);

                    return Task.FromResult(idea);
                });
        }

        private CreateIdeaService CreateService() =>
            new(
                _ideaRepository.Object,
                _topicRepository.Object,
                _currentUser.Object,
                _usersApiClient.Object,
                _timeProvider.Object);

        private static CreateIdeaRequest Request(
            string title = "New idea",
            string description = "Idea description") =>
            new()
            {
                Title = title,
                Description = description
            };

        private static Topic NewTopic()
        {
            var topic = new Topic(
                "Topic title",
                "Topic description",
                UserId,
                CreatedAt);

            typeof(Topic)
                .GetProperty(nameof(Topic.Id))!
                .SetValue(topic, TopicId);

            return topic;
        }

        private void GivenTopic(Topic? topic) =>
            _topicRepository
                .Setup(r => r.GetByIdAsync(
                    TopicId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(topic);

        private void GivenAuthorNames(
            IReadOnlyDictionary<string, string> names) =>
            _usersApiClient
                .Setup(c => c.GetNamesByIdsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(names);

        private void VerifyNothingSaved() =>
            _ideaRepository.Verify(
                r => r.AddAsync(
                    It.IsAny<Idea>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

        [Fact]
        public async Task ExecuteAsync_ValidRequest_CreatesIdea()
        {
            var result = await CreateService().ExecuteAsync(
                TopicId,
                Request("  New idea  ", "  New description  "));

            Assert.NotNull(result);
            Assert.Equal(IdeaId, result.Id);
            Assert.Equal(TopicId, result.TopicId);
            Assert.Equal("New idea", result.Title);
            Assert.Equal("New description", result.Description);
            Assert.Equal("Alessandro", result.AuthorName);
            Assert.Equal(IdeaStatus.Active, result.Status);
            Assert.Equal(Now, result.CreatedAt);
            Assert.Equal(Now, result.UpdatedAt);

            _ideaRepository.Verify(
                r => r.AddAsync(
                    It.Is<Idea>(i =>
                        i.TopicId == TopicId &&
                        i.UserId == UserId &&
                        i.Title == "New idea" &&
                        i.Description == "New description" &&
                        i.Status == IdeaStatus.Active),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                CreateService().ExecuteAsync(TopicId, null!));

            VerifyNothingSaved();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_NoAuthenticatedUser_ThrowsUnauthorizedAccess(
            string? userId)
        {
            _currentUser
                .Setup(u => u.UserId)
                .Returns(userId);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().ExecuteAsync(TopicId, Request()));

            VerifyNothingSaved();

            _topicRepository.Verify(
                r => r.GetByIdAsync(
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_TopicDoesNotExist_ReturnsNull()
        {
            GivenTopic(null);

            var result = await CreateService().ExecuteAsync(
                TopicId,
                Request());

            Assert.Null(result);
            VerifyNothingSaved();

            _usersApiClient.Verify(
                c => c.GetNamesByIdsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_TopicIsDeleted_ReturnsNull()
        {
            var topic = NewTopic();
            topic.Delete(Now);

            GivenTopic(topic);

            var result = await CreateService().ExecuteAsync(
                TopicId,
                Request());

            Assert.Null(result);
            VerifyNothingSaved();
        }

        [Theory]
        [InlineData("", "Valid description")]
        [InlineData("   ", "Valid description")]
        [InlineData("Valid title", "")]
        [InlineData("Valid title", "   ")]
        public async Task ExecuteAsync_InvalidData_ThrowsArgumentException(
            string title,
            string description)
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(
                    TopicId,
                    Request(title, description)));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_TitleTooLong_ThrowsArgumentException()
        {
            var request = Request(
                title: new string('a', IdeaRules.MaxTitleLength + 1));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(TopicId, request));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_DescriptionTooLong_ThrowsArgumentException()
        {
            var request = Request(
                description: new string(
                    'a', IdeaRules.MaxDescriptionLength + 1));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(TopicId, request));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_InvalidUserId_ThrowsArgumentException()
        {
            _currentUser
                .Setup(u => u.UserId)
                .Returns("invalid-user-id");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(
                    TopicId,
                    Request()));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_AuthorNotFound_ReturnsUnknownUser()
        {
            GivenAuthorNames(new Dictionary<string, string>());

            var result = await CreateService().ExecuteAsync(
                TopicId,
                Request());

            Assert.NotNull(result);
            Assert.Equal("Unknown user", result.AuthorName);
        }

        [Fact]
        public async Task ExecuteAsync_ValidRequest_RequestsAuthorName()
        {
            await CreateService().ExecuteAsync(TopicId, Request());

            _usersApiClient.Verify(
                c => c.GetNamesByIdsAsync(
                    It.Is<IEnumerable<string>>(ids =>
                        ids.Single() == UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ValidRequest_SavesIdeaOnce()
        {
            await CreateService().ExecuteAsync(TopicId, Request());

            _ideaRepository.Verify(
                r => r.AddAsync(
                    It.IsAny<Idea>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ValidRequest_UsesProvidedCancellationToken()
        {
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            await CreateService().ExecuteAsync(
                TopicId,
                Request(),
                token);

            _topicRepository.Verify(
                r => r.GetByIdAsync(TopicId, token),
                Times.Once);

            _usersApiClient.Verify(
                c => c.GetNamesByIdsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    token),
                Times.Once);

            _ideaRepository.Verify(
                r => r.AddAsync(
                    It.IsAny<Idea>(),
                    token),
                Times.Once);
        }
    }
}
