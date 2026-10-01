using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.DataAccess.Configurations
{
    public sealed class TagConfiguration
    : IEntityTypeConfiguration<Tag>
    {
        public void Configure(EntityTypeBuilder<Tag> builder)
        {
            builder.ToTable("tags");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(t => t.Name)
                .HasColumnName("name")
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(t => t.NormalizedName)
                .HasColumnName("normalized_name")
                .HasMaxLength(30)
                .IsRequired();

            builder.HasIndex(t => t.NormalizedName)
                .IsUnique()
                .HasDatabaseName("ux_tags_normalized_name");
        }
    }
}
