namespace WebApi.Models
{
    public class ConfirmEmailModel
    {
        public string Email { get; set; } = null!;

        public string Token { get; set; } = null!;
    }
}
