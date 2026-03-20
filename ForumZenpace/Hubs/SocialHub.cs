using System.Security.Claims;
using ForumZenpace.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ForumZenpace.Hubs
{
    [Authorize]
    public sealed class SocialHub : Hub
    {
        private readonly SocialService _socialService;

        public SocialHub(SocialService socialService)
        {
            _socialService = socialService;
        }

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SocialChannel.GetUserGroupName(GetCurrentUserId()));
            await base.OnConnectedAsync();
        }

        public async Task<IReadOnlyList<Models.FriendCandidateViewModel>> SearchFriendCandidates(string? term)
        {
            return await _socialService.SearchFriendCandidatesAsync(GetCurrentUserId(), term);
        }

        public async Task SendFriendRequest(int targetUserId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _socialService.SendFriendRequestAsync(currentUserId, targetUserId);
            if (!result.Success)
            {
                throw new HubException(result.ErrorMessage);
            }

            if (result.ReceiverNotification is not null)
            {
                await Clients.Group(SocialChannel.GetUserGroupName(targetUserId))
                    .SendAsync("NotificationUpserted", result.ReceiverNotification);
            }

            await Clients.Group(SocialChannel.GetUserGroupName(targetUserId))
                .SendAsync("NotificationCountChanged", new { unreadCount = result.ReceiverUnreadNotificationCount });

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("FriendRequestStateChanged", new
                {
                    userId = targetUserId,
                    state = "pending-sent"
                });

            await Clients.Group(SocialChannel.GetUserGroupName(targetUserId))
                .SendAsync("FriendRequestStateChanged", new
                {
                    userId = currentUserId,
                    state = "pending-received"
                });
        }

        public async Task AcceptFriendRequest(int requestId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _socialService.AcceptFriendRequestAsync(currentUserId, requestId);
            if (!result.Success)
            {
                throw new HubException(result.ErrorMessage);
            }

            if (result.FriendForReceiver is not null)
            {
                await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                    .SendAsync("FriendshipAdded", result.FriendForReceiver);
            }

            if (result.FriendForSender is not null)
            {
                await Clients.Group(SocialChannel.GetUserGroupName(result.SenderUserId))
                    .SendAsync("FriendshipAdded", result.FriendForSender);

                await Clients.Group(SocialChannel.GetUserGroupName(result.SenderUserId))
                    .SendAsync("FriendRequestStateChanged", new
                    {
                        userId = currentUserId,
                        state = "friend"
                    });
            }

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("FriendRequestResolved", new
                {
                    requestId,
                    status = Models.FriendRequestStatuses.Accepted,
                    unreadCount = result.ReceiverUnreadNotificationCount
                });

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("NotificationCountChanged", new { unreadCount = result.ReceiverUnreadNotificationCount });

            if (result.SenderNotification is not null && result.FriendForSender is not null)
            {
                await Clients.Group(SocialChannel.GetUserGroupName(result.SenderUserId))
                    .SendAsync("NotificationUpserted", result.SenderNotification);
            }

            if (result.FriendForSender is not null)
            {
                await Clients.Group(SocialChannel.GetUserGroupName(result.SenderUserId))
                    .SendAsync("NotificationCountChanged", new { unreadCount = result.SenderUnreadNotificationCount });
            }
        }

        public async Task DeclineFriendRequest(int requestId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _socialService.DeclineFriendRequestAsync(currentUserId, requestId);
            if (!result.Success)
            {
                throw new HubException(result.ErrorMessage);
            }

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("FriendRequestResolved", new
                {
                    requestId,
                    status = Models.FriendRequestStatuses.Declined,
                    unreadCount = result.ReceiverUnreadNotificationCount
                });

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("NotificationCountChanged", new { unreadCount = result.ReceiverUnreadNotificationCount });

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("FriendRequestStateChanged", new
                {
                    userId = result.SenderUserId,
                    state = "none"
                });

            await Clients.Group(SocialChannel.GetUserGroupName(result.SenderUserId))
                .SendAsync("FriendRequestStateChanged", new
                {
                    userId = currentUserId,
                    state = "none"
                });
        }

        public async Task RemoveFriend(int friendUserId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _socialService.RemoveFriendAsync(currentUserId, friendUserId);
            if (!result.Success)
            {
                throw new HubException(result.ErrorMessage);
            }

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("FriendshipRemoved", new { friendUserId });

            await Clients.Group(SocialChannel.GetUserGroupName(friendUserId))
                .SendAsync("FriendshipRemoved", new { friendUserId = currentUserId });

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("FriendRequestStateChanged", new
                {
                    userId = friendUserId,
                    state = "none"
                });

            await Clients.Group(SocialChannel.GetUserGroupName(friendUserId))
                .SendAsync("FriendRequestStateChanged", new
                {
                    userId = currentUserId,
                    state = "none"
                });
        }

        public async Task ToggleMessageBlock(int targetUserId)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _socialService.ToggleMessageBlockAsync(currentUserId, targetUserId);
            if (!result.Success)
            {
                throw new HubException(result.ErrorMessage);
            }

            await Clients.Group(SocialChannel.GetUserGroupName(currentUserId))
                .SendAsync("MessageBlockChanged", new
                {
                    targetUserId,
                    isMessageBlockedByViewer = result.IsMessageBlockedByViewer,
                    isMessageBlockedByOtherUser = result.IsMessageBlockedByOtherUser,
                    isConversationBlocked = result.IsConversationBlocked
                });

            var reverseRelationship = await _socialService.GetRelationshipStatusAsync(targetUserId, currentUserId);
            await Clients.Group(SocialChannel.GetUserGroupName(targetUserId))
                .SendAsync("MessageBlockChanged", new
                {
                    targetUserId = currentUserId,
                    isMessageBlockedByViewer = reverseRelationship.IsMessageBlockedByViewer,
                    isMessageBlockedByOtherUser = reverseRelationship.IsMessageBlockedByOtherUser,
                    isConversationBlocked = reverseRelationship.IsConversationBlocked
                });
        }

        private int GetCurrentUserId()
        {
            if (!int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                throw new HubException("Ban can dang nhap de su dung tinh nang xa hoi.");
            }

            return userId;
        }
    }
}
