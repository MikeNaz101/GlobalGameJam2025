using Ishana.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ishana.UI
{
    /// <summary>
    /// When a save file exists, turns the start menu's Play button into "Continue" and adds a
    /// "New Game" button above it (a copy of the Play button, so it matches the menu's style).
    /// New Game erases the save. With no save file the menu is left exactly as it was.
    ///
    /// Added automatically by IshanaBootstrap to scenes that contain a MainMenuController.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Main Menu Save Options")]
    public class MainMenuSaveOptions : MonoBehaviour
    {
        public string continueLabel = "Continue";
        public string newGameLabel = "New Game";
        [Tooltip("Gap between the New Game button and the Play button, as a multiple of the button height.")]
        public float spacing = 1.4f;

        void Start()
        {
            if (!SaveSystem.Exists) return;

            var menu = GetComponent<MainMenuController>();
            if (menu == null) menu = FindFirstObjectByType<MainMenuController>();
            if (menu == null) return;

            Button play = FindPlayButton(menu);
            if (play == null)
            {
                Debug.LogWarning("MainMenuSaveOptions: couldn't find the Play button, so no New Game button was added. " +
                                 "Play will continue from the save.", this);
                return;
            }

            // Copy first, while it still reads "Play"
            Button newGame = Instantiate(play, play.transform.parent);
            newGame.name = "New Game";
            var rect = (RectTransform)newGame.transform;
            var playRect = (RectTransform)play.transform;
            rect.anchoredPosition = playRect.anchoredPosition + new Vector2(0f, playRect.rect.height * spacing);
            rect.SetSiblingIndex(playRect.GetSiblingIndex());

            newGame.onClick = new Button.ButtonClickedEvent(); // Drop the copied "StartGame" hookup
            newGame.onClick.AddListener(() =>
            {
                SaveSystem.Delete();
                menu.StartGame();
            });

            SetLabel(newGame, newGameLabel);
            SetLabel(play, continueLabel);
        }

        static Button FindPlayButton(MainMenuController menu)
        {
            Button byName = null;
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var clicks = button.onClick;
                for (int i = 0; i < clicks.GetPersistentEventCount(); i++)
                {
                    if (clicks.GetPersistentMethodName(i) == nameof(MainMenuController.StartGame)) return button;
                }

                string n = button.name.ToLowerInvariant();
                if (byName == null && (n.Contains("start") || n.Contains("play"))) byName = button;
            }
            return byName;
        }

        static void SetLabel(Button button, string label)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = label; return; }
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
        }
    }
}
