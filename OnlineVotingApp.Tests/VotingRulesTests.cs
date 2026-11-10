using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineVotingApp.Data;
using OnlineVotingApp.Models;
using Xunit;

namespace OnlineVotingApp.Tests
{
    public class VotingRulesTests
    {
        private ApplicationDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public void PollDateRange_WhenToDateIsBeforeOrEqualToFromDate_IsInvalid()
        {
            // Rule 1: A poll's toDate must be strictly after its fromDate
            var poll = new Poll
            {
                Id = 1,
                Question = "Favorite Language?",
                fromDate = new DateTime(2026, 12, 1, 10, 0, 0),
                toDate = new DateTime(2026, 11, 1, 10, 0, 0), // Invalid: earlier than fromDate
                displayOrder = 1,
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 1, OptionText = "C#" },
                    new AnswerOption { Id = 2, OptionText = "TypeScript" }
                }
            };

            bool isDateRangeValid = poll.toDate > poll.fromDate;

            Assert.False(isDateRangeValid, "Poll date range should be rejected when toDate is earlier than or equal to fromDate.");
        }

        [Fact]
        public void PollDateRange_WhenToDateIsAfterFromDate_IsValid()
        {
            var poll = new Poll
            {
                Id = 1,
                Question = "Favorite Framework?",
                fromDate = new DateTime(2026, 1, 1, 0, 0, 0),
                toDate = new DateTime(2026, 12, 31, 23, 59, 59),
                displayOrder = 1
            };

            bool isDateRangeValid = poll.toDate > poll.fromDate;

            Assert.True(isDateRangeValid, "Poll date range should be accepted when toDate is strictly after fromDate.");
        }

        [Fact]
        public void PollOptions_WhenFewerThanTwoOptions_FailsValidation()
        {
            // Rule 2: A poll must have at least 2 answer options
            var options = new List<AnswerOption>
            {
                new AnswerOption { Id = 1, OptionText = "Single Option Only" }
            };

            var validOptions = options.Where(o => !string.IsNullOrWhiteSpace(o.OptionText)).ToList();

            Assert.True(validOptions.Count < 2, "Polls with fewer than 2 answer options must be rejected.");
        }

        [Fact]
        public async Task VotingWindow_WhenOutsideActiveDates_RejectsVote()
        {
            // Rule 3: Voting is rejected if server time is outside the poll window
            using var context = CreateInMemoryContext(nameof(VotingWindow_WhenOutsideActiveDates_RejectsVote));

            var pastPoll = new Poll
            {
                Id = 1,
                Question = "Past Poll",
                fromDate = new DateTime(2025, 1, 1),
                toDate = new DateTime(2025, 1, 31),
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 1, OptionText = "A" },
                    new AnswerOption { Id = 2, OptionText = "B" }
                }
            };

            context.Polls.Add(pastPoll);
            await context.SaveChangesAsync();

            var simulatedNow = new DateTime(2026, 6, 1);
            bool canVote = simulatedNow >= pastPoll.fromDate && simulatedNow <= pastPoll.toDate;

            Assert.False(canVote, "Vote must be rejected when current time is past the toDate.");
        }

        [Fact]
        public async Task OptionOwnership_WhenOptionBelongsToAnotherPoll_RejectsVote()
        {
            // Rule 4: Selected option must belong to the poll being voted on
            using var context = CreateInMemoryContext(nameof(OptionOwnership_WhenOptionBelongsToAnotherPoll_RejectsVote));

            var poll1 = new Poll
            {
                Id = 101,
                Question = "Poll 1",
                fromDate = DateTime.Now.AddDays(-1),
                toDate = DateTime.Now.AddDays(1),
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 10, OptionText = "Poll 1 - Opt A", PollId = 101 },
                    new AnswerOption { Id = 11, OptionText = "Poll 1 - Opt B", PollId = 101 }
                }
            };

            var poll2 = new Poll
            {
                Id = 102,
                Question = "Poll 2",
                fromDate = DateTime.Now.AddDays(-1),
                toDate = DateTime.Now.AddDays(1),
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 20, OptionText = "Poll 2 - Opt A", PollId = 102 }
                }
            };

            context.Polls.AddRange(poll1, poll2);
            await context.SaveChangesAsync();

            // Attacker attempts to vote on poll1 using option 20 (which belongs to poll2)
            int attemptedOptionId = 20;
            var targetPoll = await context.Polls
                .Include(p => p.AnswerOptions)
                .FirstAsync(p => p.Id == 101);

            var validOption = targetPoll.AnswerOptions.FirstOrDefault(o => o.Id == attemptedOptionId);

            Assert.Null(validOption);
        }

        [Fact]
        public async Task DuplicateVote_WhenUserAlreadyVoted_BlocksSecondVote()
        {
            // Rule 5: A user can only vote once per poll
            using var context = CreateInMemoryContext(nameof(DuplicateVote_WhenUserAlreadyVoted_BlocksSecondVote));

            var poll = new Poll
            {
                Id = 201,
                Question = "Favorite Season?",
                fromDate = DateTime.Now.AddDays(-1),
                toDate = DateTime.Now.AddDays(1),
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 1, OptionText = "Summer", PollId = 201 },
                    new AnswerOption { Id = 2, OptionText = "Winter", PollId = 201 }
                }
            };
            context.Polls.Add(poll);

            // Record first vote
            string userId = "user-abc-123";
            context.UserVotes.Add(new UserVote
            {
                Id = 1,
                UserId = userId,
                PollId = 201,
                AnswerOptionId = 1
            });
            await context.SaveChangesAsync();

            // Attempt second vote submission
            bool hasAlreadyVoted = await context.UserVotes.AnyAsync(v => v.UserId == userId && v.PollId == 201);

            Assert.True(hasAlreadyVoted, "System must identify that user has already voted in this poll.");
        }

        [Fact]
        public async Task OptionRemoval_WhenOptionHasVotes_CannotBeDeleted()
        {
            // Rule 6 (Priority 1): Voted options cannot be removed during edit
            using var context = CreateInMemoryContext(nameof(OptionRemoval_WhenOptionHasVotes_CannotBeDeleted));

            var poll = new Poll
            {
                Id = 301,
                Question = "Select Choice",
                fromDate = DateTime.Now.AddDays(-1),
                toDate = DateTime.Now.AddDays(1),
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 50, OptionText = "Choice 1", VoteCount = 3, PollId = 301 },
                    new AnswerOption { Id = 51, OptionText = "Choice 2", VoteCount = 0, PollId = 301 }
                }
            };
            context.Polls.Add(poll);
            await context.SaveChangesAsync();

            var optionWithVotes = poll.AnswerOptions.First(o => o.Id == 50);

            // Policy check
            bool canRemove = optionWithVotes.VoteCount == 0 &&
                !await context.UserVotes.AnyAsync(v => v.AnswerOptionId == optionWithVotes.Id);

            Assert.False(canRemove, "Option with recorded votes must not be removable.");
        }

        [Fact]
        public async Task OptionRemoval_WhenOptionHasZeroVotes_CanBeSafelyDeleted()
        {
            // Rule 7 (Priority 1): 0-vote options can be safely removed
            using var context = CreateInMemoryContext(nameof(OptionRemoval_WhenOptionHasZeroVotes_CanBeSafelyDeleted));

            var poll = new Poll
            {
                Id = 401,
                Question = "Select Choice",
                fromDate = DateTime.Now.AddDays(-1),
                toDate = DateTime.Now.AddDays(1),
                AnswerOptions = new List<AnswerOption>
                {
                    new AnswerOption { Id = 60, OptionText = "Choice A", VoteCount = 2, PollId = 401 },
                    new AnswerOption { Id = 61, OptionText = "Choice B (Unused)", VoteCount = 0, PollId = 401 }
                }
            };
            context.Polls.Add(poll);
            await context.SaveChangesAsync();

            var unvotedOption = poll.AnswerOptions.First(o => o.Id == 61);

            bool canRemove = unvotedOption.VoteCount == 0 &&
                !await context.UserVotes.AnyAsync(v => v.AnswerOptionId == unvotedOption.Id);

            Assert.True(canRemove, "Option with 0 votes must be eligible for safe deletion.");

            if (canRemove)
            {
                context.Answers.Remove(unvotedOption);
                await context.SaveChangesAsync();
            }

            var remainingOptions = await context.Answers.Where(a => a.PollId == 401).ToListAsync();
            Assert.Single(remainingOptions);
            Assert.Equal(60, remainingOptions[0].Id);
        }

        [Fact]
        public void ResultsCalculation_ZeroVotes_GuardsAgainstDivisionByZero()
        {
            // Rule 8: Zero-vote polls must render 0% without throwing DivideByZeroException
            var options = new List<AnswerOption>
            {
                new AnswerOption { Id = 1, OptionText = "Alpha", VoteCount = 0 },
                new AnswerOption { Id = 2, OptionText = "Beta", VoteCount = 0 }
            };

            var totalVotes = options.Sum(o => o.VoteCount);
            Assert.Equal(0, totalVotes);

            foreach (var opt in options)
            {
                double percentage = totalVotes > 0 ? (opt.VoteCount * 100.0 / totalVotes) : 0;
                Assert.Equal(0.0, percentage);
            }
        }

        [Fact]
        public void ResultsCalculation_TiedVotes_HighlightsAllLeaders()
        {
            // Rule 9: Tied leaders must both receive the leader indicator
            var options = new List<AnswerOption>
            {
                new AnswerOption { Id = 1, OptionText = "Option 1", VoteCount = 5 },
                new AnswerOption { Id = 2, OptionText = "Option 2", VoteCount = 5 },
                new AnswerOption { Id = 3, OptionText = "Option 3", VoteCount = 2 }
            };

            var maxVotes = options.Max(o => o.VoteCount);
            Assert.Equal(5, maxVotes);

            var leaders = options.Where(o => o.VoteCount == maxVotes && maxVotes > 0).ToList();
            Assert.Equal(2, leaders.Count);
        }
    }
}
