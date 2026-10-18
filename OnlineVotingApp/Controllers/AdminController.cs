using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineVotingApp.Data;

namespace OnlineVotingApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult ListUsers()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "User ID was not provided.";
                return RedirectToAction(nameof(ListUsers));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(ListUsers));
            }

            // Prevent the currently logged-in admin from deleting themselves using user ID
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(ListUsers));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Clean up user's votes and decrement option vote counts to maintain referential integrity
                var userVotes = await _context.UserVotes.Where(v => v.UserId == id).ToListAsync();
                foreach (var vote in userVotes)
                {
                    var option = await _context.Answers.FirstOrDefaultAsync(a => a.Id == vote.AnswerOptionId);
                    if (option != null && option.VoteCount > 0)
                    {
                        option.VoteCount--;
                    }
                }
                _context.UserVotes.RemoveRange(userVotes);
                await _context.SaveChangesAsync();

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = "Failed to delete user account: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(ListUsers));
                }

                await transaction.CommitAsync();
                TempData["Message"] = "User and associated votes deleted successfully.";
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "An error occurred while deleting the user.";
            }

            return RedirectToAction(nameof(ListUsers));
        }
    }
}
