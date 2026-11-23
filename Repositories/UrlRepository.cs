public class UrlRepository
{
    private readonly string _connectionString;

    public UrlRepository(string connectionString)
    {
        _connectionString = connectionString;
    }
    public int GetTotalShortUrlsCount() =>  0 ;
}