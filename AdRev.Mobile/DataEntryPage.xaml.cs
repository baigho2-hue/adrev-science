using System;
using AdRev.Domain.MobileSync.Models;
using AdRev.Domain.Variables;
using AdRev.Domain.Enums;
using AdRev.Mobile.Services;

namespace AdRev.Mobile;

public partial class DataEntryPage : ContentPage
{
    private readonly MobileQuestionnaire _questionnaire;
    private readonly DatabaseService _dbService;
    private readonly Dictionary<string, View> _inputs = new();

    public DataEntryPage(MobileQuestionnaire questionnaire, DatabaseService dbService)
    {
        InitializeComponent();
        _questionnaire = questionnaire;
        _dbService = dbService;
        
        LoadForm();
    }

    private void LoadForm()
    {
        FormTitleLabel.Text = _questionnaire.Title;
        FormDescLabel.Text = _questionnaire.Description;
        
        foreach (var variable in _questionnaire.Variables)
        {
            var fieldLayout = CreateField(variable);
            QuestionsContainer.Children.Add(fieldLayout);
        }
    }

    private View CreateField(StudyVariable variable)
    {
        var layout = new VerticalStackLayout { Spacing = 5 };
        
        // Label
        var label = new Label 
        { 
            Text = variable.Prompt + (variable.IsRequired ? " *" : ""),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        label.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
        layout.Children.Add(label);
        
        // Input based on type
        View inputView;
        
        switch (variable.Type)
        {
            case VariableType.QuantitativeDiscrete: // Was Number
            case VariableType.QuantitativeContinuous: // Was Decimal
                var entryNum = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "Entrez un nombre" };
                entryNum.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
                entryNum.SetAppThemeColor(Entry.PlaceholderColorProperty, Colors.Gray, Colors.LightGray);
                entryNum.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                inputView = entryNum;
                break;
                
            case VariableType.QuantitativeTemporal: // Was Date
                var datePicker = new DatePicker { Format = "dd/MM/yyyy" };
                datePicker.SetAppThemeColor(DatePicker.TextColorProperty, Colors.Black, Colors.White);
                datePicker.SetAppThemeColor(DatePicker.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                inputView = datePicker;
                break;
                
            case VariableType.Time:
                var timePicker = new TimePicker();
                timePicker.SetAppThemeColor(TimePicker.TextColorProperty, Colors.Black, Colors.White);
                timePicker.SetAppThemeColor(TimePicker.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                inputView = timePicker;
                break;
                
            case VariableType.QualitativeBinary: // Was YesNo
                var pickerYN = new Picker();
                pickerYN.Items.Add("Oui");
                pickerYN.Items.Add("Non");
                pickerYN.SetAppThemeColor(Picker.TextColorProperty, Colors.Black, Colors.White);
                pickerYN.SetAppThemeColor(Picker.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                pickerYN.SetAppThemeColor(Picker.TitleColorProperty, Colors.Gray, Colors.LightGray);
                inputView = pickerYN;
                break;
                
            case VariableType.QualitativeNominal: // Was Choice
            case VariableType.MultipleChoice:
                var picker = new Picker();
                if (!string.IsNullOrEmpty(variable.ChoiceOptions))
                {
                    foreach (var opt in variable.ChoiceOptions.Split(','))
                        picker.Items.Add(opt.Trim());
                }
                picker.SetAppThemeColor(Picker.TextColorProperty, Colors.Black, Colors.White);
                picker.SetAppThemeColor(Picker.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                picker.SetAppThemeColor(Picker.TitleColorProperty, Colors.Gray, Colors.LightGray);
                inputView = picker;
                break;
                
            case VariableType.Memo:
                var editor = new Editor { HeightRequest = 100, Placeholder = "Saisissez votre texte..." };
                editor.SetAppThemeColor(Editor.TextColorProperty, Colors.Black, Colors.White);
                editor.SetAppThemeColor(Editor.PlaceholderColorProperty, Colors.Gray, Colors.LightGray);
                editor.SetAppThemeColor(Editor.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                inputView = editor;
                break;
                
            default: // Text
                var entry = new Entry { Placeholder = "Texte" };
                entry.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
                entry.SetAppThemeColor(Entry.PlaceholderColorProperty, Colors.Gray, Colors.LightGray);
                entry.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D2D"));
                inputView = entry;
                break;
        }

        // Apply Anonymity Masking if enabled
        bool strictAnonymity = Preferences.Default.Get("StrictAnonymity", false);
        if (strictAnonymity && variable.IsSensitive)
        {
            if (inputView is Entry e) e.IsPassword = true;
            if (inputView is Editor ed) { /* Editor doesn't support IsPassword, maybe hide it? */ }
        }
        
        // Add to map for retrieval
        _inputs[variable.Name] = inputView;
        
        layout.Children.Add(inputView);
        return layout;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var answers = new Dictionary<string, string>();
        bool isValid = true;
        
        foreach (var variable in _questionnaire.Variables)
        {
            if (_inputs.TryGetValue(variable.Name, out var view))
            {
                string value = GetValueFromView(view, variable.Type);
                
                if (variable.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    // Visual feedback could be added here (red border, etc.)
                    isValid = false;
                    await DisplayAlert("Erreur", $"Le champ '{variable.Prompt}' est obligatoire.", "OK");
                    return;
                }
                
                answers[variable.Name] = value;
            }
        }
        
        if (isValid)
        {
            var record = new CollectedDataRecord
            {
                QuestionnaireId = _questionnaire.Id,
                CollectedAt = DateTime.Now,
                DeviceId = "CURRENT_DEVICE", // TODO: Get real ID
                AnswersJson = Newtonsoft.Json.JsonConvert.SerializeObject(answers),
                IsSynced = false
            };
            
            await _dbService.SaveCollectedDataAsync(record);
            await DisplayAlert("Succès", "Données enregistrées avec succès !", "OK");
            await Navigation.PopAsync();
        }
    }

    private string GetValueFromView(View view, VariableType type)
    {
        switch (view)
        {
            case Entry entry: return entry.Text;
            case Editor editor: return editor.Text;
            case DatePicker date: return string.Format("{0:yyyy-MM-dd}", date.Date);
            case TimePicker time: return time.Time.ToString();
            case Picker picker: return picker.SelectedItem?.ToString() ?? "";
            default: return "";
        }
    }
}
