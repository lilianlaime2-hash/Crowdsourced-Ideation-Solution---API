using CIS.Domain.Constants;
using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.DataAccess.Configurations
{
    public sealed class IdeaConfiguration
        : IEntityTypeConfiguration<Idea>
    {
        public void Configure(EntityTypeBuilder<Idea> builder)
        {
            builder.ToTable("ideas");

            builder.HasKey(i => i.Id);

            builder.Property(i => i.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(i => i.TopicId)
                .HasColumnName("topic_id")
                .IsRequired();

            builder.Property(i => i.UserId)
                .HasColumnName("user_id")
                .HasColumnType("varchar(36)")
                .IsRequired();

            builder.Property(i => i.Title)
                .HasColumnName("title")
                .HasMaxLength(IdeaRules.MaxTitleLength)
                .IsRequired();

            builder.Property(i => i.Description)
                .HasColumnName("description")
                .HasMaxLength(IdeaRules.MaxDescriptionLength)
                .IsRequired();

            builder.Property(i => i.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            
            builder.Property(i => i.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            builder.Property(i => i.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime(6)")
                .IsRequired();

            builder.HasIndex(i => i.TopicId)
                .HasDatabaseName("ix_ideas_topic_id");

            builder.HasIndex(i => i.UserId)
                .HasDatabaseName("ix_ideas_user_id");

            builder.HasOne<Topic>()
                .WithMany()
                .HasForeignKey(i => i.TopicId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
