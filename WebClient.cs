using System.Net;
using System.Text;

namespace TPLink.SwitchClient;

public class WebClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly SwitchClientOptions _options;
    private bool _isLoggedIn;

    public WebClient(SwitchClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = _options.Timeout,
            BaseAddress = new Uri(_options.SwitchWebAddress)
        };

        Log($"=== Session Started: {DateTime.Now} ===");
    }

    private void Log(string message)
    {
        _options.Logger?.Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}");
    }

    public async Task<bool> Login()
    {
        try
        {
            Log($"Attempting login to {_options.SwitchWebAddress}");
            Log($"Username: {_options.Username}");

            var requestBody = $"username={_options.Username}&password={_options.Password}&cpassword=&logon=Login";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.PostAsync("/logon.cgi", content);
            var responseText = await response.Content.ReadAsStringAsync();

            Log($"Response Status: {response.StatusCode}");
            Log($"Response Length: {responseText.Length}");
            Log($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
            Log($"Full Response:\n{responseText}");

            // Check if login was successful based on the response content
            // Successful login returns HTML with logonInfo array
            var containsLogonInfo = responseText.Contains("logonInfo");
            var containsError = responseText.Contains("error", StringComparison.OrdinalIgnoreCase);

            Log($"Contains logonInfo: {containsLogonInfo}");
            Log($"Contains error: {containsError}");

            // Login is successful if we get a 200 OK and the response contains logonInfo
            _isLoggedIn = response.IsSuccessStatusCode && containsLogonInfo;

            Log($"Login result: {_isLoggedIn}");

            return _isLoggedIn;
        }
        catch (Exception ex)
        {
            Log($"Login exception: {ex.GetType().Name}");
            Log($"Message: {ex.Message}");
            Log($"Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Log($"Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    public async Task<string> Get(string endpoint)
    {
        if (!_isLoggedIn && !await Login())
        {
            Log($"GET {endpoint} - Not logged in");
            return string.Empty;
        }

        try
        {
            Log($"GET {endpoint}");
            var response = await _httpClient.GetAsync(endpoint);
            var responseText = await response.Content.ReadAsStringAsync();

            Log($"GET {endpoint} - Status: {response.StatusCode}, Length: {responseText.Length}");

            // Log full response for cable diagnostics
            if (endpoint.Contains("cable_diag"))
            {
                Log($"GET {endpoint} - Full Response:\n{responseText}");
            }

            response.EnsureSuccessStatusCode();

            return responseText;
        }
        catch (Exception ex)
        {
            Log($"GET {endpoint} - Error: {ex.Message}");
            return string.Empty;
        }
    }

    public async Task<string> Post(string endpoint, string requestBody)
    {
        if (!_isLoggedIn && !await Login())
        {
            Log($"POST {endpoint} - Not logged in");
            return string.Empty;
        }

        try
        {
            Log($"POST {endpoint} - Body: {requestBody}");
            var content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseText = await response.Content.ReadAsStringAsync();

            Log($"POST {endpoint} - Status: {response.StatusCode}, Length: {responseText.Length}");
            response.EnsureSuccessStatusCode();

            return responseText;
        }
        catch (Exception ex)
        {
            Log($"POST {endpoint} - Error: {ex.Message}");
            return string.Empty;
        }
    }

    public bool IsLoggedIn => _isLoggedIn;

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

