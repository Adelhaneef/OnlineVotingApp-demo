// Controllers/AdminPollsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineVotingApp.Data;
using OnlineVotingApp.Models;

[Authorize(Roles = "Admin")]
public class AdminPollsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminPollsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var polls = await _context.Polls.Include(p => p.AnswerOptions).ToListAsync();
        return View(polls);
    }

    public IActionResult Create()
    {
        var now = DateTime.Now;
        var poll = new Poll
        {
            fromDate = now,
            toDate = now.AddDays(7),
            displayOrder = 1,
            AnswerOptions = new List<AnswerOption>
            {
                new AnswerOption(),
                new AnswerOption()
            }
        };
        return View(poll);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Poll poll)
    {
        // Filter out blank options
        poll.AnswerOptions = poll.AnswerOptions?
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .ToList() ?? new List<AnswerOption>();

        // Remove navigation property validation from AnswerOptions
        for (int i = 0; i < poll.AnswerOptions.Count; i++)
        {
            ModelState.Remove($"AnswerOptions[{i}].Poll");
        }

        // Validate date logic
        if (poll.toDate <= poll.fromDate)
        {
            ModelState.AddModelError(nameof(poll.toDate), "The 'To Date' must be later than the 'From Date'.");
        }

        // Enforce minimum of 2 options
        if (poll.AnswerOptions.Count < 2)
        {
            ModelState.AddModelError("AnswerOptions", "A poll must have at least 2 answer options.");
            ModelState.AddModelError(string.Empty, "A poll must have at least 2 answer options.");
        }

        if (!ModelState.IsValid)
        {
            while (poll.AnswerOptions.Count < 2)
                poll.AnswerOptions.Add(new AnswerOption());

            return View(poll);
        }

        foreach (var option in poll.AnswerOptions)
            option.VoteCount = 0;

        _context.Add(poll);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Poll created successfully.";
        return RedirectToAction(nameof(Index));
    }



    public async Task<IActionResult> Edit(int id)
    {
        var poll = await _context.Polls
            .Include(p => p.AnswerOptions)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poll == null)
        {
            return NotFound();
        }

        return View(poll);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Poll poll)
    {
        // Filter out blank options
        poll.AnswerOptions = poll.AnswerOptions?
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .ToList() ?? new List<AnswerOption>();

        // Remove navigation property validation from AnswerOptions
        for (int i = 0; i < poll.AnswerOptions.Count; i++)
        {
            ModelState.Remove($"AnswerOptions[{i}].Poll");
        }

        // Validate date logic
        if (poll.toDate <= poll.fromDate)
        {
            ModelState.AddModelError(nameof(poll.toDate), "The 'To Date' must be later than the 'From Date'.");
        }

        // Enforce minimum of 2 options
        if (poll.AnswerOptions.Count < 2)
        {
            ModelState.AddModelError("AnswerOptions", "A poll must have at least 2 answer options.");
            ModelState.AddModelError(string.Empty, "A poll must have at least 2 answer options.");
        }

        if (!ModelState.IsValid)
        {
            return View(poll);
        }

        var existingPoll = await _context.Polls
            .Include(p => p.AnswerOptions)
            .FirstOrDefaultAsync(p => p.Id == poll.Id);

        if (existingPoll == null)
        {
            return NotFound();
        }

        // Identify options being removed (present in DB but omitted from submitted form)
        var submittedOptionIds = poll.AnswerOptions
            .Where(o => o.Id > 0)
            .Select(o => o.Id)
            .ToHashSet();

        var removedOptions = existingPoll.AnswerOptions
            .Where(o => !submittedOptionIds.Contains(o.Id))
            .ToList();

        // Enforce safety policy: Do not delete options that have existing votes
        foreach (var removedOption in removedOptions)
        {
            var hasVotes = removedOption.VoteCount > 0 ||
                await _context.UserVotes.AnyAsync(v => v.AnswerOptionId == removedOption.Id);

            if (hasVotes)
            {
                var errorMsg = $"Cannot remove option '{removedOption.OptionText}' because it already has {removedOption.VoteCount} recorded vote(s). Answer options with existing votes cannot be deleted to preserve election history.";
                ModelState.AddModelError(string.Empty, errorMsg);
                ModelState.AddModelError("AnswerOptions", errorMsg);

                poll.AnswerOptions = existingPoll.AnswerOptions.ToList();
                return View(poll);
            }
        }

        // Update poll fields
        existingPoll.Question = poll.Question;
        existingPoll.fromDate = poll.fromDate;
        existingPoll.toDate = poll.toDate;
        existingPoll.displayOrder = poll.displayOrder;

        // Safely remove options with 0 votes
        foreach (var removedOption in removedOptions)
        {
            _context.Answers.Remove(removedOption);
        }

        // Sync answer options: update existing or add new ones
        foreach (var updatedOption in poll.AnswerOptions)
        {
            if (updatedOption.Id > 0)
            {
                var existingOption = existingPoll.AnswerOptions
                    .FirstOrDefault(o => o.Id == updatedOption.Id);

                if (existingOption != null)
                {
                    existingOption.OptionText = updatedOption.OptionText;
                }
            }
            else
            {
                // New option added during edit
                existingPoll.AnswerOptions.Add(new AnswerOption
                {
                    OptionText = updatedOption.OptionText,
                    VoteCount = 0,
                    PollId = existingPoll.Id
                });
            }
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = "Poll updated successfully.";
        return RedirectToAction(nameof(Index));
    }


    public async Task<IActionResult> Delete(int id)
    {
        var poll = await _context.Polls
            .Include(p => p.AnswerOptions)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poll == null)
        {
            TempData["Error"] = "Poll not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(poll);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var poll = await _context.Polls
            .Include(p => p.AnswerOptions)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poll == null)
        {
            TempData["Error"] = "Poll not found or already deleted.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Remove associated UserVotes first to prevent orphaned records
            var relatedVotes = await _context.UserVotes.Where(v => v.PollId == id).ToListAsync();
            if (relatedVotes.Any())
            {
                _context.UserVotes.RemoveRange(relatedVotes);
            }

            if (poll.AnswerOptions != null && poll.AnswerOptions.Any())
            {
                _context.Answers.RemoveRange(poll.AnswerOptions);
            }

            _context.Polls.Remove(poll);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Message"] = "Poll and associated votes deleted successfully.";
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "An error occurred while deleting the poll.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Results(int id)
    {
        var poll = await _context.Polls
            .Include(p => p.AnswerOptions)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poll == null)
            return NotFound();

        // Get active user IDs
        var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();

        // Get valid votes for this poll
        var filteredVotes = await _context.UserVotes
            .Where(v => v.PollId == id && validUserIds.Contains(v.UserId))
            .ToListAsync();

        // Count votes per option
        var voteCounts = filteredVotes
            .GroupBy(v => v.AnswerOptionId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Update the vote counts for display (do not save to DB)
        foreach (var option in poll.AnswerOptions)
        {
            option.VoteCount = voteCounts.ContainsKey(option.Id) ? voteCounts[option.Id] : 0;
        }

        return View(poll);
    }

}
