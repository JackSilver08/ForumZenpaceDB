using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ForumZenpace.Models;

namespace ForumZenpace.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private static readonly HashSet<string> AllowedAvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };
        private const long MaxAvatarSizeBytes = 5 * 1024 * 1024;
        private readonly ForumDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(ForumDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Challenge();

            var user = await _context.Users.FindAsync(userId.Value);
            
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ProfileViewModel model)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Challenge();

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null) return NotFound();

            ValidateAvatarFile(model.AvatarFile);

            if (!ModelState.IsValid)
            {
                model.Username = user.Username;
                model.Avatar = user.Avatar;
                return View("Index", model);
            }

            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();

            if (model.AvatarFile is not null)
            {
                user.Avatar = await SaveAvatarAsync(model.AvatarFile, user.Id);
            }

            await _context.SaveChangesAsync();

            ViewBag.SuccessMessage = "Cap nhat ho so thanh cong.";
            model.Username = user.Username;
            model.Avatar = user.Avatar;

            return View("Index", model);
        }

        private void ValidateAvatarFile(IFormFile? avatarFile)
        {
            if (avatarFile is null)
            {
                return;
            }

            if (avatarFile.Length == 0)
            {
                ModelState.AddModelError(nameof(ProfileViewModel.AvatarFile), "Anh tai len dang trong.");
                return;
            }

            if (avatarFile.Length > MaxAvatarSizeBytes)
            {
                ModelState.AddModelError(nameof(ProfileViewModel.AvatarFile), "Anh dai dien chi duoc toi da 5MB.");
            }

            var extension = Path.GetExtension(avatarFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedAvatarExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(ProfileViewModel.AvatarFile), "Chi chap nhan file JPG, PNG, GIF hoac WEBP.");
            }
        }

        private async Task<string> SaveAvatarAsync(IFormFile avatarFile, int userId)
        {
            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var avatarDirectory = Path.Combine(webRootPath, "uploads", "avatars");
            Directory.CreateDirectory(avatarDirectory);

            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            var fileName = $"avatar-{userId}-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(avatarDirectory, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await avatarFile.CopyToAsync(stream);

            return $"/uploads/avatars/{fileName}";
        }

        private int? GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;
        }
    }
}
