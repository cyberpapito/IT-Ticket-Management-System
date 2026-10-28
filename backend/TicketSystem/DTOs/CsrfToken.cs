namespace TicketSystem.DTOs
{
    // Sent back in the X-XSRF-TOKEN header on every POST, PUT and DELETE.
    public record CsrfToken(string Token);
}
