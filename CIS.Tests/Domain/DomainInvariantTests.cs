using CIS.Domain.Constants;
using CIS.Domain.Entities;
using CIS.Domain.Enums;
using CIS.Domain.Policies;
using Xunit;

namespace CIS.Tests.Domain;

public sealed class DomainInvariantTests
{
    private const string User = "AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA";
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("  Ｃ＃  ", "C#", "C#")]
    [InlineData("  Data\t science  ", "Data science", "DATA SCIENCE")]
    [InlineData("C++", "C++", "C++")]
    [InlineData("Español", "Español", "ESPAÑOL")]
    public void TagPolicy_NormalizesUnicodeWhitespaceAndCase(string input, string name, string normalized)
    {
        var tag = new Tag(input);
        Assert.Equal(name, tag.Name);
        Assert.Equal(normalized, tag.NormalizedName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("invalid/tag")]
    [InlineData("123 +#")]
    [InlineData("A very long tag name exceeding thirty characters")]
    public void TagPolicy_RejectsUnusableNames(string? input) =>
        Assert.Throws<ArgumentException>(() => TagNamePolicy.ValidateAndNormalize(input));

    [Fact]
    public void Topic_AddTagEnforcesNormalizedUniquenessAndMaximum()
    {
        var topic = new Topic("Title", "Description", User, Now);
        var tag = new Tag("C#");
        topic.AddTag(tag);
        Assert.Throws<InvalidOperationException>(() => topic.AddTag(tag));
        Assert.Throws<InvalidOperationException>(() => topic.AddTag(new Tag(" c# ")));
        for (var i = 1; i < TagRules.MaxTagsPerTopic; i++) topic.AddTag(new Tag("Tag " + i));
        Assert.Throws<InvalidOperationException>(() => topic.AddTag(new Tag("Overflow")));
        Assert.Throws<ArgumentNullException>(() => topic.AddTag(null!));
        Assert.Equal(TagRules.MaxTagsPerTopic, topic.TopicTags.Count);
        Assert.All(topic.TopicTags, relation => Assert.Same(topic, relation.Topic));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("longTitle")]
    [InlineData("description")]
    [InlineData("longDescription")]
    [InlineData("user")]
    [InlineData("date")]
    public void Constructors_RejectInvalidContentIdentityAndTime(string invalid)
    {
        var title = invalid == "title" ? " " : invalid == "longTitle" ? new string('x', TopicRules.MaxTitleLength + 1) : "Title";
        var description = invalid == "description" ? " " : invalid == "longDescription" ? new string('x', TopicRules.MaxDescriptionLength + 1) : "Description";
        var user = invalid == "user" ? "invalid" : User;
        var date = invalid == "date" ? DateTime.SpecifyKind(Now, DateTimeKind.Unspecified) : Now;
        Assert.Throws<ArgumentException>(() => new Topic(title, description, user, date));
        title = invalid == "longTitle" ? new string('x', IdeaRules.MaxTitleLength + 1) : title;
        description = invalid == "longDescription" ? new string('x', IdeaRules.MaxDescriptionLength + 1) : description;
        Assert.Throws<ArgumentException>(() => new Idea(1, title, description, user, date));
    }

    [Fact]
    public void SoftDelete_ValidatesUtcAndCannotBeRepeated()
    {
        var topic = new Topic("Title", "Description", User, Now);
        var idea = new Idea(1, "Title", "Description", User, Now);
        var invalid = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);
        Assert.Throws<ArgumentException>(() => topic.Delete(invalid));
        Assert.Throws<ArgumentException>(() => idea.Delete(invalid));
        Assert.Equal(TopicStatus.Active, topic.Status);
        Assert.Equal(IdeaStatus.Active, idea.Status);
        topic.Delete(Now.AddMinutes(1));
        idea.Delete(Now.AddMinutes(1));
        Assert.Equal(TopicStatus.Deleted, topic.Status);
        Assert.Equal(IdeaStatus.Deleted, idea.Status);
        Assert.Equal(Now.AddMinutes(1), topic.UpdatedAt);
        Assert.Equal(Now.AddMinutes(1), idea.UpdatedAt);
        Assert.Throws<InvalidOperationException>(() => topic.Delete(Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => idea.Delete(Now.AddMinutes(2)));
    }

    [Fact]
    public void Vote_RejectsInvalidIdentityParentAndTimestampAndCanonicalizesUser()
    {
        Assert.Throws<ArgumentException>(() => new Vote(0, User, Now));
        Assert.Throws<ArgumentException>(() => new Vote(1, "invalid", Now));
        Assert.Throws<ArgumentException>(() => new Vote(1, User, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => new Idea(0, "Title", "Description", User, Now));
        var vote = new Vote(2, User, Now);
        Assert.Equal(2, vote.IdeaId);
        Assert.Equal(User.ToLowerInvariant(), vote.UserId);
        Assert.Equal(Now, vote.CreatedAt);
        Assert.Throws<ArgumentNullException>(() => new TopicTag(null!, new Tag("C#")));
        Assert.Throws<ArgumentNullException>(() => new TopicTag(new Topic("Title", "Description", User, Now), null!));
    }
}
