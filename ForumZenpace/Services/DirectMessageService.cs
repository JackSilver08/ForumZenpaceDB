using ForumZenpace.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumZenpace.Services
{
    public sealed class DirectMessageService
    {
        private const int MaxMessageLength = 1000;
        private readonly ForumDbContext _context;

        public DirectMessageService(ForumDbContext context)
        {
            _context = context;
        }

        public async Task<DirectMessageSendResult> SendMessageAsync(int senderUserId, SendDirectMessageViewModel model, CancellationToken cancellationToken = default)
        {
            var username = model.Username?.Trim() ?? string.Empty;
            var content = model.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Failure("Noi dung tin nhan khong duoc de trong.");
            }

            if (content.Length > MaxMessageLength)
            {
                return Failure("Tin nhan chi duoc toi da 1000 ky tu.");
            }

            var sender = await _context.Users
                .Where(user => user.Id == senderUserId && user.IsActive)
                .Select(user => new
                {
                    user.Id,
                    user.Username,
                    user.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (sender is null)
            {
                return Failure("Ban can dang nhap de gui tin nhan.");
            }

            var targetUser = await _context.Users
                .Where(user => user.Id == model.TargetUserId && user.Username == username && user.IsActive)
                .Select(user => new
                {
                    user.Id,
                    user.Username,
                    user.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (targetUser is null)
            {
                return Failure("Khong tim thay nguoi dung nhan tin.");
            }

            var isConversationBlocked = await _context.MessageBlocks
                .AnyAsync(block =>
                    (block.BlockerUserId == senderUserId && block.BlockedUserId == targetUser.Id) ||
                    (block.BlockerUserId == targetUser.Id && block.BlockedUserId == senderUserId),
                    cancellationToken);

            if (isConversationBlocked)
            {
                return Failure("Tin nhan da bi chan boi mot trong hai ben.");
            }

            if (targetUser.Id == senderUserId)
            {
                return Failure("Ban khong the tu nhan tin cho chinh minh.");
            }

            var conversation = await GetOrCreateConversationAsync(senderUserId, targetUser.Id, cancellationToken);
            var createdAt = DateTime.UtcNow;
            conversation.UpdatedAt = createdAt;

            var message = new DirectMessage
            {
                ConversationId = conversation.Id,
                SenderId = senderUserId,
                Content = content,
                CreatedAt = createdAt
            };

            _context.DirectMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            return new DirectMessageSendResult
            {
                Success = true,
                ConversationGroupName = DirectMessageChannel.GetConversationGroupName(senderUserId, targetUser.Id),
                TargetUsername = targetUser.Username,
                TargetDisplayName = string.IsNullOrWhiteSpace(targetUser.FullName) ? targetUser.Username : targetUser.FullName,
                Message = new DirectMessageRealtimeViewModel
                {
                    Id = message.Id,
                    ConversationId = conversation.Id,
                    SenderId = senderUserId,
                    SenderDisplayName = string.IsNullOrWhiteSpace(sender.FullName) ? sender.Username : sender.FullName,
                    Content = content,
                    CreatedAtDisplay = createdAt.ToString("dd MMM, HH:mm"),
                    CreatedAtIso = createdAt.ToString("O")
                }
            };
        }

        public async Task MarkConversationAsReadAsync(int viewerUserId, int targetUserId, CancellationToken cancellationToken = default)
        {
            var (userAId, userBId) = OrderConversationUsers(viewerUserId, targetUserId);
            var unreadMessages = await _context.DirectMessages
                .Where(message =>
                    message.Conversation.UserAId == userAId &&
                    message.Conversation.UserBId == userBId &&
                    message.SenderId != viewerUserId &&
                    !message.IsRead)
                .ToListAsync(cancellationToken);

            if (unreadMessages.Count == 0)
            {
                return;
            }

            foreach (var unreadMessage in unreadMessages)
            {
                unreadMessage.IsRead = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<DirectConversation> GetOrCreateConversationAsync(int userId, int targetUserId, CancellationToken cancellationToken)
        {
            var existingConversation = await GetConversationQuery(userId, targetUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingConversation is not null)
            {
                return existingConversation;
            }

            var (userAId, userBId) = OrderConversationUsers(userId, targetUserId);
            var conversation = new DirectConversation
            {
                UserAId = userAId,
                UserBId = userBId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.DirectConversations.Add(conversation);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return conversation;
            }
            catch (DbUpdateException)
            {
                _context.Entry(conversation).State = EntityState.Detached;

                existingConversation = await GetConversationQuery(userId, targetUserId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingConversation is not null)
                {
                    return existingConversation;
                }

                throw;
            }
        }

        private IQueryable<DirectConversation> GetConversationQuery(int userId, int targetUserId)
        {
            var (userAId, userBId) = OrderConversationUsers(userId, targetUserId);
            return _context.DirectConversations.Where(conversation => conversation.UserAId == userAId && conversation.UserBId == userBId);
        }

        private static (int UserAId, int UserBId) OrderConversationUsers(int firstUserId, int secondUserId)
        {
            return firstUserId < secondUserId
                ? (firstUserId, secondUserId)
                : (secondUserId, firstUserId);
        }

        private static DirectMessageSendResult Failure(string errorMessage)
        {
            return new DirectMessageSendResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    public static class DirectMessageChannel
    {
        public static string GetConversationGroupName(int firstUserId, int secondUserId)
        {
            var userAId = Math.Min(firstUserId, secondUserId);
            var userBId = Math.Max(firstUserId, secondUserId);
            return $"dm:{userAId}:{userBId}";
        }
    }
}
