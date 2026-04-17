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
                .ThenByDescending(member => member.Role == GroupMemberRoles.ViceLeader)
                .ThenByDescending(member => member.Role == GroupMemberRoles.Moderator)
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
            var isBanned = currentUserId.HasValue && await _context.GroupBans
                .AsNoTracking()
                .AnyAsync(ban => ban.GroupId == group.Id && ban.UserId == currentUserId.Value, cancellationToken);
            var canManageAvatar = currentUserId.HasValue && members.Any(member => member.UserId == currentUserId.Value && member.Role == GroupMemberRoles.Admin);
            var currentUserRole = currentUserId.HasValue ? members.FirstOrDefault(member => member.UserId == currentUserId.Value)?.Role : null;
            var bannedMembers = await _context.GroupBans
                .AsNoTracking()
                .Where(ban => ban.GroupId == group.Id)
                .Include(ban => ban.User)
                .OrderBy(ban => ban.User.Username)
                .Select(ban => new GroupBanSummaryViewModel
                {
                    UserId = ban.UserId,
                    Username = ban.User.Username,
                    DisplayName = string.IsNullOrWhiteSpace(ban.User.FullName) ? ban.User.Username : ban.User.FullName,
                    AvatarUrl = ban.User.Avatar
                })
                .ToListAsync(cancellationToken);

            var availableFriendsToInvite = new List<InvitableFriendSummaryViewModel>();
            var canInviteFriends = false;

            if (currentUserId.HasValue && isJoined && !isBanned && (currentUserRole == GroupMemberRoles.Admin || currentUserRole == GroupMemberRoles.ViceLeader))
            {
                canInviteFriends = true;

                // Get all friend IDs (both UserA and UserB)
                var friendIds = await _context.Friendships
                    .AsNoTracking()
                    .Where(f => f.UserAId == currentUserId.Value || f.UserBId == currentUserId.Value)
                    .Select(f => f.UserAId == currentUserId.Value ? f.UserBId : f.UserAId)
                    .ToListAsync(cancellationToken);

                // Get member IDs and banned IDs
                var memberIds = members.Select(m => m.UserId).ToList();
                var bannedIds = bannedMembers.Select(b => b.UserId).ToList();

                // Filter friends: not members, not banned
                var invitableFriendIds = friendIds
                    .Where(fId => !memberIds.Contains(fId) && !bannedIds.Contains(fId))
                    .ToList();

                // Get friend details
                availableFriendsToInvite = await _context.Users
                    .AsNoTracking()
                    .Where(u => invitableFriendIds.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .Select(u => new InvitableFriendSummaryViewModel
                    {
                        UserId = u.Id,
                        Username = u.Username,
                        DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
                        AvatarUrl = u.Avatar
                    })
                    .ToListAsync(cancellationToken);
            }

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
                    .ThenByDescending(member => member.Role == GroupMemberRoles.ViceLeader)
                    .ThenByDescending(member => member.Role == GroupMemberRoles.Moderator)
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
                ErrorMessage = TempData["ErrorMessage"] as string ?? string.Empty,
                CurrentUserId = currentUserId,
                CanLeaveGroup = isJoined && currentUserRole != GroupMemberRoles.Admin,
                CanManageBans = currentUserRole == GroupMemberRoles.Admin,
                CanInviteFriends = canInviteFriends,
                IsBanned = isBanned,
                BannedMembers = bannedMembers,
                AvailableFriendsToInvite = availableFriendsToInvite,
                CurrentUserRole = currentUserRole
            });
        }

        [HttpPost("{slug}/Leave")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(string slug, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                return NotFound();
            }

            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null)
            {
                TempData["ErrorMessage"] = "Ban khong phai thanh vien cua group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (currentMember.Role == GroupMemberRoles.Admin)
            {
                TempData["ErrorMessage"] = "Truong nhom phai chuyen quyen sang nguoi khac hoac giai tan group truoc khi roi khoi.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            _context.GroupMembers.Remove(currentMember);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da roi khoi group.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
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

            var isBanned = await _context.GroupBans
                .AnyAsync(ban => ban.GroupId == id && ban.UserId == userId, cancellationToken);

            if (isBanned)
            {
                TempData["ErrorMessage"] = "Ban da bi danh khoi nhom, khong the tham gia lai.";
                return RedirectToAction(nameof(Details), new { slug = group.Slug });
            }

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

        [HttpPost("{slug}/TransferAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferAdmin(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is Admin
            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || currentMember.Role != GroupMemberRoles.Admin)
            {
                TempData["ErrorMessage"] = "Chi truong nhom moi co the chuyen vai tro truong nhom.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists
            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (targetMember.UserId == currentUserId)
            {
                TempData["ErrorMessage"] = "Ban khong the chuyen vai tro cho chinh minh.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Transfer admin role: Current Admin becomes Member, Target becomes Admin
            currentMember.Role = GroupMemberRoles.Member;
            targetMember.Role = GroupMemberRoles.Admin;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da chuyen vai tro truong nhom thanh cong.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/AppointViceLeader")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AppointViceLeader(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is Admin
            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || currentMember.Role != GroupMemberRoles.Admin)
            {
                TempData["ErrorMessage"] = "Chi truong nhom moi co the bo nhiem pho nhom.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists and is Member
            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (targetMember.Role != GroupMemberRoles.Member)
            {
                TempData["ErrorMessage"] = "Chi co thanh vien binh thuong moi co the duoc bo nhiem lam pho.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Appoint as ViceLeader
            targetMember.Role = GroupMemberRoles.ViceLeader;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da bo nhiem pho nhom thanh cong.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/RemoveViceLeader")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveViceLeader(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is Admin
            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || currentMember.Role != GroupMemberRoles.Admin)
            {
                TempData["ErrorMessage"] = "Chi truong nhom moi co the thu hoi chuc vu pho nhom.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists and is ViceLeader
            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (targetMember.Role != GroupMemberRoles.ViceLeader)
            {
                TempData["ErrorMessage"] = "Thanh vien nay khong phai la pho nhom.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Demote to Member
            targetMember.Role = GroupMemberRoles.Member;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da thu hoi chuc vu pho nhom thanh cong.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/AppointModerator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AppointModerator(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is Admin or ViceLeader
            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || (currentMember.Role != GroupMemberRoles.Admin && currentMember.Role != GroupMemberRoles.ViceLeader))
            {
                TempData["ErrorMessage"] = "Chi truong nhom hoac pho nhom moi co the ban tặng quan tri vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists and is Member
            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (targetMember.Role != GroupMemberRoles.Member)
            {
                TempData["ErrorMessage"] = "Chi co thanh vien binh thuong moi co the duoc ban tặng quan tri vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Appoint as Moderator
            targetMember.Role = GroupMemberRoles.Moderator;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da ban tặng quan tri vien thanh cong.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/RemoveModerator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveModerator(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is Admin or ViceLeader
            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || (currentMember.Role != GroupMemberRoles.Admin && currentMember.Role != GroupMemberRoles.ViceLeader))
            {
                TempData["ErrorMessage"] = "Chi truong nhom hoac pho nhom moi co the thu hoi chuc vu quan tri vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists and is Moderator
            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (targetMember.Role != GroupMemberRoles.Moderator)
            {
                TempData["ErrorMessage"] = "Thanh vien nay khong phai la quan tri vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Demote to Member
            targetMember.Role = GroupMemberRoles.Member;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da thu hoi chuc vu quan tri vien thanh cong.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/KickMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KickMember(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is Admin, ViceLeader, or Moderator
            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || (currentMember.Role != GroupMemberRoles.Admin && currentMember.Role != GroupMemberRoles.ViceLeader && currentMember.Role != GroupMemberRoles.Moderator))
            {
                TempData["ErrorMessage"] = "Ban khong co quyen kick thanh vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists
            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Cannot kick self
            if (targetMember.UserId == currentUserId)
            {
                TempData["ErrorMessage"] = "Ban khong the kick chinh minh.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Moderator can only kick regular members
            if (currentMember.Role == GroupMemberRoles.Moderator && targetMember.Role != GroupMemberRoles.Member)
            {
                TempData["ErrorMessage"] = "Quyen quan tri vien chi duoc kick thanh vien thuong.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // ViceLeader cannot kick group owner or other vice leaders
            if (currentMember.Role == GroupMemberRoles.ViceLeader && (targetMember.Role == GroupMemberRoles.Admin || targetMember.Role == GroupMemberRoles.ViceLeader))
            {
                TempData["ErrorMessage"] = "Pho nhom chi duoc kick quan tri vien hoac thanh vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Remove member
            _context.GroupMembers.Remove(targetMember);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da kick thanh vien khoi group.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/BanMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanMember(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || (currentMember.Role != GroupMemberRoles.Admin && currentMember.Role != GroupMemberRoles.ViceLeader && currentMember.Role != GroupMemberRoles.Moderator))
            {
                TempData["ErrorMessage"] = "Ban khong co quyen kick vien vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            var targetMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == targetUserId,
                    cancellationToken);

            if (targetMember is null)
            {
                TempData["ErrorMessage"] = "Thanh vien muc tieu khong co trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (targetMember.UserId == currentUserId)
            {
                TempData["ErrorMessage"] = "Ban khong the kick chinh minh.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (currentMember.Role == GroupMemberRoles.Moderator && targetMember.Role != GroupMemberRoles.Member)
            {
                TempData["ErrorMessage"] = "Quyen quan tri vien chi duoc kick vien vien thanh vien thuong.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (currentMember.Role == GroupMemberRoles.ViceLeader && (targetMember.Role == GroupMemberRoles.Admin || targetMember.Role == GroupMemberRoles.ViceLeader))
            {
                TempData["ErrorMessage"] = "Pho nhom chi duoc kick vien vien quan tri vien hoac thanh vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            if (await _context.GroupBans.AnyAsync(ban => ban.GroupId == group.Id && ban.UserId == targetUserId, cancellationToken))
            {
                TempData["ErrorMessage"] = "Thanh vien da bi kick vien vien truoc do.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            _context.GroupBans.Add(new GroupBan
            {
                GroupId = group.Id,
                UserId = targetUserId
            });

            _context.GroupMembers.Remove(targetMember);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da kick vien vien thanh cong. Thanh vien khong the tham gia lai.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/UnbanMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbanMember(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            var currentMember = await _context.GroupMembers
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || currentMember.Role != GroupMemberRoles.Admin)
            {
                TempData["ErrorMessage"] = "Chi truong nhom moi co the go kick vien vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            var ban = await _context.GroupBans
                .FirstOrDefaultAsync(item => item.GroupId == group.Id && item.UserId == targetUserId, cancellationToken);

            if (ban is null)
            {
                TempData["ErrorMessage"] = "Thanh vien nay khong bi kick vien vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            _context.GroupBans.Remove(ban);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = "Da go kick vien vien. Thanh vien co the tham gia lai group.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/InviteFriend")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteFriend(string slug, int targetUserId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var normalizedSlug = slug.Trim();
            var group = await _context.Groups
                .FirstOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

            if (group is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if current user is in group and has permission to invite (Admin or ViceLeader)
            var currentMember = await _context.GroupMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(member =>
                    member.GroupId == group.Id &&
                    member.UserId == currentUserId,
                    cancellationToken);

            if (currentMember is null || (currentMember.Role != GroupMemberRoles.Admin && currentMember.Role != GroupMemberRoles.ViceLeader))
            {
                TempData["ErrorMessage"] = "Ban khong co quyen moi ban be vao group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if target user exists
            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

            if (targetUser is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay ban be.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if they are friends
            var isFriend = await _context.Friendships
                .AsNoTracking()
                .AnyAsync(f =>
                    (f.UserAId == currentUserId && f.UserBId == targetUserId) ||
                    (f.UserAId == targetUserId && f.UserBId == currentUserId),
                    cancellationToken);

            if (!isFriend)
            {
                TempData["ErrorMessage"] = "Thanh vien nay khong phai la ban be cua ban.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if already a member
            var isAlreadyMember = await _context.GroupMembers
                .AsNoTracking()
                .AnyAsync(m => m.GroupId == group.Id && m.UserId == targetUserId, cancellationToken);

            if (isAlreadyMember)
            {
                TempData["ErrorMessage"] = "Thanh vien da trong group.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if banned
            var isBanned = await _context.GroupBans
                .AsNoTracking()
                .AnyAsync(b => b.GroupId == group.Id && b.UserId == targetUserId, cancellationToken);

            if (isBanned)
            {
                TempData["ErrorMessage"] = "Thanh vien da bi kick vien vien.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Check if already invited
            var existingInvitation = await _context.GroupInvitations
                .FirstOrDefaultAsync(i => i.GroupId == group.Id && i.ReceiverId == targetUserId && i.Status == GroupInvitationStatuses.Pending, cancellationToken);

            if (existingInvitation is not null)
            {
                TempData["ErrorMessage"] = "Da gui loi moi roi.";
                return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
            }

            // Create invitation
            var invitation = new GroupInvitation
            {
                GroupId = group.Id,
                SenderId = currentUserId,
                ReceiverId = targetUserId,
                Status = GroupInvitationStatuses.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.GroupInvitations.Add(invitation);
            await _context.SaveChangesAsync(cancellationToken);

            // Create notification
            var notification = new Notification
            {
                UserId = targetUserId,
                Content = $"{currentMember.User?.FullName ?? "Someone"} da moi ban vao group {group.Name}",
                Type = NotificationTypes.GroupInvitation,
                ActorUserId = currentUserId,
                GroupInvitationId = invitation.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = $"Da moi {targetUser.FullName} vao group.";
            return RedirectToAction(nameof(Details), new { slug = normalizedSlug });
        }

        [HttpPost("{slug}/AcceptInvitation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptInvitation(string slug, int invitationId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var invitation = await _context.GroupInvitations
                .Include(i => i.Group)
                .FirstOrDefaultAsync(i => i.Id == invitationId && i.ReceiverId == currentUserId, cancellationToken);

            if (invitation is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay loi moi.";
                return RedirectToAction(nameof(Details), new { slug = slug.Trim() });
            }

            if (invitation.Status != GroupInvitationStatuses.Pending)
            {
                TempData["ErrorMessage"] = "Loi moi nay khong con hoat dong.";
                return RedirectToAction(nameof(Details), new { slug = invitation.Group.Slug });
            }

            // Add to group
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = invitation.GroupId,
                UserId = currentUserId,
                Role = GroupMemberRoles.Member,
                JoinedAt = DateTime.UtcNow
            });

            invitation.Status = GroupInvitationStatuses.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = $"Da tham gia group {invitation.Group.Name}.";
            return RedirectToAction(nameof(Details), new { slug = invitation.Group.Slug });
        }

        [HttpPost("{slug}/DeclineInvitation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineInvitation(string slug, int invitationId, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var invitation = await _context.GroupInvitations
                .Include(i => i.Group)
                .FirstOrDefaultAsync(i => i.Id == invitationId && i.ReceiverId == currentUserId, cancellationToken);

            if (invitation is null)
            {
                TempData["ErrorMessage"] = "Khong the tim thay loi moi.";
                return RedirectToAction(nameof(Details), new { slug = slug.Trim() });
            }

            if (invitation.Status != GroupInvitationStatuses.Pending)
            {
                TempData["ErrorMessage"] = "Loi moi nay khong con hoat dong.";
                return RedirectToAction(nameof(Details), new { slug = invitation.Group.Slug });
            }

            invitation.Status = GroupInvitationStatuses.Declined;
            invitation.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = $"Da tu choi loi moi tham gia {invitation.Group.Name}.";
            return RedirectToAction("Index", "Home");
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
