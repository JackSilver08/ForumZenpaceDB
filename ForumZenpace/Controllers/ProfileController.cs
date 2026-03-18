using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ForumZenpace.Models;

namespace ForumZenpace.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ForumDbContext _context;

        public ProfileController(ForumDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(userId);
            
            if (user == null) return NotFound();

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Avatar = user.Avatar,
                Username = user.Username
            };
            
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Update(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                model.Username = (await _context.Users.FindAsync(userId)).Username;
                return View("Index", model);
            }

            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(id);

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Avatar = model.Avatar;

            await _context.SaveChangesAsync();

            ViewBag.SuccessMessage = "Profile updated successfully.";
            model.Username = user.Username;

            return View("Index", model);
        }
    }
}
