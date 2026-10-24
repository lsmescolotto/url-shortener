var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(defaultConnection))
{
  throw new InvalidOperationException("DefaultConnection string is not configured.");
}

// using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
// connection.Open();

var app = builder.Build();

var urlsList = new Dictionary<string, ShortUrl>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/db-test", () => {
  using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
  connection.Open();
  var command = connection.CreateCommand();
  command.CommandText = "SELECT COUNT(*) FROM short_urls";
  var result = command.ExecuteScalar();

  return Results.Ok(new { message = "Database connection successful.", result });
}


);

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

app.MapGet("/urls/{code}", async (string code) => {
  //esse aqui é pra retornar só a url
  using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
  await connection.OpenAsync();

  var command = connection.CreateCommand();

  command.CommandText = "SELECT original_url, expires_at, click_count FROM short_urls WHERE short_code = @short_code";
  command.Parameters.AddWithValue("@short_code", code);

  var result = await command.ExecuteReaderAsync();
  bool urlFound = await result.ReadAsync();
  string url = result.GetString(result.GetOrdinal("original_url"));

  if (!urlFound)
  {
    return Results.NotFound(new { message = "Short code not found." });
  }
  var expiresAt = result.GetDateTime(result.GetOrdinal("expires_at"));
  if (expiresAt <= DateTime.UtcNow)
  {
    return Results.NotFound(new { message = "Short code has expired. To access this short code statistics, consult the '/urls/{code}/stats' get endpoint." });
  }
  int clickCount = result.GetInt32(result.GetOrdinal("click_count"));

  command.CommandText = "UPDATE short_urls SET click_count = click_count +1 WHERE short_code = @short_code";
  await command.ExecuteNonQueryAsync();
  //TODO: click count and redirect



  return Results.Redirect(url);
}
);

app.MapGet("/urls/{code}/stats", async (string code) => {
  //esse aqui é pra retornar as estatisticas
  using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
  await connection.OpenAsync();

  var command = connection.CreateCommand();

  command.CommandText = "SELECT * FROM short_urls WHERE short_code = @short_code";
  command.Parameters.AddWithValue("@short_code", code);

  var result = await command.ExecuteReaderAsync();
  bool urlFound = await result.ReadAsync();

  if (!urlFound)
  {
    return Results.NotFound(new { message = "Short code not found. No statistics available." });
  }

  string shortCode = result.GetString(result.GetOrdinal("short_code"));
  string originalUrl = result.GetString(result.GetOrdinal("original_url"));
  int expirationInMinutes = result.GetInt32(result.GetOrdinal("expiration_in_minutes"));
  int clickCount = result.GetInt32(result.GetOrdinal("click_count"));
  var id = result.GetGuid(result.GetOrdinal("id"));
  var createdAt = result.GetDateTime(result.GetOrdinal("created_at"));
  var expiresAt = result.GetDateTime(result.GetOrdinal("expires_at"));

  var shortUrlStats = new ShortUrlStatsResponse(id, createdAt, shortCode, originalUrl, expirationInMinutes, clickCount, expiresAt);
  return Results.Ok(shortUrlStats);
}
);

app.MapDelete("/urls/{code}", async (string code) => {
  using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
  await connection.OpenAsync();

  var command = connection.CreateCommand();

  command.CommandText = "DELETE FROM short_urls WHERE short_code = @short_code";
  command.Parameters.AddWithValue("@short_code", code);

  int result = await command.ExecuteNonQueryAsync();

  if (result == 0)
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

