using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForumZenpace.Models
{
    public class Role
    {
        public int Id { get; set; }
        
        [Required, MaxLength(50)]
        public string Name { get; set; }
        
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    public class User
    {
        public int Id { get; set; }
        
        [Required, MaxLength(50)]
        public string Username { get; set; }
        
        [Required]
        public string Password { get; set; }
        
        [Required, MaxLength(100)]
        public string FullName { get; set; }
        
        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; }
        
        [MaxLength(255)]
        public string? Avatar { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;

        public int RoleId { get; set; }
        [ForeignKey("RoleId")]
        public Role Role { get; set; }

        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Report> Reports { get; set; } = new List<Report>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class Category
    {
        public int Id { get; set; }
        
        [Required, MaxLength(100)]
        public string Name { get; set; }

        public ICollection<Post> Posts { get; set; } = new List<Post>();
    }

    public class Post
    {
        public int Id { get; set; }
        
        [Required, MaxLength(255)]
        public string Title { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public int ViewCount { get; set; } = 0;
        
        public string Status { get; set; } = "Active"; // Active, Hidden

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category Category { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Report> Reports { get; set; } = new List<Report>();
    }

    public class Comment
    {
        public int Id { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        public int PostId { get; set; }
        [ForeignKey("PostId")]
        public Post Post { get; set; }

        public int? ParentId { get; set; }
        [ForeignKey("ParentId")]
        public Comment? ParentComment { get; set; }
        
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }

    public class Like
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        public int PostId { get; set; }
        [ForeignKey("PostId")]
        public Post Post { get; set; }
    }

    public class Report
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        public int PostId { get; set; }
        [ForeignKey("PostId")]
        public Post Post { get; set; }

        [Required, MaxLength(255)]
        public string Reason { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        public string Content { get; set; }

        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
