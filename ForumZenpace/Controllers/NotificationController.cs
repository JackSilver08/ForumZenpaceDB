using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using ForumZenpace.Hubs;
using ForumZenpace.Services;

namespace ForumZenpace.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly SocialService _socialService;
        private readonly IHubContext<SocialHub> _hubContext;

        public NotificationController(SocialService socialService, IHubContext<SocialHub> hubContext)
        {
            _socialService = socialService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            return View(await _socialService.GetNotificationPageAsync(userId));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var unreadCount = await _socialService.MarkNotificationAsReadAsync(userId, id);
            await _hubContext.Clients.Group(SocialChannel.GetUserGroupName(userId))
                .SendAsync("NotificationCountChanged", new { unreadCount });
            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var unreadCount = await _socialService.MarkAllNotificationsAsReadAsync(userId);
            await _hubContext.Clients.Group(SocialChannel.GetUserGroupName(userId))
                .SendAsync("NotificationCountChanged", new { unreadCount });
            return RedirectToAction(nameof(Index));
        }
    }
}
