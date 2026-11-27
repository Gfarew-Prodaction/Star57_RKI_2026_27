using System;
using System.IO;
using UnityEngine;

namespace Zvezda57.Core
{
    internal sealed class JsonSaveFile<T> where T : class
    {
        private readonly string _path;
        private readonly string _description;

        public JsonSaveFile(string path, string description)
        {
            _path = Path.GetFullPath(path);
            _description = description;
        }

        private string BackupPath => _path + ".bak";
        private string TemporaryPath => _path + ".tmp";

        public T Load(Func<T, bool> isValid)
        {
            return Read(_path, isValid) ?? Read(BackupPath, isValid);
        }

        public void Write(T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(TemporaryPath, JsonUtility.ToJson(value, true));

            if (File.Exists(_path))
            {
                File.Replace(TemporaryPath, _path, BackupPath);
            }
            else
            {
                File.Move(TemporaryPath, _path);
            }
        }

        public void Delete()
        {
            foreach (string path in new[] { _path, BackupPath, TemporaryPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private T Read(string path, Func<T, bool> isValid)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var saved = JsonUtility.FromJson<T>(File.ReadAllText(path));
                return saved != null && isValid(saved) ? saved : null;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                Debug.LogWarning($"Cannot read {_description} from '{path}': {exception.Message}");
                return null;
            }
        }
    }
}
