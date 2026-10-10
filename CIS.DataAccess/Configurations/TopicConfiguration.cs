using CIS.Domain.Constants;
using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.DataAccess.Configurations
{
    public sealed class TopicConfiguration
    : IEntityTypeConfiguration<Topic>
    {
        public void Configure(EntityTypeBuilder<Topic> builder)
        {
            builder.ToTable("topics");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(t => t.Title)
                .HasColumnName("title")
                .HasMaxLength(TopicRules.MaxTitleLength)
                .IsRequired();

            builder.Property(t => t.Description)
                .HasColumnName("description")
                .HasMaxLength(TopicRules.MaxDescriptionLength)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(t => t.UserId)
                .HasColumnName("user_id")
                .HasColumnType("varchar(36)")
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            builder.Property(t => t.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            builder.HasIndex(t => t.UserId)
                .HasDatabaseName("ix_topics_user_id");

            builder.HasMany(t => t.TopicTags)
                .WithOne(tt => tt.Topic)
                .HasForeignKey(tt => tt.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(t => t.TopicTags)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
