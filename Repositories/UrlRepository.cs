


public class UrlRepository
{
    private readonly string _connectionString;



    public UrlRepository(string connectionString)
    {
        _connectionString = connectionString;
    }
    public int GetTotalShortUrlsCount(string? defaultConnection)
    {
        using var connection = new Npgsql.NpgsqlConnection(defaultConnection);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM short_urls";

        var result = command.ExecuteScalar();

        return result.result;
    }
}