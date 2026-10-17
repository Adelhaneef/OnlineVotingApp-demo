using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnlineVotingApp.Models;

namespace OnlineVotingApp.Data
{

    public class ApplicationDbContext : IdentityDbContext
    {

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserVote>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }


        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
            public DbSet<Poll>Polls {  get; set; }
            public DbSet<AnswerOption> Answers { get; set; }
            public DbSet<UserVote>UserVotes { get; set; }

        }
    }



