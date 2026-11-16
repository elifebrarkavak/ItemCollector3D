using System;
using System.IO;
using UnityEngine;

namespace ItemCollector3D.Progress
{
    /// <summary>Where the progress JSON lives. Swappable so tests never touch the disk.</summary>
    public interface IProgressStore
    {
        /// <returns>true if something was read (the text may still be invalid - the service validates it).</returns>
        bool TryRead(out string json);

        /// <returns>true if the data was written.</returns>
        bool Write(string json);

        void Delete();

        /// <summary>Keeps a copy of unreadable data (so it is not silently lost) before it is replaced.</summary>
        void BackupCorrupt(string json);
    }

    /// <summary>Atomic file store under Application.persistentDataPath (works on Android). Never throws.</summary>
    public sealed class FileProgressStore : IProgressStore
    {
        private readonly string path;

        public FileProgressStore(string fileName = "item_collector_progress.json")
        {
            path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public string FilePath => path;

        public bool TryRead(out string json)
        {
            json = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                json = File.ReadAllText(path);
                return true;
            }
            catch (Exception exception)
            {
                ProgressLog.Warning("Progress file could not be read: " + exception.Message);
                return false;
            }
        }

        public bool Write(string json)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string temp = path + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }

                return true;
            }
            catch (Exception exception)
            {
                ProgressLog.Warning("Progress file could not be written: " + exception.Message);
                return false;
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                ProgressLog.Warning("Progress file could not be deleted: " + exception.Message);
            }
        }

        public void BackupCorrupt(string json)
        {
            try
            {
                File.WriteAllText(path + ".corrupt", json ?? string.Empty);
            }
            catch (Exception exception)
            {
                ProgressLog.Warning("Corrupt progress backup failed: " + exception.Message);
            }
        }
    }

    /// <summary>In-memory store (tests, tools). Optionally pre-loaded with text.</summary>
    public sealed class MemoryProgressStore : IProgressStore
    {
        public string Json { get; set; }
        public int WriteCount { get; private set; }
        public int BackupCount { get; private set; }
        public string LastBackup { get; private set; }

        public MemoryProgressStore() { }

        public MemoryProgressStore(string initialJson)
        {
            Json = initialJson;
        }

        public bool TryRead(out string json)
        {
            json = Json;
            return Json != null;
        }

        public bool Write(string json)
        {
            Json = json;
            WriteCount++;
            return true;
        }

        public void Delete()
        {
            Json = null;
        }

        public void BackupCorrupt(string json)
        {
            BackupCount++;
            LastBackup = json;
        }
    }
}
