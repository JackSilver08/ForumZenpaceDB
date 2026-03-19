using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ForumZenpace.Models;

namespace ForumZenpace.Controllers
{
    [Authorize]
    public class PostController : Controller
    {
        private static readonly HashSet<string> AllowedPostImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };
        private static readonly Regex MarkdownImageRegex = new(@"!\[[^\]]*\]\((?<url>[^)]+)\)", RegexOptions.Compiled);
        private const long MaxPostImageSizeBytes = 10 * 1024 * 1024;
        private readonly ForumDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PostController(ForumDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = new SelectList(await _context.Categories.ToListAsync(), "Id", "Name");
            return View(new PostViewModel
            {
                DraftToken = CreateDraftToken()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostViewModel model)
        {
            model.DraftToken = SanitizeDraftToken(model.DraftToken);
            if (string.IsNullOrWhiteSpace(model.DraftToken))
            {
                model.DraftToken = CreateDraftToken();
            }

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
            await AttachDraftImagesToPostAsync(post.Id, userId, model.DraftToken, model.Content);

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
                PostId = post.Id,
                DraftToken = CreateDraftToken(),
                Title = post.Title,
                Content = post.Content,
                CategoryId = post.CategoryId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PostViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

            if (post == null) return NotFound();

            model.PostId = id;
            model.DraftToken = SanitizeDraftToken(model.DraftToken);
            if (string.IsNullOrWhiteSpace(model.DraftToken))
            {
                model.DraftToken = CreateDraftToken();
            }

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
            await AttachDraftImagesToPostAsync(post.Id, userId, model.DraftToken, model.Content);
            await SyncPostImagesAsync(post.Id, userId, model.Content);

            return RedirectToAction("Details", new { id = post.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(PostImageUploadViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var validationError = ValidatePostImage(model.Image);
            if (validationError != null)
            {
                return Json(new { success = false, message = validationError });
            }

            model.DraftToken = SanitizeDraftToken(model.DraftToken);

            if (model.PostId.HasValue)
            {
                var canEditPost = await _context.Posts.AnyAsync(p => p.Id == model.PostId.Value && p.UserId == userId);
                if (!canEditPost)
                {
                    return Json(new { success = false, message = "Ban khong co quyen chen anh vao bai viet nay." });
                }
            }
            else if (string.IsNullOrWhiteSpace(model.DraftToken))
            {
                return Json(new { success = false, message = "Khong tim thay phien soan bai hop le." });
            }

            var saveAsDraftImage = !string.IsNullOrWhiteSpace(model.DraftToken);

            var (fileName, imageUrl) = await SavePostImageAsync(model.Image, userId);
            var originalFileName = Path.GetFileName(model.Image.FileName);

            var postImage = new PostImage
            {
                UserId = userId,
                PostId = saveAsDraftImage ? null : model.PostId,
                DraftToken = saveAsDraftImage ? model.DraftToken : null,
                FileName = fileName,
                OriginalFileName = originalFileName,
                ContentType = model.Image.ContentType ?? "application/octet-stream",
                ImageUrl = imageUrl
            };

            _context.PostImages.Add(postImage);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                imageUrl,
                markdown = CreateImageMarkdown(postImage)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

            if (post != null)
            {
                var commentIds = await _context.Comments
                    .Where(c => c.PostId == id)
                    .Select(c => c.Id)
                    .ToListAsync();
                var comments = await _context.Comments.Where(c => c.PostId == id).ToListAsync();
                var commentLikes = await _context.CommentLikes.Where(cl => commentIds.Contains(cl.CommentId)).ToListAsync();
                var likes = await _context.Likes.Where(l => l.PostId == id).ToListAsync();
                var postImages = await _context.PostImages.Where(pi => pi.PostId == id).ToListAsync();
                var reports = await _context.Reports.Where(r => r.PostId == id).ToListAsync();

                _context.CommentLikes.RemoveRange(commentLikes);
                _context.Comments.RemoveRange(comments);
                _context.Likes.RemoveRange(likes);
                _context.PostImages.RemoveRange(postImages);
                _context.Reports.RemoveRange(reports);
                DeletePostImageFiles(postImages);

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
                .Include(p => p.Comments)
                    .ThenInclude(c => c.CommentLikes)
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
            
            var topLevelComments = BuildCommentTree(post.Comments);
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
        public async Task<IActionResult> ToggleCommentLike(int commentId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Comment not found." });

                return RedirectToAction("Index", "Home");
            }

            var existingLike = await _context.CommentLikes
                .FirstOrDefaultAsync(cl => cl.UserId == userId && cl.CommentId == commentId);
            bool isLiked = false;

            if (existingLike != null)
            {
                _context.CommentLikes.Remove(existingLike);
            }
            else
            {
                _context.CommentLikes.Add(new CommentLike { UserId = userId, CommentId = commentId });
                isLiked = true;

                if (comment.UserId != userId)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = comment.UserId,
                        Content = "Someone liked your comment."
                    });
                }
            }

            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var likeCount = await _context.CommentLikes.CountAsync(cl => cl.CommentId == commentId);
                return Json(new { success = true, liked = isLiked, likeCount = likeCount, commentId = commentId });
            }

            return RedirectToAction("Details", new { id = comment.PostId });
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
                var commentBranch = await GetCommentBranchAsync(comment);
                var commentIds = commentBranch
                    .Select(c => c.Id)
                    .Append(comment.Id)
                    .ToList();
                var commentLikes = await _context.CommentLikes
                    .Where(cl => commentIds.Contains(cl.CommentId))
                    .ToListAsync();

                _context.CommentLikes.RemoveRange(commentLikes);
                _context.Comments.RemoveRange(commentBranch);
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

        private static List<Comment> BuildCommentTree(IEnumerable<Comment> comments)
        {
            var orderedComments = comments
                .OrderBy(c => c.CreatedAt)
                .ToList();

            var commentLookup = orderedComments.ToDictionary(c => c.Id);
            foreach (var comment in orderedComments)
            {
                comment.Replies = new List<Comment>();
            }

            var topLevelComments = new List<Comment>();
            foreach (var comment in orderedComments)
            {
                if (comment.ParentId.HasValue && commentLookup.TryGetValue(comment.ParentId.Value, out var parentComment))
                {
                    parentComment.Replies.Add(comment);
                    continue;
                }

                topLevelComments.Add(comment);
            }

            return topLevelComments;
        }

        private async Task<List<Comment>> GetCommentBranchAsync(Comment rootComment)
        {
            var postComments = await _context.Comments
                .Where(c => c.PostId == rootComment.PostId)
                .ToListAsync();

            var childrenLookup = postComments
                .Where(c => c.ParentId.HasValue)
                .ToLookup(c => c.ParentId!.Value);

            var idsToDelete = new HashSet<int>();
            var pendingCommentIds = new Queue<int>();
            pendingCommentIds.Enqueue(rootComment.Id);

            while (pendingCommentIds.Count > 0)
            {
                var currentId = pendingCommentIds.Dequeue();
                foreach (var childComment in childrenLookup[currentId])
                {
                    if (idsToDelete.Add(childComment.Id))
                    {
                        pendingCommentIds.Enqueue(childComment.Id);
                    }
                }
            }

            return postComments
                .Where(c => idsToDelete.Contains(c.Id))
                .ToList();
        }

        private static string? ValidatePostImage(IFormFile? image)
        {
            if (image is null || image.Length == 0)
            {
                return "Anh tai len khong hop le.";
            }

            if (image.Length > MaxPostImageSizeBytes)
            {
                return "Anh trong bai viet chi duoc toi da 10MB.";
            }

            var extension = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedPostImageExtensions.Contains(extension))
            {
                return "Chi chap nhan file JPG, PNG, GIF hoac WEBP.";
            }

            return null;
        }

        private async Task<(string FileName, string ImageUrl)> SavePostImageAsync(IFormFile image, int userId)
        {
            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var postImageDirectory = Path.Combine(webRootPath, "uploads", "posts");
            Directory.CreateDirectory(postImageDirectory);

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            var fileName = $"post-{userId}-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(postImageDirectory, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);

            return (fileName, $"/uploads/posts/{fileName}");
        }

        private async Task AttachDraftImagesToPostAsync(int postId, int userId, string draftToken, string content)
        {
            if (string.IsNullOrWhiteSpace(draftToken))
            {
                return;
            }

            var normalizedDraftToken = SanitizeDraftToken(draftToken);
            var referencedUrls = ExtractImageUrls(content);
            var draftImages = await _context.PostImages
                .Where(pi => pi.UserId == userId && pi.PostId == null && pi.DraftToken == normalizedDraftToken)
                .ToListAsync();

            var orphanedDraftImages = draftImages
                .Where(pi => !referencedUrls.Contains(pi.ImageUrl))
                .ToList();

            if (orphanedDraftImages.Count > 0)
            {
                _context.PostImages.RemoveRange(orphanedDraftImages);
                DeletePostImageFiles(orphanedDraftImages);
            }

            foreach (var image in draftImages.Except(orphanedDraftImages))
            {
                image.PostId = postId;
                image.DraftToken = null;
            }

            await _context.SaveChangesAsync();
        }

        private async Task SyncPostImagesAsync(int postId, int userId, string content)
        {
            var referencedUrls = ExtractImageUrls(content);
            var postImages = await _context.PostImages
                .Where(pi => pi.PostId == postId && pi.UserId == userId)
                .ToListAsync();

            var removedImages = postImages
                .Where(pi => !referencedUrls.Contains(pi.ImageUrl))
                .ToList();

            if (removedImages.Count == 0)
            {
                return;
            }

            _context.PostImages.RemoveRange(removedImages);
            DeletePostImageFiles(removedImages);
            await _context.SaveChangesAsync();
        }

        private void DeletePostImageFiles(IEnumerable<PostImage> postImages)
        {
            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var postImageDirectory = Path.Combine(webRootPath, "uploads", "posts");

            foreach (var postImage in postImages)
            {
                if (string.IsNullOrWhiteSpace(postImage.FileName))
                {
                    continue;
                }

                var filePath = Path.Combine(postImageDirectory, postImage.FileName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }

        private static string CreateDraftToken()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string SanitizeDraftToken(string? draftToken)
        {
            if (string.IsNullOrWhiteSpace(draftToken))
            {
                return string.Empty;
            }

            var trimmed = draftToken.Trim();
            return trimmed.Length <= 64 ? trimmed : trimmed[..64];
        }

        private static string CreateImageMarkdown(PostImage postImage)
        {
            var altText = Path.GetFileNameWithoutExtension(postImage.OriginalFileName)
                .Replace("-", " ", StringComparison.Ordinal)
                .Trim();

            if (string.IsNullOrWhiteSpace(altText))
            {
                altText = "Hinh anh bai viet";
            }

            return $"![{altText}]({postImage.ImageUrl})";
        }

        private static HashSet<string> ExtractImageUrls(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return MarkdownImageRegex.Matches(content)
                .Select(match => match.Groups["url"].Value.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
