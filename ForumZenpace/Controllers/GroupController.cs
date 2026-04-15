using System.Security.Claims;
using ForumZenpace.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumZenpace.Controllers
{
    [Authorize]
    [Route("Group")]
    public class GroupController : Controller
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

        public GroupController(ForumDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [AllowAnonymous]
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            var groups = await BuildGroupCardsQuery(currentUserId)
                .OrderByDescending(group => group.MemberCount)
                .ThenBy(group => group.Name)
                .ToListAsync(cancellationToken);

            return View(groups);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View(new GroupCreateViewModel());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupCreateViewModel model, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Description = model.Description?.Trim() ?? string.Empty;
            model.AccentColor = NormalizeAccentColor(model.AccentColor);
            ValidateAvatarFile(model.AvatarFile);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var group = new ForumZenpace.Models.Group
            {
                Name = model.Name,
                Description = model.Description,
                AccentColor = model.AccentColor,
                Avatar = await SaveAvatarAsync(model.AvatarFile, cancellationToken),
                Slug = await CreateUniqueSlugAsync(model.Name, cancellationToken),
                CreatorUserId = userId
            };

            _context.Groups.Add(group);
            await _context.SaveChangesAsync(cancellationToken);

            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                Role = GroupMemberRoles.Admin
            });

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Details), new { slug = group.Slug });
        }

        [AllowAnonymous]
        [HttpGet("{slug}")]
        public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return NotFound();
            }

            var currentUserId = GetCurrentUserId();
            var normalizedSlug = slug.Trim();

            var group = await _context.Groups
                .AsNoTracking()
                .Include(item => item.CreatorUser)
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                return NotFound();
            }

            var members = await _context.GroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == group.Id)
                .Include(member => member.User)
                .OrderByDescending(member => member.Role == GroupMemberRoles.Admin)
                .ThenBy(member => member.User.FullName)
                .ToListAsync(cancellationToken);

            var posts = await _context.Posts
                .AsNoTracking()
                .Where(post => post.GroupId == group.Id && post.Status == "Active")
                .Include(post => post.User)
                .Include(post => post.Category)
                .Include(post => post.Likes)
                .Include(post => post.Comments)
                .OrderByDescending(post => post.CreatedAt)
                .ToListAsync(cancellationToken);

            var isJoined = currentUserId.HasValue && members.Any(member => member.UserId == currentUserId.Value);
            var canManageAvatar = currentUserId.HasValue && members.Any(member => member.UserId == currentUserId.Value && member.Role == GroupMemberRoles.Admin);

            return View(new GroupDetailsViewModel
            {
                Group = new GroupListItemViewModel
                {
                    Id = group.Id,
                    Name = group.Name,
                    Slug = group.Slug,
                    Description = group.Description,
                    AccentColor = group.AccentColor,
                    AvatarUrl = group.Avatar,
                    CreatorDisplayName = string.IsNullOrWhiteSpace(group.CreatorUser.FullName) ? group.CreatorUser.Username : group.CreatorUser.FullName,
                    MemberCount = members.Count,
                    PostCount = posts.Count,
                    IsJoined = isJoined,
                    IsOwnedByCurrentUser = canManageAvatar
                },
                Posts = posts,
                Members = members
                    .OrderByDescending(member => member.Role == GroupMemberRoles.Admin)
                    .ThenBy(member => member.User.FullName)
                    .Select(member => new GroupMemberSummaryViewModel
                    {
                        UserId = member.UserId,
                        Username = member.User.Username,
                        DisplayName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Username : member.User.FullName,
                        AvatarUrl = member.User.Avatar,
                        Role = member.Role
                    })
                    .ToList(),
                CanCreatePosts = isJoined,
                SuccessMessage = TempData["SuccessMessage"] as string ?? string.Empty,
                ErrorMessage = TempData["ErrorMessage"] as string ?? string.Empty
            });
        }

        [HttpPost("{slug}/Avatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(string slug, GroupAvatarUpdateViewModel model, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            model.Slug = normalizedSlug;
            ValidateAvatarFile(model.AvatarFile);

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = ModelState.Values
                    .SelectMany(entry => entry.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "Khong the cap nhat anh dai dien group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                return NotFound();
            }

            var canManageAvatar = await _context.GroupMembers
                .AnyAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == userId &&
                    member.Role == GroupMemberRoles.Admin,
                    cancellationToken);

            if (!canManageAvatar)
            {
                return Forbid();
            }

            var previousAvatar = group.Avatar;
            group.Avatar = await SaveAvatarAsync(model.AvatarFile, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            DeleteAvatarFile(previousAvatar);

            TempData["SuccessMessage"] = "Da cap nhat anh dai dien cho group.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("Join")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int id, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var group = await _context.Groups.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (group is null)
            {
                return NotFound();
            }

            var alreadyMember = await _context.GroupMembers
                .AnyAsync(member => member.GroupId == id && member.UserId == userId, cancellationToken);

            if (!alreadyMember)
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    GroupId = id,
                    UserId = userId,
                    Role = GroupMemberRoles.Member
                });

                await _context.SaveChangesAsync(cancellationToken);
            }

            return RedirectToAction(nameof(Details), new { slug = group.Slug });
        }

        private IQueryable<GroupListItemViewModel> BuildGroupCardsQuery(int? currentUserId)
        {
            return _context.Groups
                .AsNoTracking()
                .Select(group => new GroupListItemViewModel
                {
                    Id = group.Id,
                    Name = group.Name,
                    Slug = group.Slug,
                    Description = group.Description,
                    AccentColor = group.AccentColor,
                    AvatarUrl = group.Avatar,
                    CreatorDisplayName = string.IsNullOrWhiteSpace(group.CreatorUser.FullName) ? group.CreatorUser.Username : group.CreatorUser.FullName,
                    MemberCount = group.Members.Count,
                    PostCount = group.Posts.Count(post => post.Status == "Active"),
                    IsJoined = currentUserId.HasValue && group.Members.Any(member => member.UserId == currentUserId.Value),
                    IsOwnedByCurrentUser = currentUserId.HasValue && group.CreatorUserId == currentUserId.Value
                });
        }

        private async Task<string> CreateUniqueSlugAsync(string groupName, CancellationToken cancellationToken)
        {
            var baseSlug = Slugify(groupName);
            var slug = baseSlug;
            var suffix = 2;

            while (await _context.Groups.AnyAsync(group => group.Slug == slug, cancellationToken))
            {
                slug = $"{baseSlug}-{suffix++}";
            }

            return slug;
        }

        private static string Slugify(string input)
        {
            var normalized = System.Text.RegularExpressions.Regex.Replace((input ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"-+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? $"group-{Guid.NewGuid():N}"[..14] : normalized;
        }

        private void ValidateAvatarFile(IFormFile? avatarFile)
        {
            if (avatarFile is null)
            {
                return;
            }

            if (avatarFile.Length == 0)
            {
                ModelState.AddModelError(nameof(GroupCreateViewModel.AvatarFile), "Anh dai dien dang trong.");
                return;
            }

            if (avatarFile.Length > MaxAvatarSizeBytes)
            {
                ModelState.AddModelError(nameof(GroupCreateViewModel.AvatarFile), "Anh dai dien chi duoc toi da 5MB.");
            }

            var extension = Path.GetExtension(avatarFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedAvatarExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(GroupCreateViewModel.AvatarFile), "Chi chap nhan file JPG, PNG, GIF hoac WEBP.");
            }
        }

        private async Task<string?> SaveAvatarAsync(IFormFile? avatarFile, CancellationToken cancellationToken)
        {
            if (avatarFile is null || avatarFile.Length == 0)
            {
                return null;
            }

            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var groupDirectory = Path.Combine(webRootPath, "uploads", "groups");
            Directory.CreateDirectory(groupDirectory);

            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            var fileName = $"group-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(groupDirectory, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await avatarFile.CopyToAsync(stream, cancellationToken);

            return $"/uploads/groups/{fileName}";
        }

        private void DeleteAvatarFile(string? avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath))
            {
                return;
            }

            var relativePath = avatarPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var absolutePath = Path.Combine(webRootPath, relativePath);

            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
        }

        private static string NormalizeAccentColor(string? accentColor)
        {
            return accentColor switch
            {
                GroupAccentColors.Coral => GroupAccentColors.Coral,
                GroupAccentColors.Mint => GroupAccentColors.Mint,
                GroupAccentColors.Gold => GroupAccentColors.Gold,
                _ => GroupAccentColors.Sky
            };
        }

        private int? GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : null;
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
    }
}
