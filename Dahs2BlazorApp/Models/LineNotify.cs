using System.Text;
using System.Text.Json;
using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Models;

public interface ILineNotify
{
    Task Notify(string message);
    string GetToken();
}

public class LineNotify : ILineNotify
{
    private readonly ILogger<LineNotify> _logger;
    private readonly HttpClient _httpClient;

    public class LineConfig
    {
        public string ChannelToken { get; set; } = string.Empty;
        public string[] GroupIDs { get; set; } = Array.Empty<string>();
    }

    public readonly LineConfig Config = new();

    public LineNotify(ILogger<LineNotify> logger, IConfiguration configuration, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        configuration.GetSection("Line").Bind(Config);
    }

    public async Task Notify(string message)
    {
        try
        {
            await BroadcastLine(Config.ChannelToken, message);
            foreach (var groupId in Config.GroupIDs)
            {
                await PushMessage(Config.ChannelToken, groupId, message);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "LineNotify");
            throw;
        }
    }

    public string GetToken()
    {
        return Config.ChannelToken;
    }
    
    public Task BroadcastLine(string token, string message)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var content = new
        {
            messages = new[]
            {
                new
                {
                    type = "text",
                    text = message
                }
            }
        };

        return _httpClient.PostAsync("https://api.line.me/v2/bot/message/broadcast",
            new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"));
    }

    public Task PushMessage(string token, string groupId, string message)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var content = new
        {
            to = groupId,
            messages = new[]
            {
                new
                {
                    type = "text",
                    text = message
                }
            }
        };

        return _httpClient.PostAsync("https://api.line.me/v2/bot/message/push",
            new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json"));
    }
}