using System.Reflection;
using CIS.DataAccess.MongoDB.Documents;
using CIS.Domain.Entities;

namespace CIS.DataAccess.MongoDB;

internal static class Rehydrator
{
    public static T Create<T>() where T : class
    {
        return (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
    }

    public static void Set(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' not found in {target.GetType().Name}.");

        property.SetValue(target, value);
    }

    public static Tag ToTag(TagDocument doc)
    {
        var tag = Create<Tag>();
        Set(tag, nameof(Tag.Id), doc.Id);
        Set(tag, nameof(Tag.Name), doc.Name);
        Set(tag, nameof(Tag.NormalizedName), doc.NormalizedName);
        return tag;
    }

    public static Topic ToTopic(TopicDocument doc, IReadOnlyDictionary<long, Tag> tags)
    {
        var topic = Create<Topic>();
        Set(topic, nameof(Topic.Id), doc.Id);
        Set(topic, nameof(Topic.Title), doc.Title);
        Set(topic, nameof(Topic.Description), doc.Description);
        Set(topic, nameof(Topic.Status), doc.Status);
        Set(topic, nameof(Topic.UserId), doc.UserId);
        Set(topic, nameof(Topic.CreatedAt), doc.CreatedAt);
        Set(topic, nameof(Topic.UpdatedAt), doc.UpdatedAt);

        foreach (var tagId in doc.TagIds)
        {
            if (tags.TryGetValue(tagId, out var tag))
            {
                topic.AddTag(tag);
            }
        }

        foreach (var topicTag in topic.TopicTags)
        {
            Set(topicTag, nameof(TopicTag.TopicId), topic.Id);
            Set(topicTag, nameof(TopicTag.TagId), topicTag.Tag.Id);
        }

        return topic;
    }

    public static Idea ToIdea(IdeaDocument doc)
    {
        var idea = Create<Idea>();
        Set(idea, nameof(Idea.Id), doc.Id);
        Set(idea, nameof(Idea.TopicId), doc.TopicId);
        Set(idea, nameof(Idea.UserId), doc.UserId);
        Set(idea, nameof(Idea.Title), doc.Title);
        Set(idea, nameof(Idea.Description), doc.Description);
        Set(idea, nameof(Idea.Status), doc.Status);
        Set(idea, nameof(Idea.CreatedAt), doc.CreatedAt);
        Set(idea, nameof(Idea.UpdatedAt), doc.UpdatedAt);
        return idea;
    }

    public static Vote ToVote(VoteDocument doc)
    {
        var vote = Create<Vote>();
        Set(vote, nameof(Vote.Id), doc.Id);
        Set(vote, nameof(Vote.IdeaId), doc.IdeaId);
        Set(vote, nameof(Vote.UserId), doc.UserId);
        Set(vote, nameof(Vote.CreatedAt), doc.CreatedAt);
        return vote;
    }


}