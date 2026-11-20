namespace UrlShortener.Models;

public class ShortUrl
{
    public Guid Id { get; private set; }
    public string OriginalUrl { get; private set; }
    public string ShortCode { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public int ExpirationInMinutes { get; private set; }
    public int ClickCount { get; private set; }

    public ShortUrl(string OriginalUrl, string? ShortCode = null, int ExpirationInMinutes = 60)
    {
        //OriginalUrl
        if (!Uri.TryCreate(OriginalUrl, UriKind.Absolute, out Uri? uriResult) ||
        !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Invalid URL format.", nameof(OriginalUrl));
        }
        this.OriginalUrl = uriResult.ToString();

        //ExpirationInMinutes
        if (ExpirationInMinutes <= 0)
        {
            throw new ArgumentException("ExpirationInMinutes must be greater than 0.", nameof(ExpirationInMinutes));
        }
        this.ExpirationInMinutes = ExpirationInMinutes;

        //ShortCode
        if (ShortCode != null)
        {
            if (string.IsNullOrWhiteSpace(ShortCode))
            {
                throw new ArgumentException("ShortCode must not be empty or contain only whitespace.", nameof(ShortCode));
            }
            else if (ShortCode.Length < 4 || ShortCode.Length > 20)
            {
                throw new ArgumentException("ShortCode must be between 4 and 20 characters long.", nameof(ShortCode));
            }
            this.ShortCode = ShortCode;
        }
        else
        {
            this.ShortCode = GenerateShortCode();
        }

        var createdAt = DateTime.UtcNow;
        this.CreatedAt = createdAt;
        this.ExpiresAt = createdAt.AddMinutes(ExpirationInMinutes);
        this.Id = Guid.NewGuid();
        this.ClickCount = 0;
    }
    public void IncrementClickCount()
    {
        this.ClickCount++;
    }

    public void GenerateNewShortCode()
    {
        this.ShortCode = GenerateShortCode();
    }

    private static string GenerateShortCode()
    {
        return Guid.NewGuid().ToString().Substring(0, 7);
    }
}