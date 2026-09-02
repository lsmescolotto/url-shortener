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
  Console.WriteLine($"Received request to shorten URL: {request.Url}");
  if (Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
  {
    if (request.ExpirationInMinutes > 0 || !request.ExpirationInMinutes.HasValue)
    {
      string code;
      if (string.IsNullOrEmpty(request.ShortCode))
      {
        code = Guid.NewGuid().ToString().Substring(0, 7);
      }
      else if (request.ShortCode.Length > 3 && request.ShortCode.Length <= 20)
      {
        code = request.ShortCode;
      }
      else
      {
        return Results.BadRequest(new { message = "ShortCode must be between 4 and 20 characters long." });
      }


      Console.WriteLine($"URI: {uriResult}");
      string id = Guid.NewGuid().ToString();

      while (urlsList.ContainsKey(code))
      {
        if (!string.IsNullOrEmpty(request.ShortCode))
        {
          return Results.Conflict(new { message = "Short code already exists." });
        }
        else
        {
          code = Guid.NewGuid().ToString().Substring(0, 7);
        }
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
    }

    else
    {
      return Results.BadRequest(new { message = "ExpirationInMinutes must be greater than 0." });
    }
  }
  else
  {
    return Results.BadRequest(new { message = "Invalid URL format." });
  }
});

app.MapGet("/urls/{code}", (string code) => {
  var url = new UrlResponse(
    Guid.NewGuid().ToString(),
    "https://example.com",
    code,
    DateTime.UtcNow,
    DateTime.UtcNow.AddMinutes(60),
    60,
    0
  );
  return url;
}
);

app.MapGet("/urls/{code}/stats", (string code) => {

}
);

app.MapDelete("/urls/{code}", (string code) => {

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
  DateTime EspiresAt,
  int ExpirationInMinutes,
  int ClickCount
);