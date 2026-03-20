using System.Security.Claims;
using ForumZenpace.Models;
using ForumZenpace.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ForumZenpace.Hubs
{
    [Authorize]
    public sealed class DirectMessageHub : Hub
    {
        private readonly DirectMessageService _directMessageService;

        public DirectMessageHub(DirectMessageService directMessageService)
        {
            _directMessageService = directMessageService;
        }

        public async Task JoinConversation(int targetUserId)
        {
            var currentUserId = GetCurrentUserId();
            if (targetUserId <= 0 || targetUserId == currentUserId)
            {
                throw new HubException("Khong tim thay cuoc tro chuyen hop le.");
            }

            var conversationGroup = DirectMessageChannel.GetConversationGroupName(currentUserId, targetUserId);
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationGroup);
            await _directMessageService.MarkConversationAsReadAsync(currentUserId, targetUserId);
        }

        public async Task MarkConversationAsRead(int targetUserId)
        {
            var currentUserId = GetCurrentUserId();
            if (targetUserId <= 0 || targetUserId == currentUserId)
            {
                return;
            }

            await _directMessageService.MarkConversationAsReadAsync(currentUserId, targetUserId);
        }

        public async Task SendDirectMessage(SendDirectMessageViewModel model)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _directMessageService.SendMessageAsync(currentUserId, model);
            if (!result.Success || result.Message is null)
            {
                throw new HubException(result.ErrorMessage);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, result.ConversationGroupName);
            await Clients.Group(result.ConversationGroupName)
                .SendAsync("DirectMessageReceived", result.Message);
        }

        private int GetCurrentUserId()
        {
            if (!int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                throw new HubException("Ban can dang nhap de su dung chat realtime.");
            }

            return userId;
        }
    }
}
