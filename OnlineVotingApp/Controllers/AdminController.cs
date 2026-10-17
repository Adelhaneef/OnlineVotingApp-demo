using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OnlineVotingApp.Models; // Update with your namespace
using System.Threading.Tasks;
using System.Linq;

namespace OnlineVotingApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public IActionResult ListUsers()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(); // User not found in the database
            }

            // ✅ Prevent the currently logged-in user from deleting themselves
            if (user.Email == User.Identity.Name)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction("ListUsers");
            }

            await _userManager.DeleteAsync(user); // Delete the user
            return RedirectToAction("ListUsers"); // Go back to the user list
        }

    }
}
