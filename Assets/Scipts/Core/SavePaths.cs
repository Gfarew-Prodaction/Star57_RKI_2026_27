using System.IO;
using UnityEngine;

namespace Zvezda57.Core
{
    public static class SavePaths
    {
        public static string Decisions
        {
            get
            {
#if UNITY_EDITOR
                string validation = UnityEditor.SessionState.GetString("Z57.ManualSavePath", "");
                if (!string.IsNullOrEmpty(validation))
                {
                    return validation;
                }

#endif
                return Path.Combine(Application.isBatchMode ? Application.temporaryCachePath : Application.persistentDataPath, Application.isBatchMode ? "Z57ValidationDecisions.json" : "decisions.json");
            }
        }

        public static bool HasProgress => File.Exists(Decisions) || File.Exists(Decisions + ".campaign") || File.Exists(Decisions + ".campaign.bak");

        public static void Clear()
        {
            foreach (var suffix in new[]
            {
                "",
                ".upgrades",
                ".campaign"
            })
            {
                foreach (var extension in new[]
                {
                    "",
                    ".bak",
                    ".tmp"
                })
                {
                    string path = Decisions + suffix + extension;
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }
    }
}
