using CIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.DataAccess.Context
{
    public sealed class CisDbContext : DbContext
    {
        public CisDbContext(DbContextOptions<CisDbContext> options) : base(options)
        {
        }

        public DbSet<Topic> Topics => Set<Topic>();

        public DbSet<Tag> Tags => Set<Tag>();

        public DbSet<TopicTag> TopicTags => Set<TopicTag>();

        public DbSet<Idea> Ideas => Set<Idea>();

        public DbSet<Vote> Votes => Set<Vote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CisDbContext).Assembly);
        }
    }
}
