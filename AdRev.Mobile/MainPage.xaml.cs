using BarcodeScanner.Mobile;
using AdRev.Mobile.Services;
using AdRev.Domain.MobileSync.Models;

namespace AdRev.Mobile;

public partial class MainPage : ContentPage
{
    private bool _isProcessing = false;
    private readonly ApiClient _apiClient;
    private readonly DatabaseService _dbService;

    public MainPage(ApiClient apiClient, DatabaseService dbService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _dbService = dbService;
        
        // Ensure camera starts if visible
        BarcodeScanner.Mobile.Methods.AskForRequiredPermission();
    }

    private void OnConnectClicked(object sender, EventArgs e)
    {
        string code = CodeEntry.Text;
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
        {
            DisplayAlert("Erreur", "Veuillez entrer un code valide à 6 chiffres", "OK");
            return;
        }
        
        if (string.IsNullOrEmpty(_apiClient.BaseUrl))
        {
             // DisplayAlert("Info", "Pour l'entrée manuelle, veuillez d'abord scanner le QR code pour configurer l'adresse du serveur.", "OK");
        }

        ProcessPairing(code);
    }
    
    // Property to expose BaseUrl for testing if needed
    public string ServerUrl { get; set; } = "http://10.0.2.2:5000"; // Default Android emulator localhost alias

    private void OnDetected(object sender, OnDetectedEventArg e)
    {
        if (_isProcessing) return;

        var first = e.BarcodeResults?.FirstOrDefault();
        if (first is null) return;

        Dispatcher.Dispatch(() =>
        {
            // StatusLabel.Text = "Code détecté !";
            _isProcessing = true;
            Camera.IsScanning = false; // Pause scanning

            // Process QR content
            // Expected format: {"code":"123456","server":"http://192.168.1.x:5000"}
            ProcessQrContent(first.DisplayValue);
        });
    }

    private  void ProcessQrContent(string content)
    {
        try 
        {
            // Simple JSON parsing (we have NewtonSoft)
            dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
            string code = data.code;
            string server = data.server;

            StatusLabel.Text = $"Connexion à {server}...";
            
            _apiClient.SetBaseUrl(server);
            CodeEntry.Text = code;
            
            ProcessPairing(code);
        }
        catch 
        {
             StatusLabel.Text = "QR Code invalide";
             _isProcessing = false;
             Camera.IsScanning = true;
        }
    }

    private async void ProcessPairing(string code)
    {
        StatusLabel.Text = $"Tentative de jumelage avec le code {code}...";
        
        try
        {
            var deviceName = DeviceInfo.Name;
            var deviceId = DeviceInfo.Idiom.ToString() + "_" + Guid.NewGuid().ToString().Substring(0, 8); // Simple ID generation

            // Allow manual "Simulated" pairing to see the app
            bool simulated = false; // Disable for real test
            if (simulated) 
            {
               // ... (Simulation code)
            }

            // Normal flow
            var result = await _apiClient.ValidatePairingCodeAsync(code, deviceName, deviceId);
            
            if (result.Success)
            {
                StatusLabel.Text = "Jumelage réussi !";
                await DisplayAlert("Succès", "Appareil jumelé avec succès !", "OK");
                
                // Save pairing info
                var pairedDevice = new PairedDevice
                {
                    DeviceId = deviceId,
                    DeviceName = deviceName,
                    PairedAt = DateTime.Now,
                    IsActive = true
                };
                
                await _dbService.SavePairedDeviceAsync(pairedDevice);
                _apiClient.SetToken(result.Token);

                // App is now "Premium/Full" because it's linked to a Desktop instance
                Preferences.Default.Set("IsUniversalLicense", true);

                // Navigate to forms
                 await Shell.Current.GoToAsync("//FormsPage");
            }
            else
            {
                StatusLabel.Text = $"Échec: {result.Message}";
                await DisplayAlert("Erreur", $"Jumelage échoué: {result.Message}", "OK");
                _isProcessing = false;
                Camera.IsScanning = true;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Erreur: {ex.Message}";
            _isProcessing = false;
            Camera.IsScanning = true;
        }
    }
}
