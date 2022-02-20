using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickLaunch;

public class QuickLaunch
{
    private static readonly string ConfigPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "config.json");

    private readonly List<Account> _accounts;
    private readonly HttpClient _httpClient = new();

    public string? GameExecutable { get; set; }

    public IEnumerable<Account> Accounts => _accounts.AsReadOnly();

    public QuickLaunch()
    {
        if (File.Exists(ConfigPath))
        {
            var config = JsonSerializer.Deserialize(File.ReadAllText(ConfigPath), ConfigJsonContext.Default.Config)!;
            _accounts = config.Accounts;
            GameExecutable = config.GameExecutable;
        }
        else
            _accounts = new List<Account>();

        SetClientDefaultHeaders(_httpClient);
    }

    private static void SetClientDefaultHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(NxlUtil.NxlUserAgent);
        client.DefaultRequestHeaders.Referrer = new Uri(NxlUtil.NxlCdnUri);
        client.DefaultRequestHeaders.Add("x-arena-fe-version", NxlUtil.NxlFeVersion);
    }

    public void AddAccount(Account account)
    {
        _accounts.Add(account);
        Save();
    }

    public void RemoveAccount(Account account)
    {
        _accounts.Remove(account);
        Save();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(new Config(_accounts, GameExecutable), ConfigJsonContext.Default.Config);
        File.WriteAllText(ConfigPath, json);
    }

    public async Task<bool> Launch(Account account)
    {
        if (string.IsNullOrEmpty(account.Cookie) || account.CookieExpiration < DateTime.Now)
        {
            return false;
        }

        var passport = await RequestPassport(account);
        var processInfo = new ProcessStartInfo
        {
            FileName = GameExecutable!,
            Arguments = $"-nxl {passport}",
            WorkingDirectory = Path.GetDirectoryName(GameExecutable)
        };
        Process.Start(processInfo);

        return true;
    }

    private async Task<string?> RequestPassport(Account account)
    {
        var sessionId = NxlUtil.GenSessionId();

        // Nexon's cookies last 2 weeks at the time of writing. Refresh our session when ~4 days have passed.
        if (account.CookieExpiration < DateTime.Now.AddDays(10))
        {
            await RefreshSession(account, sessionId);
        }

        var content = new StringContent("{\"productId\":\"10100\"}", Encoding.UTF8, "application/json");
        content.Headers.Add("Cookie", $"NxLSession={account.Cookie}");
        content.Headers.Add("x-nxl-session-id", sessionId);

        var playable = await _httpClient.PostAsync("https://www.nexon.com/api/game-auth2/v1/playable", content);
        if (playable.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Playable status: {playable.StatusCode}");
        }

        var response = await _httpClient.PostAsync("https://www.nexon.com/api/passport/v2/passport", content);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Passport status: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var passportResponse = JsonSerializer.Deserialize(json, PassportJsonContext.Default.PassportResponse);
        if (passportResponse == null || string.IsNullOrEmpty(passportResponse.passport))
        {
            throw new Exception($"Passport response: {json}");
        }
        return passportResponse.passport;
    }

    private async Task RefreshSession(Account account, string sessionId)
    {
        var nexonUri = new Uri("https://www.nexon.com");
        var cookieContainer = new CookieContainer();
        cookieContainer.Add(nexonUri, new Cookie("NxLSession", account.Cookie));

        // We need a unique client here due to the cookie container.
        var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
        using var client = new HttpClient(handler, true);
        SetClientDefaultHeaders(client);

        var deviceId = NxlUtil.GetDeviceId();
        var content = new StringContent($"{{\"deviceId\":\"{deviceId}\"}}", Encoding.UTF8, "application/json");
        content.Headers.Add("x-nxl-session-id", sessionId);
        var response = await client.PostAsync("https://www.nexon.com/api/account/v1/login/launcher/autologin", content);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Autologin failed: {response.StatusCode}");
        }

        var cookie = cookieContainer.GetCookies(nexonUri).Where(c => c.Name == "NxLSession").Last();
        account.Cookie = cookie.Value;
        account.CookieExpiration = cookie.Expires;
        Save();
    }
}

public class Config
{
    public List<Account> Accounts { get; set; }
    public string? GameExecutable { get; set; }

    public Config(List<Account> accounts, string? gameExecutable)
    {
        Accounts = accounts;
        GameExecutable = gameExecutable;
    }
}

[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext { }

public class PassportResponse
{
#pragma warning disable IDE1006 // Naming Styles
    public string? passport { get; set; }
    public bool need_update_session { get; set; } // What's this? Is it ever true?
#pragma warning restore IDE1006 // Naming Styles
}

[JsonSerializable(typeof(PassportResponse))]
internal partial class PassportJsonContext : JsonSerializerContext { }
