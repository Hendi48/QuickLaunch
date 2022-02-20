namespace QuickLaunch;

public class Account
{
    public string Email { get; set; }
    public string Cookie { get; set; } = "";
    public DateTime CookieExpiration { get; set; }

    public Account(string email)
    {
        Email = email;
    }
}
