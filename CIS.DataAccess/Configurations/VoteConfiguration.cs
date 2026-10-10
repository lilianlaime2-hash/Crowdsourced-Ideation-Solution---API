using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS.DataAccess.Configurations;

public sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("votes");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(v => v.IdeaId)
            .HasColumnName("idea_id")
            .IsRequired();

        builder.Property(v => v.UserId)
            .HasColumnName("user_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(v => new { v.IdeaId, v.UserId })
            .IsUnique()
            .HasDatabaseName("ux_votes_idea_id_user_id");

        builder.HasOne<Idea>()
            .WithMany()
            .HasForeignKey(v => v.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}