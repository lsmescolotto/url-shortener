using UrlShortener.Models;
using UrlShortener.DTOs;

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
    Console.WriteLine(result);

    return Results.Ok(new { message = "Database connection successful.", result });
}
);

app.MapPost("/urls", async (CreateUrlRequest request) => {
  try
  {
    int maxAttemptsToGenerateShortCode = 5;
    var shortUrl = new ShortUrl(
      request.Url,
      request.ShortCode,
      request.ExpirationInMinutes ?? 60
    );


    await using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();

    command.CommandText = "INSERT INTO short_urls (id, original_url, short_code, created_at, expires_at, expiration_in_minutes, click_count) VALUES (@id, @originalUrl, @shortCode, @createdAt, @expiresAt, @expirationInMinutes, @clickCount)";

    command.Parameters.AddWithValue("@id", shortUrl.Id);
    command.Parameters.AddWithValue("@originalUrl", shortUrl.OriginalUrl);
    command.Parameters.AddWithValue("@shortCode", shortUrl.ShortCode);
    command.Parameters.AddWithValue("@createdAt", shortUrl.CreatedAt);
    command.Parameters.AddWithValue("@expiresAt", shortUrl.ExpiresAt);
    command.Parameters.AddWithValue("@expirationInMinutes", shortUrl.ExpirationInMinutes);
    command.Parameters.AddWithValue("@clickCount", shortUrl.ClickCount);

    for (int i = 1; i <= maxAttemptsToGenerateShortCode; i++)
    {
      try
      {
        await command.ExecuteNonQueryAsync();
        return Results.Created($"/urls/{shortUrl.ShortCode}", shortUrl);
      }
      catch (Npgsql.PostgresException ex)
      {
        if (ex.SqlState == "23505" && ex.ConstraintName == "short_urls_short_code_key")
        {
          if (!string.IsNullOrWhiteSpace(request.ShortCode))
          {
            return Results.Conflict(new { message = "Short code already exists." });
          }
          if (i < maxAttemptsToGenerateShortCode)
          {
            shortUrl.GenerateNewShortCode();
            command.Parameters["@shortCode"].Value = shortUrl.ShortCode;
            continue;
          }
        }
        throw;
      }
    }
    return Results.InternalServerError(new { message = "It was not possible to complete your request." });
  }
  catch (System.ArgumentException ex)
  {
    return Results.BadRequest(new { message = ex.Message });
  }
  catch (Npgsql.PostgresException ex)
  {
        Console.Error.Write(ex);
        return Results.InternalServerError(new { message = "It was not possible to complete your request." });
    }
});

app.MapGet("/urls/{code}", async (string code) => {
  //redirects to the original url
  await using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
  await connection.OpenAsync();

  await using var command = connection.CreateCommand();

  command.CommandText = "SELECT original_url, expires_at FROM short_urls WHERE short_code = @short_code";
  command.Parameters.AddWithValue("@short_code", code);

  string url;

  {
    await using var result = await command.ExecuteReaderAsync();
    bool urlFound = await result.ReadAsync();

    if (!urlFound)
    {
      return Results.NotFound(new { message = "Short code not found." });
    }

    var expiresAt = result.GetDateTime(result.GetOrdinal("expires_at"));

    if (expiresAt <= DateTime.UtcNow)
    {
      return Results.NotFound(new { message = "Short code has expired. To access this short code statistics, consult the '/urls/{code}/stats' get endpoint." });
    }

    url = result.GetString(result.GetOrdinal("original_url"));
  }

  command.CommandText = "UPDATE short_urls SET click_count = click_count +1 WHERE short_code = @short_code";
  await command.ExecuteNonQueryAsync();

  return Results.Redirect(url);
}
);

app.MapGet("/urls/{code}/stats", async (string code) => {
  //returns short code statistics
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

