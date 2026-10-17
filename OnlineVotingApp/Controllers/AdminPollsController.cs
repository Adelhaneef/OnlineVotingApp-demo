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
        var poll = new Poll
        {
            AnswerOptions = new List<AnswerOption>
            {
            new AnswerOption(),
            new AnswerOption(),
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
        // Manually remove the Poll field from validation
        foreach (var opt in poll.AnswerOptions)
        {
            ModelState.Remove($"AnswerOptions[{poll.AnswerOptions.IndexOf(opt)}].Poll");
        }

        poll.AnswerOptions = poll.AnswerOptions?
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .ToList();

        if (!ModelState.IsValid)
        {
            while (poll.AnswerOptions.Count < 4)
                poll.AnswerOptions.Add(new AnswerOption());

            return View(poll);
        }

        foreach (var option in poll.AnswerOptions)
            option.VoteCount = 0;

        _context.Add(poll);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }



    public async Task<IActionResult> Edit(int id)
    {
        var poll = await _context.Polls.
            Include(p => p.AnswerOptions).
            FirstOrDefaultAsync(p => p.Id == id);
        return View(poll);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Poll poll)
    {
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

        // Update poll fields
        existingPoll.Question = poll.Question;
        existingPoll.fromDate = poll.fromDate;
        existingPoll.toDate = poll.toDate;
        existingPoll.displayOrder = poll.displayOrder;

        // Sync answer options
        foreach (var updatedOption in poll.AnswerOptions)
        {
            var existingOption = existingPoll.AnswerOptions
                .FirstOrDefault(o => o.Id == updatedOption.Id);

            if (existingOption != null)
            {
                existingOption.OptionText = updatedOption.OptionText;
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    public async Task<IActionResult> Delete(int id)
    {
        var poll = await _context.Polls.Include(p => p.AnswerOptions).FirstOrDefaultAsync(p => p.Id == id);
        return View(poll);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var poll = await _context.Polls.Include(p => p.AnswerOptions).FirstOrDefaultAsync(p => p.Id == id);
        _context.Answers.RemoveRange(poll.AnswerOptions);
        _context.Polls.Remove(poll);
        await _context.SaveChangesAsync();
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
