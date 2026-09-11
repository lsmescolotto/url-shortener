var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

var urlsList = new Dictionary<string, ShortUrl>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/urls", (CreateUrlRequest request) => {
  if (!string.IsNullOrWhiteSpace(request.ShortCode) && urlsList.ContainsKey(request.ShortCode))
  {
    return Results.Conflict(new { message = "Short code already exists." });
  }

  try
  {
    var shortUrl = new ShortUrl(
      request.Url,
      request.ShortCode,
      request.ExpirationInMinutes ?? 60
    );


    while (urlsList.ContainsKey(shortUrl.ShortCode))
    {
      shortUrl.GenerateNewShortCode();
    }

    urlsList.TryAdd(shortUrl.ShortCode, shortUrl);

    return Results.Created($"/urls/{shortUrl.ShortCode}", shortUrl);
  }
  catch (System.ArgumentException ex)
  {
    return Results.BadRequest(new { message = ex.Message });
  }
});

app.MapGet("/urls/{code}", (string code) => {
  //esse aqui é pra retornar só a url

  if (!urlsList.TryGetValue(code, out ShortUrl? shortUrl))
  {
    return Results.NotFound(new { message = "Short code not found." });
  }

  if (shortUrl.ExpiresAt <= DateTime.UtcNow)
  {
    return Results.NotFound(new { message = "Short code has expired. To access this short code statistics, consult the '/urls/{code}/stats' get endpoint." });
  }
  shortUrl.IncrementClickCount();

  return Results.Redirect(shortUrl.OriginalUrl);
}
);

app.MapGet("/urls/{code}/stats", (string code) => {
  //esse aqui é pra retornar as estatisticas

  if (!urlsList.TryGetValue(code, out ShortUrl? shortUrl))
  {
    return Results.NotFound(new { message = "Short code not found. No statistics available." });
  }
  return Results.Ok(shortUrl);
}
);

app.MapDelete("/urls/{code}", (string code) => {
  if (!urlsList.Remove(code))
  {
    return Results.NotFound(new { message = "Short code not found. Not able to delete." });
  }
  return Results.NoContent();
}
);

app.Run();

record CreateUrlRequest(
  string Url,
  string? ShortCode,
  int? ExpirationInMinutes
);

record UrlResponse(
  string Id,
  string OriginalUrl,
  string ShortCode,
  DateTime CreatedAt,
  DateTime ExpiresAt,
  int ExpirationInMinutes,
  int ClickCount
);

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

