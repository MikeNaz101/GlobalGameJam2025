using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ishana.UI
{
    /// <summary>
    /// Tells the player what to do next: how many groves are cleansed, how many creatures are left in the
    /// current grove, and where the nearest blighted grove is (an on-screen marker with distance).
    /// Also shows short toast messages ("Grove cleansed", "Progress saved").
    ///
    /// The whole UI is built in code on its own canvas, so no prefab or scene setup is needed.
    /// Created automatically by IshanaBootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Objective HUD")]
    public class ObjectiveHUD : MonoBehaviour
    {
        [Header("Layout (reference resolution 1920x1080)")]
        public Vector2 panelOffset = new Vector2(-32f, -32f);
        public float titleSize = 30f;
        public float detailSize = 24f;
        public Color textColor = Color.white;
        public Color accentColor = new Color(0.62f, 1f, 0.72f);

        [Header("Grove Marker")]
        public bool showMarker = true;
        public Color markerColor = new Color(0.62f, 1f, 0.72f, 0.95f);
        public float markerSize = 22f;
        [Tooltip("Keep the marker this far from the screen edge (pixels at reference resolution).")]
        public float markerEdgePadding = 90f;
        [Tooltip("Height above the grove's centre that the marker points at.")]
        public float markerHeight = 3f;

        [Header("Toasts")]
        public float toastDuration = 3f;
        public float toastSize = 34f;

        static ObjectiveHUD _instance;
        static readonly Queue<string> _pendingToasts = new Queue<string>();

        Canvas _canvas;
        RectTransform _canvasRect, _marker;
        TextMeshProUGUI _title, _detail, _markerLabel, _toast;
        AreaCleansingManager[] _groves = new AreaCleansingManager[0];
        GlobalAmbianceController _ambiance;
        Transform _player;
        float _nextSlowUpdate, _toastEnd;
        AreaCleansingManager _nearest;
        bool _allCleansedAnnounced;

        /// <summary>Show a short message near the top of the screen.</summary>
        public static void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_pendingToasts.Count < 6) _pendingToasts.Enqueue(message);
        }

        /// <summary>Force the objective text to update now (e.g. after loading a save).</summary>
        public static void Refresh()
        {
            if (_instance != null) _instance._nextSlowUpdate = 0f;
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildUI();
        }

        void OnEnable()
        {
            AreaCleansingManager.OnAreaCleared += HandleAreaCleared;
            BulletUnlockTrigger.OnAbilityUnlocked += HandleAbilityUnlocked;
        }

        void OnDisable()
        {
            AreaCleansingManager.OnAreaCleared -= HandleAreaCleared;
            BulletUnlockTrigger.OnAbilityUnlocked -= HandleAbilityUnlocked;
        }

        void OnDestroy()
        {
            if (_instance == this) { _instance = null; _pendingToasts.Clear(); }
        }

        void Start()
        {
            _groves = FindObjectsByType<AreaCleansingManager>(FindObjectsSortMode.None);
            _ambiance = FindFirstObjectByType<GlobalAmbianceController>();
            var player = FindFirstObjectByType<PlayerStateManager>();
            if (player != null) _player = player.transform;
        }

        // ------------------------------------------------------------------ events

        void HandleAreaCleared(AreaCleansingManager grove)
        {
            int cleansed = CountCleansed();
            Toast(cleansed >= _groves.Length && _groves.Length > 0
                ? "The forest is cleansed!"
                : $"Grove cleansed!  {cleansed} / {_groves.Length}");
            _nextSlowUpdate = 0f;
        }

        void HandleAbilityUnlocked(BulletUnlockTrigger trigger)
        {
            string ability = trigger != null && trigger.bulletIndexToUnlock == 1 ? "Freeze shot"
                           : trigger != null && trigger.bulletIndexToUnlock == 2 ? "Teleport shot"
                           : trigger != null && trigger.bulletIndexToUnlock == 3 ? "Earth shot"
                           : "New ability";
            Toast($"{ability} unlocked!  Scroll to switch shots");
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (Time.unscaledTime >= _nextSlowUpdate)
            {
                _nextSlowUpdate = Time.unscaledTime + 0.25f;
                UpdateObjectiveText();
            }
            UpdateToast();
        }

        void LateUpdate() => UpdateMarker();

        int CountCleansed()
        {
            int count = 0;
            foreach (var grove in _groves) if (grove != null && grove.IsCleared) count++;
            return count;
        }

        void UpdateObjectiveText()
        {
            if (_groves.Length == 0)
            {
                _title.text = string.Empty; _detail.text = string.Empty; _nearest = null;
                return;
            }

            int cleansed = CountCleansed();
            string accent = ColorUtility.ToHtmlStringRGB(accentColor);
            _title.text = $"Cleanse the forest  <color=#{accent}>{cleansed} / {_groves.Length}</color>";

            // Nearest grove that still needs cleansing
            _nearest = null;
            float best = float.MaxValue;
            if (_player != null)
            {
                foreach (var grove in _groves)
                {
                    if (grove == null || grove.IsCleared) continue;
                    float d = (grove.transform.position - _player.position).sqrMagnitude;
                    if (d < best) { best = d; _nearest = grove; }
                }
            }

            var active = _ambiance != null ? _ambiance.ActiveArea : null;
            if (cleansed >= _groves.Length)
            {
                _detail.text = "The forest is cleansed";
            }
            else if (active != null && !active.IsCleared)
            {
                int left = active.MonstersRemaining;
                _detail.text = left == 1 ? "1 blight creature left in this grove" : $"{left} blight creatures left in this grove";
            }
            else if (_nearest != null)
            {
                _detail.text = $"Nearest blighted grove: {Mathf.RoundToInt(Mathf.Sqrt(best))} m";
            }
            else
            {
                _detail.text = string.Empty;
            }
        }

        void UpdateMarker()
        {
            var cam = Camera.main;
            var active = _ambiance != null ? _ambiance.ActiveArea : null;
            bool show = showMarker && cam != null && _nearest != null && _player != null && active != _nearest
                        && !PauseMenuController.GameIsPaused;
            if (_marker.gameObject.activeSelf != show) _marker.gameObject.SetActive(show);
            if (!show) return;

            Vector3 world = _nearest.transform.position + Vector3.up * markerHeight;
            Vector3 view = cam.WorldToViewportPoint(world);

            // Behind the camera the projection flips, so mirror it and push it to the edge
            if (view.z < 0f)
            {
                view.x = 1f - view.x;
                view.y = 0f;
            }

            Vector2 size = _canvasRect.rect.size;
            Vector2 pos = new Vector2((view.x - 0.5f) * size.x, (view.y - 0.5f) * size.y);
            float maxX = size.x * 0.5f - markerEdgePadding;
            float maxY = size.y * 0.5f - markerEdgePadding;
            pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
            pos.y = Mathf.Clamp(pos.y, -maxY, maxY);
            _marker.anchoredPosition = pos;

            float distance = Vector3.Distance(_player.position, _nearest.transform.position);
            _markerLabel.text = $"{Mathf.RoundToInt(distance)} m";
        }

        void UpdateToast()
        {
            if (Time.unscaledTime >= _toastEnd && _pendingToasts.Count > 0)
            {
                _toast.text = _pendingToasts.Dequeue();
                _toastEnd = Time.unscaledTime + toastDuration;
            }

            float remaining = _toastEnd - Time.unscaledTime;
            float alpha = remaining <= 0f ? 0f
                        : Mathf.Min(1f, remaining / 0.4f, (toastDuration - remaining) / 0.2f);
            var c = _toast.color; c.a = Mathf.Clamp01(alpha); _toast.color = c;
        }

        // ------------------------------------------------------------------ construction

        void BuildUI()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)transform;

            // Objective, top-right
            _title = CreateText("Objective Title", titleSize, TextAlignmentOptions.TopRight, FontStyles.Bold);
            Place(_title.rectTransform, new Vector2(1f, 1f), panelOffset, new Vector2(700f, 40f));
            _detail = CreateText("Objective Detail", detailSize, TextAlignmentOptions.TopRight, FontStyles.Normal);
            Place(_detail.rectTransform, new Vector2(1f, 1f), panelOffset + new Vector2(0f, -titleSize - 10f), new Vector2(700f, 34f));

            // Toast, top-centre
            _toast = CreateText("Toast", toastSize, TextAlignmentOptions.Top, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 50f));
            _toast.color = new Color(textColor.r, textColor.g, textColor.b, 0f);

            // Grove marker: a diamond with the distance under it
            var markerGO = new GameObject("Grove Marker", typeof(RectTransform));
            _marker = (RectTransform)markerGO.transform;
            _marker.SetParent(transform, false);
            _marker.anchorMin = _marker.anchorMax = _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(markerSize, markerSize);

            var diamondGO = new GameObject("Diamond", typeof(RectTransform), typeof(Image));
            var diamond = (RectTransform)diamondGO.transform;
            diamond.SetParent(_marker, false);
            diamond.sizeDelta = new Vector2(markerSize, markerSize);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = diamondGO.GetComponent<Image>();
            image.color = markerColor;
            image.raycastTarget = false;

            _markerLabel = CreateText("Marker Distance", 20f, TextAlignmentOptions.Top, FontStyles.Bold);
            _markerLabel.rectTransform.SetParent(_marker, false);
            _markerLabel.rectTransform.anchorMin = _markerLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _markerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _markerLabel.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            _markerLabel.rectTransform.sizeDelta = new Vector2(160f, 28f);
            _markerLabel.color = markerColor;

            markerGO.SetActive(false);
        }

        TextMeshProUGUI CreateText(string name, float size, TextAlignmentOptions alignment, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.fontStyle = style;
            text.color = textColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = string.Empty;

            // Dark outline so the text reads over bright sky and dark forest alike
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            return text;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
