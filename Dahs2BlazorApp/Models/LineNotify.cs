using System.Text;
using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Models;

public interface ILineNotify
{
    Task Notify(string message);
    Task Notify(string token, string message);
}

public class LineNotify : ILineNotify
{
    private readonly ILogger<LineNotify> _logger;
    private readonly HttpClient _httpClient;
    private readonly SysConfigIo _sysConfigIo;

    public LineNotify(ILogger<LineNotify> logger, HttpClient httpClient, SysConfigIo sysConfigIo)
    {
        _logger = logger;
        _httpClient = httpClient;
        _sysConfigIo = sysConfigIo;
    }

    public async Task Notify(string token, string message)
    {
        try
        {
            var url = "https://notify-api.line.me/api/notify";
            var content = new StringContent($"message={message}", Encoding.UTF8, "application/x-www-form-urlencoded");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "LineNotify");
            throw;
        }
    }

    public async Task Notify(string message)
    {
        var token = await _sysConfigIo.GetLineToken();
        await Notify(token, message);
    }
}