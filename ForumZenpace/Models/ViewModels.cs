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
        public bool IsOwner { get; set; }
        public bool IsAuthenticatedViewer { get; set; }
        public string ActiveTab { get; set; } = "posts";
        public bool ShowChatTab { get; set; }
        public bool CanSendMessages { get; set; }
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
