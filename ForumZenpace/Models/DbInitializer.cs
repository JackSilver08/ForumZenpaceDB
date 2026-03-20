using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ForumZenpace.Models
{
    public static class DbInitializer
    {
        private const string ProductVersion = "10.0.5";
        private const string InitialCreateMigrationId = "20260318020427_InitialCreate";
        private const string SeedInitialDataMigrationId = "20260318063635_SeedInitialData";

        public static async Task Initialize(ForumDbContext context)
        {
            await EnsureDatabaseSchemaAsync(context);

            if (context.Categories.Any())
            {
                return;
            }

            var categories = new Category[]
            {
                new Category { Name = "Lập trình & Kỹ thuật" },
                new Category { Name = "Cuộc sống số" },
                new Category { Name = "Thảo luận chung" }
            };

            foreach (var category in categories)
            {
                context.Categories.Add(category);
            }

            await context.SaveChangesAsync();

            if (!context.Users.Any())
            {
                var admin = new User
                {
                    Username = "admin",
                    Password = "AdminPassword123!",
                    FullName = "Quản trị viên Zenpace",
                    Email = "admin@zenpace.com",
                    IsEmailConfirmed = true,
                    RoleId = 1,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(admin);
                await context.SaveChangesAsync();

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

        private static async Task EnsureDatabaseSchemaAsync(ForumDbContext context)
        {
            if (!await context.Database.CanConnectAsync())
            {
                await context.Database.MigrateAsync();
                return;
            }

            await AlignMigrationHistoryAsync(context);

            await context.Database.MigrateAsync();
        }

        private static async Task AlignMigrationHistoryAsync(ForumDbContext context)
        {
            var hasCoreTables = await TableExistsAsync(context, "Posts")
                && await TableExistsAsync(context, "Comments")
                && await TableExistsAsync(context, "Users");

            if (!hasCoreTables)
            {
                return;
            }

            await EnsureMigrationHistoryTableAsync(context);
            await EnsureMigrationHistoryRowAsync(context, InitialCreateMigrationId);

            if (await SeedDataExistsAsync(context))
            {
                await EnsureMigrationHistoryRowAsync(context, SeedInitialDataMigrationId);
            }
        }

        private static async Task EnsureMigrationHistoryTableAsync(ForumDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [__EFMigrationsHistory]
                    (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    );
                END
                """);
        }

        private static async Task EnsureMigrationHistoryRowAsync(ForumDbContext context, string migrationId)
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = {0})
                BEGIN
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES ({0}, {1});
                END
                """,
                migrationId,
                ProductVersion);
        }

        private static async Task<bool> SeedDataExistsAsync(ForumDbContext context)
        {
            if (!await TableExistsAsync(context, "Categories"))
            {
                return false;
            }

            var categoryIds = new[] { 1, 2, 3 };
            var seededCategoryCount = await context.Categories.CountAsync(c => categoryIds.Contains(c.Id));
            var hasAdminUser = await context.Users.AnyAsync(u => u.Id == 1);

            return seededCategoryCount == categoryIds.Length && hasAdminUser;
        }

        private static async Task<bool> TableExistsAsync(ForumDbContext context, string tableName)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT 1
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName
                    """;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
