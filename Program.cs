var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

var urlsList = new Dictionary<string, UrlResponse>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/urls", (CreateUrlRequest request) => {
  if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uriResult) ||
  !(uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
  {
    return Results.BadRequest(new { message = "Invalid URL format." });
  }

  if (request.ExpirationInMinutes <= 0 && request.ExpirationInMinutes.HasValue)
  {
    return Results.BadRequest(new { message = "ExpirationInMinutes must be greater than 0." });
  }

  string code;

  if (request.ShortCode != null)
  {
    if (string.IsNullOrWhiteSpace(request.ShortCode))
    {
      return Results.BadRequest(new { message = "ShortCode must not be empty or contain only whitespace." });
    }
    else if (request.ShortCode.Length < 4 || request.ShortCode.Length > 20)
    {
      return Results.BadRequest(new { message = "ShortCode must be between 4 and 20 characters long." });
    }
    code = request.ShortCode;

  }
  else
  {
    code = Guid.NewGuid().ToString().Substring(0, 7);
  }

  Console.WriteLine($"URI: {uriResult}");
  string id = Guid.NewGuid().ToString();

  while (urlsList.ContainsKey(code))
  {
    if (!string.IsNullOrEmpty(request.ShortCode))
    {
      return Results.Conflict(new { message = "Short code already exists." });
    }
    code = Guid.NewGuid().ToString().Substring(0, 7);
  }

  var urlResponse = new UrlResponse(
    id,
    uriResult.ToString(),
    code,
    DateTime.UtcNow,
    DateTime.UtcNow.AddMinutes(request.ExpirationInMinutes ?? 60),
    request.ExpirationInMinutes ?? 60,
    0
  );

  urlsList.TryAdd(code, urlResponse);

  return Results.Created($"/urls/{code}", urlResponse);
});

app.MapGet("/urls/{code}", (string code) => {
  //esse aqui é pra retornar só a url
  bool found = urlsList.TryGetValue(code, out UrlResponse? urlResponse);

  if (!found)
  {
    return Results.NotFound(new { message = "Short code not found." });
  }

  if (urlResponse!.ExpiresAt <= DateTime.UtcNow)
  {
    return Results.NotFound(new { message = "Short code has expired. To access this short code statistics, consult the '/urls/{code}/stats' get endpoint." });
  }
  urlsList[code] = urlResponse! with { ClickCount = urlResponse.ClickCount + 1 };

  return Results.Redirect(urlResponse!.OriginalUrl);
}
);

app.MapGet("/urls/{code}/stats", (string code) => {
  //esse aqui é pra retornar as estatisticas
  bool found = urlsList.TryGetValue(code, out UrlResponse? urlResponse);

  if (!found)
  {
    return Results.NotFound(new { message = "Short code not found. No statistics available." });
  }
  return Results.Ok(urlResponse);
}
);

app.MapDelete("/urls/{code}", (string code) => {
  bool removed = urlsList.Remove(code);
  if (!removed)
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