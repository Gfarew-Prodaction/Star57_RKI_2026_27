using UnityEngine.SceneManagement;
using UnityEngine;

namespace Zvezda57.Core
{
    public static class SceneNavigation
    {
        public const string MenuPath = "Assets/Scenes/Menu.unity";
        public const string GamePath = "Assets/Scenes/MainGame.unity";
        public static event System.Action LeavingGame;
        public static bool IsLoading { get; private set; }
        private static bool s_newGameRequested;
        private static string s_menuMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            IsLoading = false;
            LeavingGame = null;
            s_newGameRequested = false;
            s_menuMessage = null;
        }

        public static void OpenGame(bool newGame)
        {
            if (IsLoading)
            {
                return;
            }

            s_newGameRequested = newGame;
            try
            {
                Load(GamePath);
            }
            catch
            {
                s_newGameRequested = false;
                throw;
            }
        }

        internal static void PrepareGameSave()
        {
            if (s_newGameRequested)
            {
                s_newGameRequested = false;
                SavePaths.Clear();
            }
        }

        internal static void RejectGameConfiguration(string message)
        {
            s_newGameRequested = false;
            s_menuMessage = message;
            OpenMenu();
        }

        internal static string TakeMenuMessage()
        {
            string message = s_menuMessage;
            s_menuMessage = null;
            return message;
        }

        public static void OpenMenu()
        {
            if (IsLoading)
            {
                return;
            }

            LeavingGame?.Invoke();
            Load(MenuPath);
        }

        private static void Load(string path)
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            try
            {
                var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
                operation.completed += _ => IsLoading = false;
            }
            catch
            {
                IsLoading = false;
                throw;
            }
        }
    }
}
