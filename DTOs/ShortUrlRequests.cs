namespace UrlShortener.DTOs;

record CreateUrlRequest(
  string Url,
  string? ShortCode,
  int? ExpirationInMinutes
);