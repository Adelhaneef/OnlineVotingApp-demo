using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineVotingApp.Data;
using OnlineVotingApp.Models;
using System.Security.Claims;

[Authorize]
public class UserPollsController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserPollsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // Polls user has voted on
        var userVotes = await _context.UserVotes
            .Where(v => v.UserId == userId)
            .ToListAsync();

        var now = DateTime.Now;
        var polls = await _context.Polls
            .Where(p => p.fromDate <= now && p.toDate >= now)
            .OrderBy(p => p.displayOrder)
            .Include(p => p.AnswerOptions)
            .ToListAsync();

        var model = polls.Select(poll => new PollWithUserVote
        {
            Poll = poll,
            SelectedOptionId = userVotes.FirstOrDefault(v => v.PollId == poll.Id)?.AnswerOptionId
        }).ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(int optionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (optionId <= 0)
        {
            TempData["Error"] = "Please select an answer option before voting.";
            return RedirectToAction(nameof(Index));
        }

        var selectedOption = await _context.Answers.FirstOrDefaultAsync(a => a.Id == optionId);
        if (selectedOption == null)
        {
            TempData["Error"] = "The selected option does not exist.";
            return RedirectToAction(nameof(Index));
        }

        var poll = await _context.Polls.FirstOrDefaultAsync(p => p.Id == selectedOption.PollId);
        if (poll == null)
        {
            TempData["Error"] = "The poll associated with this option could not be found.";
            return RedirectToAction(nameof(Index));
        }

        // Validate that the poll is currently active
        var now = DateTime.Now;
        if (poll.fromDate > now || poll.toDate < now)
        {
            TempData["Error"] = "This poll is not currently open for voting.";
            return RedirectToAction(nameof(Index));
        }

        // Concurrency-safe vote recording using a transaction and SQL Server application lock
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var lockResource = $"Vote_{userId}_{poll.Id}";
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                lockResource);

            var alreadyVoted = await _context.UserVotes
                .AnyAsync(v => v.UserId == userId && v.PollId == poll.Id);

            if (alreadyVoted)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "You have already voted on this poll.";
                return RedirectToAction(nameof(Index));
            }

            // Increment option vote count and add record
            selectedOption.VoteCount++;
            _context.UserVotes.Add(new UserVote
            {
                UserId = userId,
                PollId = poll.Id,
                AnswerOptionId = optionId
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Message"] = "Your vote has been recorded successfully!";
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "An error occurred while casting your vote. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }
}
