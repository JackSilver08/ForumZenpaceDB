using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ForumZenpace.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class RegisterViewModel
    {
        [Required, MaxLength(50)]
        public string Username { get; set; }
        
        [Required, MaxLength(100)]
        public string FullName { get; set; }

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }
        
        [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }
    }

    public class PostViewModel
    {
        public int? PostId { get; set; }

        [MaxLength(64)]
        public string DraftToken { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        [Required]
        public int CategoryId { get; set; }
    }

    public class PostImageUploadViewModel
    {
        public int? PostId { get; set; }

        [MaxLength(64)]
        public string? DraftToken { get; set; }

        [Required]
        public IFormFile Image { get; set; } = null!;
    }

    public class CommentViewModel
    {
        [Required]
        public string Content { get; set; }
        public int PostId { get; set; }
        public int? ParentId { get; set; }
    }

    public class CommentThreadViewModel
    {
        [Required]
        public Comment RootComment { get; set; } = null!;

        public IReadOnlyList<CommentReplyViewModel> Replies { get; set; } = Array.Empty<CommentReplyViewModel>();

        public int PostId { get; set; }
        public int? CurrentUserId { get; set; }
        public bool IsAuthenticated { get; set; }
        public int InitialVisibleReplies { get; set; } = 3;
    }

    public class CommentReplyViewModel
    {
        [Required]
        public Comment Comment { get; set; } = null!;

        public int Depth { get; set; }
        public string? ReplyingToAuthorName { get; set; }
    }

    public class HomeIndexViewModel
    {
        public IReadOnlyList<Post> Posts { get; set; } = Array.Empty<Post>();
        public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
        public string CurrentSort { get; set; } = string.Empty;
        public string? SearchString { get; set; }
        public int? CurrentCategoryId { get; set; }
        public int? CurrentUserId { get; set; }
        public int UnreadNotificationCount { get; set; }
        public IReadOnlyList<FriendSummaryViewModel> Friends { get; set; } = Array.Empty<FriendSummaryViewModel>();
    }

    public class CommentItemViewModel
    {
        [Required]
        public Comment Comment { get; set; } = null!;

        public int PostId { get; set; }
        public int? CurrentUserId { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsReply { get; set; }
        public string? ReplyingToAuthorName { get; set; }
    }

    public class ProfileViewModel
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        public string? Avatar { get; set; }
        public IFormFile? AvatarFile { get; set; }
        
        // Expose username just for display
        public string? Username { get; set; }
        public int ProfileUserId { get; set; }
        public int? ViewerUserId { get; set; }
        public bool IsOwner { get; set; }
        public bool IsAuthenticatedViewer { get; set; }
        public string ActiveTab { get; set; } = "posts";
        public bool ShowChatTab { get; set; }
        public bool CanSendMessages { get; set; }
        public bool IsFriend { get; set; }
        public bool HasIncomingFriendRequest { get; set; }
        public bool HasOutgoingFriendRequest { get; set; }
        public int? IncomingFriendRequestId { get; set; }
        public bool IsMessageBlockedByViewer { get; set; }
        public bool IsMessageBlockedByOtherUser { get; set; }
        public bool IsConversationBlocked { get; set; }
        public string ChatAvailabilityMessage { get; set; } = string.Empty;
        public int ChatMessageCount { get; set; }
        public IReadOnlyList<ProfileChatMessageViewModel> ChatMessages { get; set; } = Array.Empty<ProfileChatMessageViewModel>();
        public DateTime JoinedAt { get; set; }
        public int PostCount { get; set; }
        public int TotalViewCount { get; set; }
        public int TotalCommentCount { get; set; }
        public IReadOnlyList<ProfilePostSummaryViewModel> Posts { get; set; } = Array.Empty<ProfilePostSummaryViewModel>();
    }

    public class ProfileChatMessageViewModel
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsOwnMessage { get; set; }
        public string SenderDisplayName { get; set; } = string.Empty;
    }

    public class SendDirectMessageViewModel
    {
        public int TargetUserId { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }

    public class DirectMessageRealtimeViewModel
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderDisplayName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string CreatedAtDisplay { get; set; } = string.Empty;
        public string CreatedAtIso { get; set; } = string.Empty;
    }

    public class DirectMessageSendResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ConversationGroupName { get; set; } = string.Empty;
        public string TargetUsername { get; set; } = string.Empty;
        public string TargetDisplayName { get; set; } = string.Empty;
        public DirectMessageRealtimeViewModel? Message { get; set; }
    }

    public class FriendSummaryViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsMessageBlockedByViewer { get; set; }
        public bool IsMessageBlockedByOtherUser { get; set; }
    }

    public class FriendCandidateViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string RelationshipState { get; set; } = string.Empty;
        public bool CanSendRequest { get; set; }
        public string ActionLabel { get; set; } = string.Empty;
    }

    public class NotificationItemViewModel
    {
        public int Id { get; set; }
        public string Type { get; set; } = NotificationTypes.General;
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ActorUserId { get; set; }
        public string ActorUsername { get; set; } = string.Empty;
        public string ActorDisplayName { get; set; } = string.Empty;
        public string? ActorAvatarUrl { get; set; }
        public int? FriendRequestId { get; set; }
        public string FriendRequestStatus { get; set; } = string.Empty;
        public bool CanAcceptFriendRequest { get; set; }
        public bool CanDeclineFriendRequest { get; set; }
    }

    public class NotificationPageViewModel
    {
        public int CurrentUserId { get; set; }
        public int UnreadCount { get; set; }
        public IReadOnlyList<NotificationItemViewModel> Items { get; set; } = Array.Empty<NotificationItemViewModel>();
    }

    public class SendFriendRequestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int TargetUserId { get; set; }
        public NotificationItemViewModel? ReceiverNotification { get; set; }
        public int ReceiverUnreadNotificationCount { get; set; }
    }

    public class AcceptFriendRequestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public int SenderUserId { get; set; }
        public int ReceiverUserId { get; set; }
        public FriendSummaryViewModel? FriendForReceiver { get; set; }
        public FriendSummaryViewModel? FriendForSender { get; set; }
        public NotificationItemViewModel? SenderNotification { get; set; }
        public int SenderUnreadNotificationCount { get; set; }
        public int ReceiverUnreadNotificationCount { get; set; }
    }

    public class DeclineFriendRequestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public int SenderUserId { get; set; }
        public int ReceiverUnreadNotificationCount { get; set; }
    }

    public class RemoveFriendResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int FriendUserId { get; set; }
    }

    public class ToggleMessageBlockResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int TargetUserId { get; set; }
        public bool IsMessageBlockedByViewer { get; set; }
        public bool IsMessageBlockedByOtherUser { get; set; }
        public bool IsConversationBlocked { get; set; }
    }

    public class RelationshipStatusViewModel
    {
        public bool IsFriend { get; set; }
        public bool HasIncomingFriendRequest { get; set; }
        public bool HasOutgoingFriendRequest { get; set; }
        public int? IncomingFriendRequestId { get; set; }
        public bool IsMessageBlockedByViewer { get; set; }
        public bool IsMessageBlockedByOtherUser { get; set; }
        public bool IsConversationBlocked { get; set; }
    }

    public class ProfilePostSummaryViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int CommentCount { get; set; }
        public int ViewCount { get; set; }
    }
}
