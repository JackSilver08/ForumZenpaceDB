using System.Security.Claims;
using ForumZenpace.Hubs;
using ForumZenpace.Models;
using ForumZenpace.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ForumZenpace.Controllers
{
    [Authorize]
    public class StoryController : Controller
    {
        private readonly StoryService _storyService;
        private readonly IHubContext<SocialHub> _hubContext;

        public StoryController(StoryService storyService, IHubContext<SocialHub> hubContext)
        {
            _storyService = storyService;
            _hubContext = hubContext;
        }

        [HttpGet("Story/Viewer/{id:int}")]
        public async Task<IActionResult> Viewer(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var viewModel = await _storyService.GetViewerPageAsync(id, userId, HttpContext.RequestAborted);
            return viewModel is null ? NotFound() : View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateStoryViewModel model)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var result = await _storyService.CreateStoryAsync(userId, model, HttpContext.RequestAborted);
            if (!result.Success)
            {
                if (IsAjaxRequest())
                {
                    return BadRequest(new { success = false, message = result.ErrorMessage });
                }

                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction("Index", "Home");
            }

            foreach (var delivery in result.NotificationDeliveries)
            {
                await _hubContext.Clients.Group(SocialChannel.GetUserGroupName(delivery.UserId))
                    .SendAsync("NotificationUpserted", delivery.Notification);

                await _hubContext.Clients.Group(SocialChannel.GetUserGroupName(delivery.UserId))
                    .SendAsync("NotificationCountChanged", new { unreadCount = delivery.UnreadNotificationCount });
            }

            var redirectUrl = result.Story is not null
                ? Url.Action(nameof(Viewer), new { id = result.Story.Id }) ?? StoryService.GetStoryViewerUrl(result.Story.Id)
                : Url.Action("Index", "Home") ?? "/";

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    redirectUrl
                });
            }

            return Redirect(redirectUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl = null)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var result = await _storyService.DeleteStoryAsync(userId, id, HttpContext.RequestAborted);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl!);
            }

            return RedirectToAction("Index", "Profile", new { tab = "stories" });
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }
    }
}
