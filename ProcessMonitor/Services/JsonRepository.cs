using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessMonitor.Services
{
    /// <summary>
    /// Simple generic JSON file repository.
    /// Persists a List&lt;T&gt; as a JSON array in a single file.
    /// </summary>
    internal class JsonRepository<T> where T : class
    {
        private readonly string _filePath;

        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented    = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters       = { new JsonStringEnumConverter() }
        };

        public JsonRepository(string filePath)
        {
            _filePath = filePath;

            // Ensure the directory exists
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>Loads all records from disk. Returns an empty list if the file doesn't exist.</summary>
        public List<T> LoadAll()
        {
            if (!File.Exists(_filePath))
                return new List<T>();

            try
            {
                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<T>();

                return JsonSerializer.Deserialize<List<T>>(json, _opts) ?? new List<T>();
            }
            catch (Exception ex)
            {
                // Log to debug output; return empty list so the app can still start
                System.Diagnostics.Debug.WriteLine(
                    $"[JsonRepository] Failed to load {_filePath}: {ex.Message}");
                return new List<T>();
            }
        }

        /// <summary>Saves the entire list to disk, replacing any previous content.</summary>
        public void SaveAll(List<T> items)
        {
            try
            {
                string json = JsonSerializer.Serialize(items, _opts);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[JsonRepository] Failed to save {_filePath}: {ex.Message}");
            }
        }
    }
}
