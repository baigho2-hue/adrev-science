using SQLite;
using AdRev.Domain.MobileSync.Models;

namespace AdRev.Mobile.Services;

public class DatabaseService
{
    private const string DB_NAME = "adrev_mobile.db3";
    private readonly SQLiteAsyncConnection _connection;

    public DatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DB_NAME);
        _connection = new SQLiteAsyncConnection(dbPath);
        
        // Initialize tables
        _connection.CreateTableAsync<PairedDevice>().Wait();
        _connection.CreateTableAsync<CollectedDataRecord>().Wait();
        
        // We might need a local wrapper for MobileQuestionnaire if it contains complex types like List<StudyVariable>
        // SQLite-net-pcl doesn't support Lists directly without extensions.
        // For simplicity in this demo, we will serialize the questionnaire to JSON string when storing locally, 
        // or just store the variables in a separate table if we want to be relational.
        // Given the time, let's store the questionnaire as a serialized JSON blob in a local wrapper or just keep it in memory for the demo?
        // Better: create a LocalQuestionnaire class.
        _connection.CreateTableAsync<LocalQuestionnaire>().Wait();
    }

    public async Task<PairedDevice?> GetPairedDeviceAsync()
    {
        return await _connection.Table<PairedDevice>().FirstOrDefaultAsync();
    }

    public async Task SavePairedDeviceAsync(PairedDevice device)
    {
        await _connection.DeleteAllAsync<PairedDevice>();
        await _connection.InsertAsync(device);
    }

    public async Task SaveQuestionnaireAsync(MobileQuestionnaire q)
    {
        var local = new LocalQuestionnaire
        {
            Id = q.Id,
            Title = q.Title,
            Description = q.Description,
            JsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(q)
        };
        await _connection.InsertOrReplaceAsync(local);
    }

    public async Task<List<MobileQuestionnaire>> GetQuestionnairesAsync()
    {
        var locals = await _connection.Table<LocalQuestionnaire>().ToListAsync();
        var result = new List<MobileQuestionnaire>();
        foreach (var l in locals)
        {
            try 
            {
                var q = Newtonsoft.Json.JsonConvert.DeserializeObject<MobileQuestionnaire>(l.JsonContent);
                if (q != null) result.Add(q);
            }
            catch { /* Ignore invalid JSON */ }
        }
        return result;
    }

    public async Task SaveCollectedDataAsync(CollectedDataRecord record)
    {
        await _connection.InsertOrReplaceAsync(record);
    }

     public async Task<List<CollectedDataRecord>> GetCollectedDataAsync()
    {
        return await _connection.Table<CollectedDataRecord>().OrderByDescending(x => x.CollectedAt).ToListAsync();
    }

    public async Task ClearDataAsync()
    {
         await _connection.DeleteAllAsync<PairedDevice>();
         await _connection.DeleteAllAsync<LocalQuestionnaire>();
         await _connection.DeleteAllAsync<CollectedDataRecord>();
    }

    public async Task SeedDemoDataAsync()
    {
        var existing = await _connection.Table<LocalQuestionnaire>().CountAsync();
        if (existing > 0) return;

        var demoQ = new MobileQuestionnaire
        {
            Title = "Enquête Démo AdRev",
            Description = "Questionnaire de démonstration pour tester la collecte mobile",
            CreatedAt = DateTime.Now
        };

        demoQ.Variables.Add(new AdRev.Domain.Variables.StudyVariable 
        { 
            Name = "NOM", 
            Prompt = "Nom du participant", 
            Type = AdRev.Domain.Enums.VariableType.Text,
            IsRequired = true 
        });

        demoQ.Variables.Add(new AdRev.Domain.Variables.StudyVariable 
        { 
            Name = "AGE", 
            Prompt = "Âge", 
            Type = AdRev.Domain.Enums.VariableType.QuantitativeDiscrete,
            MinValue = 18,
            MaxValue = 99
        });

        demoQ.Variables.Add(new AdRev.Domain.Variables.StudyVariable 
        { 
            Name = "SEXE", 
            Prompt = "Sexe", 
            Type = AdRev.Domain.Enums.VariableType.QualitativeNominal,
            ChoiceOptions = "Féminin,Masculin"
        });

        demoQ.Variables.Add(new AdRev.Domain.Variables.StudyVariable 
        { 
            Name = "SYMPTOMES", 
            Prompt = "Symptômes présents ?", 
            Type = AdRev.Domain.Enums.VariableType.QualitativeBinary
        });

        demoQ.Variables.Add(new AdRev.Domain.Variables.StudyVariable 
        { 
            Name = "DATE_CONSULT", 
            Prompt = "Date de consultation", 
            Type = AdRev.Domain.Enums.VariableType.QuantitativeTemporal
        });

        await SaveQuestionnaireAsync(demoQ);
    }
}

public class LocalQuestionnaire
{
    [PrimaryKey]
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string JsonContent { get; set; } = ""; // Full serialized MobileQuestionnaire
}
