using AdRev.Mobile.Services;
using System.Collections.ObjectModel;

namespace AdRev.Mobile;

public partial class DashboardPage : ContentPage
{
    private readonly DatabaseService _dbService;
    private readonly ApiClient _apiClient;
    private readonly ImportService _importService;
    private readonly BillingService _billingService;

    public DashboardPage(DatabaseService dbService, ApiClient apiClient, ImportService importService, BillingService billingService)
    {
        InitializeComponent();
        _dbService = dbService;
        _apiClient = apiClient;
        _importService = importService;
        _billingService = billingService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProfile();
        await RefreshStats();
    }

    private async Task LoadProfile()
    {
        UserNameLabel.Text = Preferences.Default.Get("UserName", "Utilisateur AdRev");
        UserTitleLabel.Text = Preferences.Default.Get("UserTitle", "Chercheur");

        bool isUniversal = Preferences.Default.Get("IsUniversalLicense", false);
        bool isPro = await _billingService.IsProUserAsync();

        bool hasPremiumFeatures = isUniversal || isPro;

        LicenseLabel.Text = hasPremiumFeatures ? (isUniversal ? "FULL / UNIVERSAL" : "MOBILE PRO") : "LITE (Gratuit)";
        LicenseBadge.BackgroundColor = hasPremiumFeatures ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
        LicenseLabel.TextColor = hasPremiumFeatures ? Colors.White : Colors.Gray;

        UpgradeFrame.IsVisible = !hasPremiumFeatures;
    }

    private async void OnUpgradeClicked(object sender, EventArgs e)
    {
        bool success = await _billingService.PurchaseProLicenceAsync();
        if (success)
        {
            await DisplayAlert("Félicitations", "Vous êtes maintenant membre Pro ! Toutes les fonctionnalités sont débloquées.", "Génial");
            await LoadProfile();
        }
        else
        {
            await DisplayAlert("Achat", "L'achat n'a pas pu être complété.", "OK");
        }
    }

    private async void OnProfileTapped(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync("Profil", "Votre nom complet :", initialValue: UserNameLabel.Text);
        if (name != null)
        {
            Preferences.Default.Set("UserName", name);
            string title = await DisplayPromptAsync("Profil", "Votre titre / fonction :", initialValue: UserTitleLabel.Text);
            if (title != null)
            {
                Preferences.Default.Set("UserTitle", title);
                await LoadProfile();
            }
        }
        else
        {
            // If they cancel name, maybe they want to reset?
            bool reset = await DisplayAlert("Initialisation", "Voulez-vous réinitialiser toutes les données locales (mise à zéro) ?", "Oui, tout effacer", "Annuler");
            if (reset)
            {
                await _dbService.ClearDataAsync();
                await RefreshStats();
                await DisplayAlert("AdRev", "Application réinitialisée à zéro.", "OK");
            }
        }
    }

    private async Task RefreshStats()
    {
        try
        {
            var forms = await _dbService.GetQuestionnairesAsync();
            var data = await _dbService.GetCollectedDataAsync();
            var unsynced = data.Where(d => !d.IsSynced).Count();

            FormsCountLabel.Text = forms.Count.ToString();
            RecordsCountLabel.Text = data.Count.ToString();
            UnsyncedLabel.Text = $"{unsynced} enregistrement(s)";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading stats: {ex.Message}");
        }
    }

    private async void OnNewEntryClicked(object sender, EventArgs e)
    {
        // Check limits for Lite users
        bool isUniversal = Preferences.Default.Get("IsUniversalLicense", false);
        bool isPro = await _billingService.IsProUserAsync();

        if (!isUniversal && !isPro)
        {
            var dataCount = (await _dbService.GetCollectedDataAsync()).Count;
            if (dataCount >= 20)
            {
                await DisplayAlert("Limite Atteinte", "La version gratuite est limitée à 20 enregistrements. Passez à la version Pro pour continuer.", "Voir les options");
                return;
            }
        }

        // Navigate to FormsPage tab
        await Shell.Current.GoToAsync("//FormsPage");
    }

    private async void OnSyncClicked(object sender, EventArgs e)
    {
        // Navigate to FormsPage to Sync (Simpler to reuse existing button there, or replicate logic?)
        // Let's replicate logic or guide user. 
        // Better: Navigate to FormsPage which has the Sync logic
        await Shell.Current.GoToAsync("//FormsPage");
        // Optionally trigger sync automatically? Hard to pass parameters via Shell easily without query props.
        // For now, just navigation.
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var data = await _dbService.GetCollectedDataAsync();
            var questionnaires = await _dbService.GetQuestionnairesAsync();

            if (data.Count == 0 && questionnaires.Count == 0)
            {
                await DisplayAlert("Info", "Rien à exporter (ni données, ni protocoles).", "OK");
                return;
            }

            string action = await DisplayActionSheet("Format d'exportation", "Annuler", null, "JSON (Projet complet)", "CSV (Données de collecte)");

            if (action == "JSON (Projet complet)")
            {
                // Create complete package
                var package = new AdRev.Domain.MobileSync.Models.ProjectSharePackage
                {
                    Data = data,
                    Questionnaires = questionnaires,
                    ExportedByDevice = DeviceInfo.Name,
                    ExportedAt = DateTime.Now
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(package, Newtonsoft.Json.Formatting.Indented);
                var fn = $"AdRev_Project_{DateTime.Now:yyyyMMdd_HHmm}.json";
                var file = Path.Combine(FileSystem.CacheDirectory, fn);
                
                await File.WriteAllTextAsync(file, json);
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "Partager en JSON", File = new ShareFile(file) });
            }
            else if (action == "CSV (Données de collecte)")
            {
                // Check Pro license for advanced export
                bool isUniversal = Preferences.Default.Get("IsUniversalLicense", false);
                bool isPro = await _billingService.IsProUserAsync();

                if (!isUniversal && !isPro)
                {
                    await DisplayAlert("Version Pro Requise", "L'exportation CSV/Excel est une fonctionnalité Pro.", "D'accord");
                    return;
                }

                if (data.Count == 0) { await DisplayAlert("Erreur", "Aucune donnée collectée à exporter.", "OK"); return; }
                
                // For CSV, we need a list of variables. We'll take them from the first available questionnaire if possible, or all.
                // For simplicity, let's take the variables from the first questionnaire.
                var q = questionnaires.FirstOrDefault();
                if (q == null) { await DisplayAlert("Erreur", "Aucun protocole trouvé pour structurer le CSV.", "OK"); return; }

                var csv = _importService.ExportDataToCsv(data, q.Variables);
                var fn = $"AdRev_Data_{DateTime.Now:yyyyMMdd_HHmm}.csv";
                var file = Path.Combine(FileSystem.CacheDirectory, fn);

                await File.WriteAllTextAsync(file, csv, System.Text.Encoding.UTF8);
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "Exporter en CSV (Excel)", File = new ShareFile(file) });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", $"Export impossible : {ex.Message}", "OK");
        }
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Sélectionnez un fichier projet AdRev (.json)",
                // FileTypes = FilePickerFileType.Format // Removed due to error
                // FileTypes = customFileType // If defined
                // Defaulting to allow all for now to unblock build
            });

            if (result != null)
            {
                var json = await File.ReadAllTextAsync(result.FullPath);
                AdRev.Domain.MobileSync.Models.ProjectSharePackage? package = null;

                try
                {
                    package = Newtonsoft.Json.JsonConvert.DeserializeObject<AdRev.Domain.MobileSync.Models.ProjectSharePackage>(json);
                }
                catch
                {
                    await DisplayAlert("Erreur", "Format de fichier invalide.", "OK");
                    return;
                }

                if (package != null)
                {
                    bool confirm = await DisplayAlert("Importer", 
                        $"Voulez-vous importer {package.Questionnaires.Count} protocole(s) et {package.Data.Count} réponse(s) ?", 
                        "Oui", "Annuler");

                    if (confirm)
                    {
                        int qCount = 0;
                        int dCount = 0;

                        // Importer les Questionnaires
                        foreach (var q in package.Questionnaires)
                        {
                            await _dbService.SaveQuestionnaireAsync(q);
                            qCount++;
                        }

                        // Importer les Données
                        foreach (var d in package.Data)
                        {
                            // On marque comme non-sync pour que ce mobile puisse les sync plus tard s'il est jumelé
                            // Ou on garde l'état d'origine ? Mieux vaut garder false pour forcer la sync vers le PC central si besoin.
                            d.IsSynced = false; 
                            await _dbService.SaveCollectedDataAsync(d);
                            dCount++;
                        }

                        await RefreshStats();
                        await DisplayAlert("Succès", $"Importation terminée.\n+{qCount} Protocoles\n+{dCount} Données", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", $"Import échoué : {ex.Message}", "OK");
        }
    }

    private async void OnImportCsvMaskClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Sélectionnez un masque CSV (Nom;Question;Type;Options...)"
            });

            if (result != null)
            {
                var q = await _importService.ImportQuestionnaireFromCsvAsync(result.FullPath);
                if (q != null)
                {
                    bool confirm = await DisplayAlert("Importer Masque", 
                        $"Voulez-vous importer le masque '{q.Title}' avec {q.Variables.Count} questions ?", 
                        "Oui", "Annuler");

                    if (confirm)
                    {
                        await _dbService.SaveQuestionnaireAsync(q);
                        await RefreshStats();
                        await DisplayAlert("Succès", "Masque de saisie importé avec succès !", "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Erreur", "Le fichier n'a pas pu être lu. Vérifiez le format CSV (séparateur: ';').", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", $"Import échoué : {ex.Message}", "OK");
        }
    }

    private async void OnWebsiteTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync("https://adrev-landing.onrender.com");
    }

    private async void OnPrivacyTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync("https://adrev-landing.onrender.com/privacy.html");
    }
}
