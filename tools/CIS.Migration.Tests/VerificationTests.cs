using CIS.Migration;
using MongoDB.Bson;
namespace CIS.Migration.Tests;

public class VerificationTests
{
    // Omitting a field, coercing BSON types or comparing counts alone would break these tests.
    public static TheoryData<string, BsonDocument> InvalidDocuments => new()
    {
        { "users", new BsonDocument { { "_id", "0149d2c8-2bb3-4fb3-a9df-08d252fa6e8c" }, { "login", "AdrianCis" } } },
        { "tags", new BsonDocument { { "_id", 1L }, { "name", "C#" }, { "normalizedName", "c#" } } },
        { "topics", Topic("unknown") },
        { "topics", Topic().Set("updatedAt", BsonNull.Value) },
        { "topics", Topic().Set("createdAt", "2025-01-01") },
        { "topics", Topic().Set("_id", 1) },
        { "topics", Topic().Set("tagIds", new BsonArray { 1 }) },
        { "topics", Topic().Set("extraUnsupported", true) },
    };
    [Theory, MemberData(nameof(InvalidDocuments))]
    public void InvalidMongoFieldsFailWithoutSilentNormalization(string entity, BsonDocument document)
        => Assert.Throws<InvalidDataException>(() => Canonical.Encode(entity, document));

    [Fact]
    public void CanonicalizationPreservesLargeIdsNullDifferencesAndExactText()
    {
        var a = Topic().Set("_id", 9007199254740993L);
        var b = Topic().Set("_id", 9007199254740992L);
        Assert.NotEqual(Canonical.Encode("topics", a), Canonical.Encode("topics", b));
        b = a.DeepClone().AsBsonDocument.Set("title", " title ");
        Assert.NotEqual(Canonical.Encode("topics", a), Canonical.Encode("topics", b));
    }
    [Fact]
    public void PasswordsAreExcludedFromBusinessCanonicalProjection()
    {
        var a = new BsonDocument { { "_id", "0149d2c8-2bb3-4fb3-a9df-08d252fa6e8c" }, { "name", "Adrian" }, { "login", "AdrianCis" }, { "password", "secret-a" } };
        var b = a.DeepClone().AsBsonDocument.Set("password", "secret-b");
        Assert.Equal(Canonical.Encode("users", a), Canonical.Encode("users", b));
        Assert.DoesNotContain("secret", Canonical.Encode("users", a));
    }
    public static BsonDocument Topic(string status = "Active") => new()
    {
        { "_id", 1L }, { "title", "Title" }, { "description", "Description" }, { "status", status },
        { "userId", "0149d2c8-2bb3-4fb3-a9df-08d252fa6e8c" },
        { "createdAt", new BsonDateTime(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)) },
        { "updatedAt", new BsonDateTime(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)) }, { "tagIds", new BsonArray() }
    };
}
