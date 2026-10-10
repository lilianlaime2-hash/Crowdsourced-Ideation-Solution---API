using CIS.Domain.Entities;
using Xunit;

namespace CIS.Tests.Topics;

public class TopicUpdateDomainTests
{
    private static readonly string OwnerId = Guid.NewGuid().ToString("D");
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    private static Topic NewTopic() =>
        new("Old title", "Old description", OwnerId, CreatedAt);

    [Fact]
    public void Update_ValidData_ChangesTitleDescriptionAndUpdatedAt()
    {
        var topic = NewTopic();

        topic.Update("  New title  ", "  New description  ", Now);

        Assert.Equal("New title", topic.Title);
        Assert.Equal("New description", topic.Description);
        Assert.Equal(Now, topic.UpdatedAt);
        Assert.Equal(CreatedAt, topic.CreatedAt);
        Assert.Equal(OwnerId, topic.UserId);
    }

    [Theory]
    [InlineData("", "New description")]
    [InlineData("   ", "New description")]
    [InlineData("New title", "")]
    [InlineData("New title", "   ")]
    public void Update_EmptyData_ThrowsAndKeepsPreviousData(string title, string description)
    {
        var topic = NewTopic();

        Assert.Throws<ArgumentException>(() => topic.Update(title, description, Now));

        Assert.Equal("Old title", topic.Title);
        Assert.Equal("Old description", topic.Description);
        Assert.Equal(CreatedAt, topic.UpdatedAt);
    }

    [Fact]
    public void Update_TitleTooLong_ThrowsAndKeepsPreviousData()
    {
        var topic = NewTopic();

        Assert.Throws<ArgumentException>(() =>
            topic.Update(new string('a', 151), "New description", Now));

        Assert.Equal("Old title", topic.Title);
    }

    [Fact]
    public void Update_DescriptionTooLong_ThrowsAndKeepsPreviousData()
    {
        var topic = NewTopic();

        Assert.Throws<ArgumentException>(() =>
            topic.Update("New title", new string('a', 1001), Now));

        Assert.Equal("Old description", topic.Description);
    }

    [Fact]
    public void Update_NonUtcDate_Throws()
    {
        var topic = NewTopic();
        var localDate = new DateTime(2026, 10, 9, 15, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => topic.Update("New title", "New description", localDate));

        Assert.Equal("Old title", topic.Title);
    }
}