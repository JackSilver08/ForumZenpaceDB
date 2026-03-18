using Microsoft.AspNetCore.Http;
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
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        [Required]
        public int CategoryId { get; set; }
    }

    public class CommentViewModel
    {
        [Required]
        public string Content { get; set; }
        public int PostId { get; set; }
        public int? ParentId { get; set; }
    }

    public class ProfileViewModel
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; }

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; }

        public string? Avatar { get; set; }
        public IFormFile? AvatarFile { get; set; }
        
        // Expose username just for display
        public string? Username { get; set; }
    }
}
