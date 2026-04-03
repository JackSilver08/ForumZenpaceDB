namespace ForumZenpace.Hubs
{
    public static class PostChannel
    {
        public static string GetPostGroupName(int postId)
        {
            return $"post:{postId}";
        }
    }
}
