namespace TicketSystem.DTOs
{
    public class AddCommentRequest
    {
        public Guid AuthorUserId { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
