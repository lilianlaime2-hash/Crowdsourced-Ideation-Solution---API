using CIS.Application.Common.Sorting;
using CIS.Application.Ideas.BrowseIdeas;
using CIS.Application.Topics.BrowseTopics;
using CIS.DataAccess.Context;
using CIS.DataAccess.Repositories;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;

namespace CIS.IntegrationTests.MySql;

[CollectionDefinition(Name)]
public sealed class MySqlCollection : ICollectionFixture<MySqlFixture>
{
    public const string Name = "MySQL repository integration";
}

public sealed class MySqlFixture : IAsyncLifetime
{
    // Testcontainers allocates a disposable database and a random host port.
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4").Build();
    public string ConnectionString => _container.GetConnectionString();
    public Task InitializeAsync() => _container.StartAsync();
    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[Collection(MySqlCollection.Name)]
public sealed class MySqlRepositoryIntegrationTests(MySqlFixture fixture) : IAsyncLifetime
{
    private const string Owner = "11111111-1111-4111-8111-111111111111";
    private const string Other = "22222222-2222-4222-8222-222222222222";
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private CisDbContext _context = null!;
    private TopicRepository Topics => new(_context);
    private IdeaRepository Ideas => new(_context);
    private VoteRepository Votes => new(_context);

    public async Task InitializeAsync()
    {
        _context = new CisDbContext(new DbContextOptionsBuilder<CisDbContext>()
            .UseMySQL(fixture.ConnectionString).Options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private static Topic Topic(string title, DateTime? date = null, string user = Owner) =>
        new(title, "MySQL searchable description", user, date ?? Now);
    private static Idea Idea(long topicId, string title, DateTime? date = null, string user = Owner) =>
        new(topicId, title, "MySQL searchable description", user, date ?? Now);

    [Fact]
    public async Task Topics_PersistTagsReadUpdateAndHideSoftDeletedRows()
    {
        var topic = Topic("Original");
        topic.AddTag(new Tag(" C# "));
        await Topics.AddAsync(topic);
        var deleted = await Topics.AddAsync(Topic("Deleted"));
        deleted.Delete(Now.AddMinutes(1));
        await Topics.UpdateAsync(deleted);
        _context.ChangeTracker.Clear();

        var loaded = Assert.IsType<Topic>(await Topics.GetByIdAsync(topic.Id));
        Assert.Equal("C#", Assert.Single(loaded.TopicTags).Tag.Name);
        loaded.Update("Changed", "Updated description", Now.AddMinutes(2));
        await Topics.UpdateAsync(loaded);
        _context.ChangeTracker.Clear();
        Assert.Equal("Changed", (await Topics.GetByIdAsync(topic.Id))!.Title);
        Assert.Null(await Topics.GetByIdAsync(long.MaxValue));
        Assert.Equal(topic.Id, Assert.Single(await Topics.GetAllAsync()).Id);
        var page = await Topics.GetPagedAsync(1, 1);
        Assert.Equal(1, page.TotalItems);
        Assert.Equal(topic.Id, Assert.Single(page.Items).Id);
        Assert.Equal("C#", Assert.Single(page.Items[0].TopicTags).Tag.Name);
        Assert.Empty((await Topics.GetPagedAsync(2, 1)).Items);
        var tagRepository = new TagRepository(_context);
        Assert.Equal("C#", (await tagRepository.GetByNormalizedNameAsync("C#"))!.Name);
        Assert.Null(await tagRepository.GetByNormalizedNameAsync("ABSENT"));
    }

    [Theory]
    [InlineData(TopicSortField.Title, SortDirection.Asc)]
    [InlineData(TopicSortField.Title, SortDirection.Desc)]
    [InlineData(TopicSortField.UpdatedAt, SortDirection.Asc)]
    [InlineData(TopicSortField.UpdatedAt, SortDirection.Desc)]
    [InlineData(TopicSortField.CreatedAt, SortDirection.Asc)]
    [InlineData(TopicSortField.CreatedAt, SortDirection.Desc)]
    [InlineData(TopicSortField.Status, SortDirection.Asc)]
    [InlineData(TopicSortField.Status, SortDirection.Desc)]
    public async Task Topics_BrowseFiltersAndSortsWithStableTieBreaks(TopicSortField sort, SortDirection direction)
    {
        var earlier = Topic("Alpha", Now);
        earlier.AddTag(new Tag("C#"));
        await Topics.AddAsync(earlier);
        var later = Topic("Zulu", Now.AddMinutes(1));
        later.AddTag(Assert.Single(earlier.TopicTags).Tag);
        await Topics.AddAsync(later);
        await Topics.AddAsync(Topic("Different owner", user: Other));
        var filtered = await Topics.BrowseAsync(new TopicBrowseOptions
        {
            Status = TopicStatus.Active,
            UserId = " " + Owner + " ",
            Tag = " c# ",
            Search = " searchable ",
            SortBy = sort,
            SortDirection = direction,
            PageSize = 1
        });
        Assert.Equal(2, filtered.TotalItems);
        Assert.Equal(direction == SortDirection.Asc ? earlier.Id : later.Id, Assert.Single(filtered.Items).Id);
        Assert.Single(filtered.Items[0].TopicTags);
        Assert.Equal(3, (await Topics.BrowseAsync(new TopicBrowseOptions())).TotalItems);
        Assert.Empty((await Topics.BrowseAsync(new TopicBrowseOptions { Search = "missing" })).Items);
    }

    [Fact]
    public async Task Ideas_PersistUpdatesAndScopePaginationToParentExcludingDeleted()
    {
        var topic = await Topics.AddAsync(Topic("Parent"));
        var other = await Topics.AddAsync(Topic("Other"));
        var idea = await Ideas.AddAsync(Idea(topic.Id, "Original"));
        var deleted = await Ideas.AddAsync(Idea(topic.Id, "Deleted"));
        await Ideas.AddAsync(Idea(other.Id, "Other parent"));
        deleted.Delete(Now.AddMinutes(1));
        await Ideas.UpdateAsync(deleted);
        idea.Update("Changed", "Updated idea", Now.AddMinutes(2));
        await Ideas.UpdateAsync(idea);
        _context.ChangeTracker.Clear();
        var loaded = Assert.IsType<Idea>(await Ideas.GetByIdAsync(idea.Id));
        Assert.Equal("Changed", loaded.Title);
        Assert.Equal("Updated idea", loaded.Description);
        Assert.Equal(Now.AddMinutes(2), loaded.UpdatedAt);
        Assert.Null(await Ideas.GetByIdAsync(long.MaxValue));
        Assert.Equal(2, (await Ideas.GetByTopicIdAsync(topic.Id)).Count);
        var page = await Ideas.GetPagedByTopicIdAsync(topic.Id, 1, 1);
        Assert.Equal(1, page.TotalItems);
        Assert.Equal(idea.Id, Assert.Single(page.Items).Id);
        Assert.Empty((await Ideas.GetPagedByTopicIdAsync(topic.Id, 2, 1)).Items);
    }

    [Theory]
    [InlineData(IdeaSortField.Title, SortDirection.Asc)]
    [InlineData(IdeaSortField.Title, SortDirection.Desc)]
    [InlineData(IdeaSortField.UpdatedAt, SortDirection.Asc)]
    [InlineData(IdeaSortField.UpdatedAt, SortDirection.Desc)]
    [InlineData(IdeaSortField.CreatedAt, SortDirection.Asc)]
    [InlineData(IdeaSortField.CreatedAt, SortDirection.Desc)]
    public async Task Ideas_BrowseFiltersSortsAndPaginates(IdeaSortField sort, SortDirection direction)
    {
        var parent = await Topics.AddAsync(Topic("Parent"));
        var first = await Ideas.AddAsync(Idea(parent.Id, "Alpha", Now));
        var second = await Ideas.AddAsync(Idea(parent.Id, "Zulu", Now.AddMinutes(1)));
        await Ideas.AddAsync(Idea(parent.Id, "Different owner", user: Other));
        var page = await Ideas.BrowseAsync(parent.Id, new IdeaBrowseOptions
        {
            Status = IdeaStatus.Active,
            UserId = " " + Owner + " ",
            Search = " searchable ",
            SortBy = sort,
            SortDirection = direction,
            PageSize = 1
        });
        Assert.Equal(2, page.TotalItems);
        Assert.Equal(direction == SortDirection.Asc ? first.Id : second.Id, Assert.Single(page.Items).Id);
        Assert.Equal(3, (await Ideas.BrowseAsync(parent.Id, new IdeaBrowseOptions())).TotalItems);
        Assert.Empty((await Ideas.BrowseAsync(parent.Id, new IdeaBrowseOptions { Search = "missing" })).Items);
    }

    [Fact]
    public async Task Votes_EnforceUniqueUserIdeaAndDeleteOnlyRequestedVote()
    {
        var topic = await Topics.AddAsync(Topic("Parent"));
        var idea = await Ideas.AddAsync(Idea(topic.Id, "Idea"));
        Assert.False(await Votes.ExistsAsync(idea.Id, Owner));
        Assert.Null(await Votes.GetAsync(idea.Id, Owner));
        var vote = await Votes.AddAsync(new Vote(idea.Id, Owner, Now));
        await Votes.AddAsync(new Vote(idea.Id, Other, Now));
        Assert.True(await Votes.ExistsAsync(idea.Id, Owner));
        Assert.Equal(vote.Id, (await Votes.GetAsync(idea.Id, Owner))!.Id);
        await Assert.ThrowsAsync<DbUpdateException>(() => Votes.AddAsync(new Vote(idea.Id, Owner, Now)));
        _context.ChangeTracker.Clear();
        await Votes.DeleteAsync((await Votes.GetAsync(idea.Id, Owner))!);
        Assert.False(await Votes.ExistsAsync(idea.Id, Owner));
        Assert.True(await Votes.ExistsAsync(idea.Id, Other));
    }

    [Fact]
    public async Task Statistics_RankVotesAndActivityAndExcludeSoftDeletedParentsAndIdeas()
    {
        var second = await Topics.AddAsync(Topic("Second"));
        var winner = await Topics.AddAsync(Topic("Winner"));
        var deleted = await Topics.AddAsync(Topic("Deleted"));
        var low = await Ideas.AddAsync(Idea(second.Id, "Low"));
        var high = await Ideas.AddAsync(Idea(winner.Id, "High"));
        var zero = await Ideas.AddAsync(Idea(winner.Id, "Zero"));
        var deletedIdea = await Ideas.AddAsync(Idea(winner.Id, "Deleted idea"));
        var hidden = await Ideas.AddAsync(Idea(deleted.Id, "Deleted parent"));
        await Votes.AddAsync(new Vote(low.Id, Owner, Now));
        await Votes.AddAsync(new Vote(high.Id, Owner, Now));
        await Votes.AddAsync(new Vote(high.Id, Other, Now));
        await Votes.AddAsync(new Vote(deletedIdea.Id, Owner, Now));
        await Votes.AddAsync(new Vote(hidden.Id, Owner, Now));
        deletedIdea.Delete(Now.AddMinutes(1));
        await Ideas.UpdateAsync(deletedIdea);
        deleted.Delete(Now.AddMinutes(1));
        await Topics.UpdateAsync(deleted);

        var statistics = new StatisticsRepository(_context);
        var mostVoted = await statistics.GetMostVotedIdeasAsync(0, 10);
        Assert.Equal(3, mostVoted.TotalItems);
        Assert.Equal(new[] { high.Id, low.Id, zero.Id }, mostVoted.Items.Select(i => i.IdeaId));
        Assert.Equal(new[] { 2, 1, 0 }, mostVoted.Items.Select(i => i.VoteCount));
        Assert.Equal(winner.Id, mostVoted.Items[0].TopicId);
        Assert.Equal(Owner, mostVoted.Items[0].UserId);
        Assert.Equal("High", mostVoted.Items[0].Title);
        Assert.Equal("MySQL searchable description", mostVoted.Items[0].Description);
        Assert.Equal(low.Id, Assert.Single((await statistics.GetMostVotedIdeasAsync(1, 1)).Items).IdeaId);
        Assert.Empty((await statistics.GetMostVotedIdeasAsync(3, 1)).Items);

        var active = await statistics.GetMostActiveTopicsAsync(0, 10);
        Assert.Equal(2, active.TotalItems);
        Assert.Equal(new[] { winner.Id, second.Id }, active.Items.Select(t => t.TopicId));
        Assert.Equal(new[] { 2, 1 }, active.Items.Select(t => t.IdeaCount));
        Assert.Equal(new[] { 2, 1 }, active.Items.Select(t => t.VoteCount));
        Assert.Equal("Winner", active.Items[0].Title);
        Assert.Equal(Owner, active.Items[0].UserId);
        Assert.Equal(second.Id, Assert.Single((await statistics.GetMostActiveTopicsAsync(1, 1)).Items).TopicId);
        Assert.Empty((await statistics.GetMostActiveTopicsAsync(2, 1)).Items);
    }
}
