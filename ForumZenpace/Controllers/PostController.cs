using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ForumZenpace.Models;

namespace ForumZenpace.Controllers
{
    [Authorize]
    public class PostController : Controller
    {
        private readonly ForumDbContext _context;

        public PostController(ForumDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(PostViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", model.CategoryId);
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var post = new Post
            {
                Title = model.Title,
                Content = model.Content,
                CategoryId = model.CategoryId,
                UserId = userId
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = post.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            
            if (post == null) return NotFound("Post not found or you don't have permission.");

            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", post.CategoryId);
            ViewBag.PostId = id;
            
            return View(new PostViewModel {
                Title = post.Title,
                Content = post.Content,
                CategoryId = post.CategoryId
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, PostViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

            if (post == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name", model.CategoryId);
                ViewBag.PostId = id;
                return View(model);
            }

            post.Title = model.Title;
            post.Content = model.Content;
            post.CategoryId = model.CategoryId;
            post.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = post.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

            if (post != null)
            {
                var comments = await _context.Comments.Where(c => c.PostId == id).ToListAsync();
                var likes = await _context.Likes.Where(l => l.PostId == id).ToListAsync();
                var reports = await _context.Reports.Where(r => r.PostId == id).ToListAsync();

                _context.Comments.RemoveRange(comments);
                _context.Likes.RemoveRange(likes);
                _context.Reports.RemoveRange(reports);

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Category)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p => p.Id == id && p.Status == "Active");

            if (post == null) return NotFound();

            // Increase view count
            post.ViewCount++;
            await _context.SaveChangesAsync();
            
            int? userId = null;
            if (User.Identity.IsAuthenticated)
            {
                userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                ViewBag.HasLiked = post.Likes.Any(l => l.UserId == userId);
            }

            ViewBag.UserId = userId;
            
            // Build tree hierarchy for comments
            var topLevelComments = post.Comments.Where(c => c.ParentId == null).OrderBy(c => c.CreatedAt).ToList();
            ViewBag.TopLevelComments = topLevelComments;

            return View(post);
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(CommentViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Content)) 
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Comment content cannot be empty" });
                return RedirectToAction("Details", new { id = model.PostId });
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var comment = new Comment
            {
                Content = model.Content,
                PostId = model.PostId,
                ParentId = model.ParentId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            
            // Notify Post Owner
            var post = await _context.Posts.FindAsync(model.PostId);
            if (post.UserId != userId)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = post.UserId,
                    Content = $"Someone commented on your post '{post.Title}'."
                });
            }

            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                _context.Entry(comment).Reference(c => c.User).Load();
                return Json(new { 
                    success = true, 
                    id = comment.Id,
                    content = comment.Content,
                    author = comment.User.FullName,
                    date = comment.CreatedAt.ToString("MMM dd HH:mm"),
                    parentId = comment.ParentId
                });
            }

            return RedirectToAction("Details", new { id = model.PostId });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            
            var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);
            bool isLiked = false;

            if (existingLike != null)
            {
                _context.Likes.Remove(existingLike);
            }
            else
            {
                _context.Likes.Add(new Like { UserId = userId, PostId = postId });
                isLiked = true;
                
                var post = await _context.Posts.FindAsync(postId);
                if (post.UserId != userId)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = post.UserId,
                        Content = $"Someone liked your post '{post.Title}'."
                    });
                }
            }

            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var likeCount = await _context.Likes.CountAsync(l => l.PostId == postId);
                return Json(new { success = true, liked = isLiked, likeCount = likeCount });
            }

            return RedirectToAction("Details", new { id = postId });
        }
        
        [HttpPost]
        public async Task<IActionResult> Report(int postId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) 
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Reason is required." });
                return RedirectToAction("Details", new { id = postId });
            }
            
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            _context.Reports.Add(new Report
            {
                PostId = postId,
                UserId = userId,
                Reason = reason
            });
            await _context.SaveChangesAsync();
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = "Report submitted successfully." });
                
            return RedirectToAction("Details", new { id = postId, reportSuccess = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
            
            if (comment != null)
            {
                var postId = comment.PostId;
                // Deleting parent comment should also delete replies. We handle cascade via code since DB restrict.
                var replies = await _context.Comments.Where(c => c.ParentId == comment.Id).ToListAsync();
                _context.Comments.RemoveRange(replies);
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });
                    
                return RedirectToAction("Details", new { id = postId });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Comment not found or unauthorized." });

            return RedirectToAction("Index", "Home");
        }
    }
}
