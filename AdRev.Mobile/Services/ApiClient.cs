using System.Net.Http.Json;
using AdRev.Domain.MobileSync.Models;
using Newtonsoft.Json;

namespace AdRev.Mobile.Services;

public class ApiClient
{
    private readonly HttpClient _client;
    private string _baseUrl = "";
    private string _token = "";
    private string _encryptionKey = "";

    public string BaseUrl => _baseUrl;

    public ApiClient()
    {
        _client = new HttpClient();
        _client.Timeout = TimeSpan.FromSeconds(10);
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        _baseUrl = Preferences.Get("api_base_url", "");
        _token = Preferences.Get("api_token", "");
        _encryptionKey = Preferences.Get("api_key", ""); // Security note: Preferences is not encrypted on all platforms, use SecureStorage in production.
        
        if (!string.IsNullOrEmpty(_token))
        {
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public void SetBaseUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
        Preferences.Set("api_base_url", _baseUrl);
    }

    public void SetToken(string token)
    {
        _token = token;
        Preferences.Set("api_token", _token);
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<PairingResponse> ValidatePairingCodeAsync(string code, string deviceName, string deviceId)
    {
        try
        {
            if (string.IsNullOrEmpty(_baseUrl)) throw new InvalidOperationException("Base URL not set");

            var request = new ValidatePairingRequest
            {
                PairingCode = code,
                DeviceName = deviceName,
                DeviceId = deviceId
            };

            var response = await _client.PostAsJsonAsync($"{_baseUrl}/api/pairing/validate", request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PairingResponse>(content) ?? new PairingResponse { Success = false, Message = "Invalid response" };
                
                if (result.Success && !string.IsNullOrEmpty(result.EncryptionKey))
                {
                    _encryptionKey = result.EncryptionKey;
                    Preferences.Set("api_key", _encryptionKey);
                }
                
                return result;
            }
            
            return new PairingResponse { Success = false, Message = $"Error: {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new PairingResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<List<MobileQuestionnaire>> GetQuestionnairesAsync()
    {
        try
        {
             if (string.IsNullOrEmpty(_baseUrl)) return new List<MobileQuestionnaire>();
            
            var response = await _client.GetAsync($"{_baseUrl}/api/sync/questionnaires");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<MobileQuestionnaire>>(content) ?? new List<MobileQuestionnaire>();
            }
            return new List<MobileQuestionnaire>();
        }
        catch { return new List<MobileQuestionnaire>(); }
    }

    public async Task<bool> UploadDataAsync(List<CollectedDataRecord> records)
    {
        try
        {
            if (string.IsNullOrEmpty(_baseUrl)) return false;

            if (!string.IsNullOrEmpty(_encryptionKey))
            {
                // Encrypted Upload
                var json = JsonConvert.SerializeObject(records);
                var encrypted = AdRev.Domain.Utils.SecurityUtils.Encrypt(json, _encryptionKey);
                var payload = new SecurePayload { Content = encrypted };
                
                var response = await _client.PostAsJsonAsync($"{_baseUrl}/api/sync/data", payload);
                return response.IsSuccessStatusCode;
            }
            else
            {
                // Legacy Plain Upload
                var response = await _client.PostAsJsonAsync($"{_baseUrl}/api/sync/data", records);
                return response.IsSuccessStatusCode;
            }
        }
        catch { return false; }
    }
}
