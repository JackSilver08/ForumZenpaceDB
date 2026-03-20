using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ForumZenpace.Models
{
    public class ForumDbContext : DbContext
    {
        public ForumDbContext(DbContextOptions<ForumDbContext> options)
            : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }
        public DbSet<PostImage> PostImages { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<DirectConversation> DirectConversations { get; set; }
        public DbSet<DirectMessage> DirectMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UNIQUE(UserId, PostId) constraint for Like
            modelBuilder.Entity<Like>()
                .HasIndex(l => new { l.UserId, l.PostId })
                .IsUnique();

            modelBuilder.Entity<CommentLike>()
                .HasIndex(cl => new { cl.UserId, cl.CommentId })
                .IsUnique();

            modelBuilder.Entity<PostImage>()
                .HasIndex(pi => pi.DraftToken);

            modelBuilder.Entity<DirectConversation>()
                .HasIndex(dc => new { dc.UserAId, dc.UserBId })
                .IsUnique();

            modelBuilder.Entity<DirectMessage>()
                .HasIndex(dm => new { dm.ConversationId, dm.CreatedAt });

            modelBuilder.Entity<DirectConversation>()
                .HasOne(dc => dc.UserA)
                .WithMany(u => u.PrimaryDirectConversations)
                .HasForeignKey(dc => dc.UserAId);

            modelBuilder.Entity<DirectConversation>()
                .HasOne(dc => dc.UserB)
                .WithMany(u => u.SecondaryDirectConversations)
                .HasForeignKey(dc => dc.UserBId);

            modelBuilder.Entity<DirectMessage>()
                .HasOne(dm => dm.Conversation)
                .WithMany(dc => dc.Messages)
                .HasForeignKey(dm => dm.ConversationId);

            modelBuilder.Entity<DirectMessage>()
                .HasOne(dm => dm.Sender)
                .WithMany(u => u.DirectMessages)
                .HasForeignKey(dm => dm.SenderId);

            // Disable cascade delete globally or per specific relationship to avoid multiple cascade paths issue in SQL Server
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Lập trình & Kỹ thuật" },
                new Category { Id = 2, Name = "Thiết kế & Nghệ thuật" },
                new Category { Id = 3, Name = "Đời sống & Khoa học" }
            );

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "User" }
            );

            modelBuilder.Entity<User>().HasData(
                new
                {
                    Id = 1,
                    Username = "admin",
                    Password = "AdminPassword123!",
                    FullName = "Quản trị viên Zenpace",
                    Email = "admin@zenpace.com",
                    Avatar = (string?)null,
                    CreatedAt = new DateTime(2026, 3, 18, 6, 36, 35, 80, DateTimeKind.Utc).AddTicks(3815),
                    IsActive = true,
                    RoleId = 1
                }
            );
        }
    }
}
