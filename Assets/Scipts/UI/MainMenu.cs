using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zvezda57.Core;

namespace Zvezda57.UI
{
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField]
        private InputSystemUIInputModule input;
        [SerializeField]
        private GameObject confirmation;
        [SerializeField]
        private TMP_Text status;
        [SerializeField]
        private Button continueButton, newGameButton, quitButton, confirmButton, cancelButton;

        private void Awake()
        {
            input.AssignDefaultActions();
            continueButton.onClick.AddListener(() => Enter(false));
            newGameButton.onClick.AddListener(NewGame);
            quitButton.onClick.AddListener(Application.Quit);
            confirmButton.onClick.AddListener(() => Enter(true));
            cancelButton.onClick.AddListener(() => confirmation.SetActive(false));
            confirmation.SetActive(false);
            continueButton.interactable = SavePaths.HasProgress;

            if (SceneNavigation.TakeMenuMessage() is string message)
            {
                status.text = message;
            }
        }

        private void NewGame()
        {
            if (SavePaths.HasProgress)
            {
                confirmation.SetActive(true);
            }
            else
            {
                Enter(true);
            }
        }

        private void Enter(bool fresh)
        {
            try
            {
                status.text = "Загрузка кабинета…";
                continueButton.interactable = false;
                newGameButton.interactable = false;
                quitButton.interactable = false;
                SceneNavigation.OpenGame(fresh);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                status.text = "Не удалось открыть игру: " + ex.Message;
                continueButton.interactable = SavePaths.HasProgress;
                newGameButton.interactable = true;
                quitButton.interactable = true;
            }
        }
    }
}
