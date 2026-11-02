using CIS.Application.Abstractions.Authentication;
using CIS.Application.Abstractions.Persistence;
using CIS.Application.Topics.CreateTopic;
using CIS.Domain.Constants;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CIS.Tests.Topics
{
    public class CreateTopicServiceTests
    {
        private static readonly string UserId =
            Guid.NewGuid().ToString("D");

        private static readonly DateTime Now =
            new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<ITopicRepository> _topicRepository = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Mock<IUsersApiClient> _usersApiClient = new();
        private readonly Mock<ITagRepository> _tagRepository = new();
        private readonly Mock<TimeProvider> _timeProvider = new();

        public CreateTopicServiceTests()
        {
            _currentUser
                .Setup(u => u.UserId)
                .Returns(UserId);

            _timeProvider
                .Setup(t => t.GetUtcNow())
                .Returns(new DateTimeOffset(Now));

            GivenAuthorNames(new Dictionary<string, string>
            {
                [UserId] = "Alessandro"
            });

            _topicRepository
                .Setup(r => r.AddAsync(
                    It.IsAny<Topic>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Topic topic, CancellationToken _) =>
                    Task.FromResult(topic));
        }

        private CreateTopicService CreateService() =>
            new(
                _topicRepository.Object,
                _currentUser.Object,
                _usersApiClient.Object,
                _timeProvider.Object,
                _tagRepository.Object);

        private static CreateTopicRequest Request(
            string title = "New topic",
            string description = "Topic description",
            List<string>? tags = null) =>
            new()
            {
                Title = title,
                Description = description,
                Tags = tags ?? []
            };

        private void GivenAuthorNames(
            IReadOnlyDictionary<string, string> names)
        {
            _usersApiClient
                .Setup(c => c.GetNamesByIdsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(names);
        }

        private void VerifyNothingSaved() =>
            _topicRepository.Verify(
                r => r.AddAsync(
                    It.IsAny<Topic>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

        [Fact]
        public async Task ExecuteAsync_ValidRequest_CreatesTopic()
        {
            var result = await CreateService().ExecuteAsync(
                Request("  New topic  ", "  New description  "));

            Assert.NotNull(result);
            Assert.Equal("New topic", result.Title);
            Assert.Equal("New description", result.Description);
            Assert.Equal("Alessandro", result.AuthorName);
            Assert.Equal(TopicStatus.Active, result.Status);
            Assert.Equal(Now, result.CreatedAt);
            Assert.Equal(Now, result.UpdatedAt);
            Assert.Empty(result.Tags);

            _topicRepository.Verify(
                r => r.AddAsync(
                    It.Is<Topic>(t =>
                        t.Title == "New topic" &&
                        t.Description == "New description" &&
                        t.UserId == UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                CreateService().ExecuteAsync(null!));

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
                CreateService().ExecuteAsync(Request()));

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
                    Request(title, description)));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_TitleTooLong_ThrowsArgumentException()
        {
            var request = Request(
                new string('a', TopicRules.MaxTitleLength + 1));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(request));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_DescriptionTooLong_ThrowsArgumentException()
        {
            var request = Request(
                description: new string(
                    'a', TopicRules.MaxDescriptionLength + 1));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(request));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_InvalidUserId_ThrowsArgumentException()
        {
            _currentUser
                .Setup(u => u.UserId)
                .Returns("invalid-user-id");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(Request()));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_ValidTags_AddsTagsToTopic()
        {
            var request = Request(
                tags: ["C#", "dotnet"]);

            var result = await CreateService().ExecuteAsync(request);

            Assert.Equal(2, result.Tags.Count);
            Assert.Contains("C#", result.Tags);
            Assert.Contains("dotnet", result.Tags);

            _topicRepository.Verify(
                r => r.AddAsync(
                    It.Is<Topic>(t =>
                        t.TopicTags.Count == 2),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_DuplicateTags_AddsOnlyOneTag()
        {
            var request = Request(
                tags: ["dotnet", "DOTNET", " dotnet "]);

            var result = await CreateService().ExecuteAsync(request);

            Assert.Single(result.Tags);
            Assert.Equal("dotnet", result.Tags[0]);

            _tagRepository.Verify(
                r => r.GetByNormalizedNameAsync(
                    "DOTNET",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ExistingTag_ReusesTag()
        {
            var existingTag = new Tag("DotNet");

            _tagRepository
                .Setup(r => r.GetByNormalizedNameAsync(
                    "DOTNET",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTag);

            var result = await CreateService().ExecuteAsync(
                Request(tags: ["dotnet"]));

            Assert.Single(result.Tags);
            Assert.Equal("DotNet", result.Tags[0]);

            _topicRepository.Verify(
                r => r.AddAsync(
                    It.Is<Topic>(t =>
                        t.TopicTags.Any(tt =>
                            ReferenceEquals(tt.Tag, existingTag))),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidTag_ThrowsArgumentException()
        {
            var request = Request(tags: ["$$$"]);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(request));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_TooManyTags_ThrowsArgumentException()
        {
            var tags = Enumerable
                .Range(1, TagRules.MaxTagsPerTopic + 1)
                .Select(i => $"Tag{i}")
                .ToList();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService().ExecuteAsync(
                    Request(tags: tags)));

            VerifyNothingSaved();
        }

        [Fact]
        public async Task ExecuteAsync_AuthorNotFound_ReturnsUnknownUser()
        {
            GivenAuthorNames(new Dictionary<string, string>());

            var result = await CreateService().ExecuteAsync(Request());

            Assert.Equal("Unknown user", result.AuthorName);
        }

        [Fact]
        public async Task ExecuteAsync_ValidRequest_RequestsAuthorName()
        {
            await CreateService().ExecuteAsync(Request());

            _usersApiClient.Verify(
                c => c.GetNamesByIdsAsync(
                    It.Is<IEnumerable<string>>(ids =>
                        ids.Single() == UserId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
