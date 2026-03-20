using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AdRev.Domain.MobileSync.Models;
using AdRev.Domain.Variables;
using AdRev.Domain.Enums;

namespace AdRev.Mobile.Services
{
    public class ImportService
    {
        /// <summary>
        /// Imports a questionnaire structure from a CSV file.
        /// Expected format: Name;Prompt;Type;Options;Required;Sensitive
        /// Example: AGE;Quel est votre âge ?;QuantitativeDiscrete;;True;False
        /// </summary>
        public async Task<MobileQuestionnaire?> ImportQuestionnaireFromCsvAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                if (lines.Length < 2) return null; // Header + at least one row

                var questionnaire = new MobileQuestionnaire
                {
                    Title = Path.GetFileNameWithoutExtension(filePath),
                    Description = $"Importé le {DateTime.Now:dd/MM/yyyy HH:mm}",
                    Variables = new List<StudyVariable>()
                };

                // Skip header
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(';');
                    if (parts.Length < 3) continue;

                    var variable = new StudyVariable
                    {
                        Name = parts[0].Trim(),
                        Prompt = parts[1].Trim(),
                        Type = ParseVariableType(parts[2].Trim()),
                        ChoiceOptions = parts.Length > 3 ? parts[3].Trim() : string.Empty,
                        IsRequired = parts.Length > 4 && bool.TryParse(parts[4].Trim(), out var req) ? req : false,
                        IsSensitive = parts.Length > 5 && bool.TryParse(parts[5].Trim(), out var sens) ? sens : false
                    };

                    questionnaire.Variables.Add(variable);
                }

                return questionnaire;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private VariableType ParseVariableType(string typeStr)
        {
            if (Enum.TryParse<VariableType>(typeStr, true, out var result))
            {
                return result;
            }
            return VariableType.Text;
        }

        /// <summary>
        /// Prepares a CSV export of collected data to be sent to a PC.
        /// </summary>
        public string ExportDataToCsv(List<CollectedDataRecord> records, List<StudyVariable> variables)
        {
            var sb = new StringBuilder();

            // Header: ID;Date;Device;Var1;Var2...
            sb.Append("ID;Date;DeviceId");
            foreach (var v in variables)
            {
                sb.Append($";{v.Name}");
            }
            sb.AppendLine();

            // Rows
            foreach (var record in records)
            {
                sb.Append($"{record.Id};{record.CollectedAt:yyyy-MM-dd HH:mm:ss};{record.DeviceId}");
                
                var answers = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(record.AnswersJson) 
                             ?? new Dictionary<string, string>();

                foreach (var v in variables)
                {
                    var val = answers.ContainsKey(v.Name) ? answers[v.Name] : "";
                    // Escape semicolons in values
                    val = val.Replace(";", ",");
                    sb.Append($";{val}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
