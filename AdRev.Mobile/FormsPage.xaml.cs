using AdRev.Mobile.Services;
using AdRev.Domain.MobileSync.Models;
using System.Collections.ObjectModel;

namespace AdRev.Mobile;

public partial class FormsPage : ContentPage
{
    private readonly DatabaseService _dbService;
    private readonly ApiClient _apiClient;
    private ObservableCollection<MobileQuestionnaire> _forms = new();

    public FormsPage(DatabaseService dbService, ApiClient apiClient)
    {
        InitializeComponent();
        _dbService = dbService;
        _apiClient = apiClient;
        FormsCollectionView.ItemsSource = _forms;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadFormsAsync();
    }

    private async Task LoadFormsAsync()
    {
        var forms = await _dbService.GetQuestionnairesAsync();
        _forms.Clear();
        foreach (var f in forms) _forms.Add(f);
    }

    private async void OnFormSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MobileQuestionnaire selectedForm) return;
        
        // Navigate to DataEntryPage
        await Navigation.PushAsync(new DataEntryPage(selectedForm, _dbService));
        // await DisplayAlert("Info", $"Vous avez sélectionné : {selectedForm.Title}", "OK");
        
        FormsCollectionView.SelectedItem = null;
    }

    private async void OnSyncClicked(object sender, EventArgs e)
    {
        IsBusy = true;
        try
        {
            // 1. Upload unsynced data
            var unsyncedData = (await _dbService.GetCollectedDataAsync()).Where(d => !d.IsSynced).ToList();
            if (unsyncedData.Any())
            {
                bool uploadSuccess = await _apiClient.UploadDataAsync(unsyncedData);
                if (uploadSuccess)
                {
                    foreach (var record in unsyncedData)
                    {
                        record.IsSynced = true;
                        await _dbService.SaveCollectedDataAsync(record);
                    }
                    await DisplayAlert("Succès", $"{unsyncedData.Count} enregistrements envoyés.", "OK");
                }
                else
                {
                    await DisplayAlert("Avertissement", "Échec de l'envoi des données.", "OK");
                }
            }
            
            // 2. Download questionnaires
            var serverForms = await _apiClient.GetQuestionnairesAsync();
            if (serverForms != null && serverForms.Any())
            {
                foreach (var form in serverForms)
                {
                    await _dbService.SaveQuestionnaireAsync(form);
                }
                await LoadFormsAsync(); // Refresh list
                await DisplayAlert("Succès", $"{serverForms.Count} formulaires mis à jour.", "OK");
            }
            else
            {
                await DisplayAlert("Info", "Aucun formulaire reçu du serveur.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", $"Erreur de synchro : {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        // Simple refresh from local DB
        await LoadFormsAsync();
    }

    private async void OnGenerateDemoClicked(object sender, EventArgs e)
    {
        await _dbService.SeedDemoDataAsync();
        await LoadFormsAsync();
    }
}
