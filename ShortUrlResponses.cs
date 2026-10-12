public record ShortUrlStatsResponse
(
  Guid Id,
  DateTime CreatedAt,
  string ShortCode,
  string OriginalUrl,
  int ExpirationInMinutes,
  int ClickCount,
  DateTime ExpiresAt
);