using ForumZenpace.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumZenpace.Models
{
    public static class DbInitializer
    {
        public static async Task Initialize(ForumDbContext context)
        {
            // Đảm bảo Database đã được tạo
            context.Database.EnsureCreated();

            // Kiểm tra xem đã có Category nào chưa
            if (context.Categories.Any())
            {
                return;   // DB đã có dữ liệu, không cần seed nữa
            }

            // Thêm Categories mẫu nếu chưa có
            var categories = new Category[]
            {
                new Category { Name = "Lập trình & Kỹ thuật" },
                new Category { Name = "Cuộc sống số" },
                new Category { Name = "Thảo luận chung" }
            };
            foreach (var c in categories) { context.Categories.Add(c); }
            await context.SaveChangesAsync();

            // Thêm User mẫu nếu chưa có (RoleId 1 là Admin)
            if (!context.Users.Any())
            {
                var admin = new User
                {
                    Username = "admin",
                    Password = "AdminPassword123!",
                    FullName = "Quản trị viên Zenpace",
                    Email = "admin@zenpace.com",
                    RoleId = 1,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(admin);
                await context.SaveChangesAsync();

                // Thêm bài viết mẫu đầu tiên
                var post = new Post
                {
                    Title = "Chào mừng bạn đến với Diễn đàn Zenpace!",
                    Content = "Đây là bài viết đầu tiên được khởi tạo tự động để chào mừng bạn gia nhập cộng đồng Zenpace. Hãy bắt đầu chia sẻ tri thức của bạn tại đây!",
                    UserId = admin.Id,
                    CategoryId = categories[0].Id,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    ViewCount = 100
                };
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }
        }
    }
}
