using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForumZenpace.Models;

namespace ForumZenpace.Controllers
{
    public class HomeController : Controller
    {
        private readonly ForumDbContext _context;

        public HomeController(ForumDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString, int? categoryId, string sortOrder)
        {
            var posts = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Category)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                .Where(p => p.Status == "Active")
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                posts = posts.Where(p => p.Title.Contains(searchString) || p.Content.Contains(searchString));
            }

            if (categoryId.HasValue)
            {
                posts = posts.Where(p => p.CategoryId == categoryId.Value);
            }

            switch (sortOrder)
            {
                case "views":
                    posts = posts.OrderByDescending(p => p.ViewCount);
                    break;
                case "likes":
                    posts = posts.OrderByDescending(p => p.Likes.Count);
                    break;
                default:
                    posts = posts.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentCategory = categoryId;
            ViewBag.SearchString = searchString;

            return View(await posts.ToListAsync());
        }

        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Posts)
                .ToListAsync();
            return View(categories);
        }
    }
}
