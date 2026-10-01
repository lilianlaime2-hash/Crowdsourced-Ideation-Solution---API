using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.DataAccess.Configurations
{
    public sealed class TopicTagConfiguration
    : IEntityTypeConfiguration<TopicTag>
    {
        public void Configure(EntityTypeBuilder<TopicTag> builder)
        {
            builder.ToTable("topic_tags");

            // Composite primary key
            builder.HasKey(tt => new
            {
                tt.TopicId,
                tt.TagId
            });

            builder.Property(tt => tt.TopicId)
                .HasColumnName("topic_id");

            builder.Property(tt => tt.TagId)
                .HasColumnName("tag_id");

            // Topic relationship
            builder.HasOne(tt => tt.Topic)
                .WithMany(t => t.TopicTags)
                .HasForeignKey(tt => tt.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            // Tag relationship
            builder.HasOne(tt => tt.Tag)
                .WithMany()
                .HasForeignKey(tt => tt.TagId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
