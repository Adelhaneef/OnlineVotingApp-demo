using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineVotingApp.Data;
using OnlineVotingApp.Models;
using System.Security.Claims;

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

        // Polls user has voted on
        var userVotes = await _context.UserVotes
            .Where(v => v.UserId == userId)
            .ToListAsync();

        var polls = await _context.Polls
            .Where(p => p.fromDate <= DateTime.Now && p.toDate >= DateTime.Now)
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

        var selectedOption = await _context.Answers
            .FirstOrDefaultAsync(a => a.Id == optionId);
        if (selectedOption != null)
        {
            var poll = await _context.Polls
                .FirstOrDefaultAsync(p => p.Id == selectedOption.PollId);

            // use poll if needed
        }

        if (selectedOption == null || string.IsNullOrEmpty(userId))
            return NotFound();

        // Check if user already voted on this poll
        var alreadyVoted = await _context.UserVotes
            .AnyAsync(v => v.UserId == userId && v.PollId == selectedOption.PollId);

        if (alreadyVoted)
        {
            TempData["Message"] = "You have already voted on this poll.";
            return RedirectToAction(nameof(Index));
        }

        // Count the vote
        selectedOption.VoteCount++;
        _context.UserVotes.Add(new UserVote
        {
            UserId = userId,
            PollId = selectedOption.PollId,
            AnswerOptionId = optionId
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = "Your vote has been recorded!";
        return RedirectToAction(nameof(Index));

        return RedirectToAction(nameof(Index));
    }
}
