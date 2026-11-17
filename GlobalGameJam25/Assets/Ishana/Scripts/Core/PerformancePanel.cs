using Ishana.Elements;
using Ishana.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ishana.Core
{
    /// <summary>
    /// Press F3 while playing to see the frame rate and switch the expensive parts of the game off one at
    /// a time. Whatever makes the frame rate jump when you switch it off is what is slowing the game down.
    ///
    /// The switches are remembered between plays (on this computer only), so you can keep something off
    /// while you work. F10 turns everything back on.
    ///
    /// Created automatically by IshanaBootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Performance Panel (F3)")]
    public class PerformancePanel : MonoBehaviour
    {
        const string Prefix = "Ishana.Perf.";

        class Switch
        {
            public string name, key, hint;
            public bool on = true;
            public System.Action<bool> apply;
        }

        Switch[] _switches;
        bool _open;
        float _smoothed = 1f / 60f, _worst, _worstUntil;
        float _noteUntil;
        Light[] _lights;
        LightShadows[] _lightShadows;
        GUIStyle _box, _label;
        WorldScatter _scatter;

        void Awake()
        {
            _switches = new[]
            {
                new Switch { name = "Generated trees, rocks, grass", key = "Scatter", hint = "F5", apply = ApplyScatter },
                new Switch { name = "Painted plants on the terrain", key = "Details", hint = "F6", apply = ApplyDetails },
                new Switch { name = "Shadows", key = "Shadows", hint = "F7", apply = ApplyShadows },
                new Switch { name = "Post-processing (bloom, colour)", key = "Post", hint = "F8", apply = ApplyPost },
                new Switch { name = "Layered ground textures", key = "Ground", hint = "F9", apply = ApplyGround },
            };
            foreach (Switch s in _switches) s.on = PlayerPrefs.GetInt(Prefix + s.key, 1) == 1;
        }

        System.Collections.IEnumerator Start()
        {
            // Let the other systems finish setting up before applying remembered switches
            yield return null;
            yield return null;
            bool anyOff = false;
            foreach (Switch s in _switches)
                if (!s.on) { s.apply(false); anyOff = true; }
            if (anyOff) _noteUntil = Time.unscaledTime + 8f;
        }

        // ------------------------------------------------------------------ switches

        void ApplyScatter(bool on)
        {
            var scatter = FindFirstObjectByType<WorldScatter>(FindObjectsInactive.Include);
            if (scatter != null) scatter.SetShown(on);
        }

        void ApplyDetails(bool on)
        {
            foreach (Terrain t in Terrain.activeTerrains)
                if (t != null) t.drawTreesAndFoliage = on;
        }

        void ApplyShadows(bool on)
        {
            if (_lights == null)
            {
                _lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                _lightShadows = new LightShadows[_lights.Length];
                for (int i = 0; i < _lights.Length; i++) _lightShadows[i] = _lights[i].shadows;
            }
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].shadows = on ? _lightShadows[i] : LightShadows.None;
        }

        void ApplyPost(bool on)
        {
            var fx = FindFirstObjectByType<BlightPostFX>();
            if (fx != null) fx.enableCameraPostProcessing = on; // Otherwise it switches it straight back on
            Camera cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = on;
        }

        void ApplyGround(bool on)
        {
            var polish = FindFirstObjectByType<TerrainPolish>();
            if (polish != null) polish.SetLayered(on);
        }

        void Toggle(Switch s)
        {
            s.on = !s.on;
            PlayerPrefs.SetInt(Prefix + s.key, s.on ? 1 : 0);
            s.apply(s.on);
            _worst = 0f;
        }

        // ------------------------------------------------------------------ input and display

        static bool Pressed(int functionKey)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            switch (functionKey)
            {
                case 3: return k.f3Key.wasPressedThisFrame;
                case 5: return k.f5Key.wasPressedThisFrame;
                case 6: return k.f6Key.wasPressedThisFrame;
                case 7: return k.f7Key.wasPressedThisFrame;
                case 8: return k.f8Key.wasPressedThisFrame;
                case 9: return k.f9Key.wasPressedThisFrame;
                case 10: return k.f10Key.wasPressedThisFrame;
            }
            return false;
#else
            return Input.GetKeyDown(KeyCode.F1 + (functionKey - 1));
#endif
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _smoothed = Mathf.Lerp(_smoothed, dt, 0.05f);
            if (Time.unscaledTime > _worstUntil) { _worst = 0f; _worstUntil = Time.unscaledTime + 3f; }
            if (dt > _worst) _worst = dt;

            if (Pressed(3)) _open = !_open;
            if (!_open) return;
            for (int i = 0; i < _switches.Length; i++)
                if (Pressed(5 + i)) Toggle(_switches[i]);
            if (Pressed(10)) ResetAll();
        }

        void ResetAll()
        {
            foreach (Switch s in _switches)
                if (!s.on) Toggle(s);
        }

        void OnGUI()
        {
            if (!_open && Time.unscaledTime > _noteUntil) return;

            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, padding = new RectOffset(10, 10, 8, 8) };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            }

            if (!_open)
            {
                GUI.Label(new Rect(12, 12, 700, 26), "<color=#ffd24a>Some graphics are switched off in the performance panel (F3).</color>", _label);
                return;
            }

            float fps = 1f / Mathf.Max(0.0001f, _smoothed);
            if (_scatter == null) _scatter = FindFirstObjectByType<WorldScatter>(FindObjectsInactive.Include);
            int generated = _scatter != null ? _scatter.ObjectCount : 0;

            var area = new Rect(12, 12, 430, 96 + _switches.Length * 26 + 34);
            GUI.Box(area, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 8, area.width - 20, area.height - 16));
            string colour = fps >= 50f ? "#7CFC7C" : fps >= 28f ? "#ffd24a" : "#ff6a5a";
            GUILayout.Label($"<b><color={colour}>{fps:F0} frames per second</color></b>   ({_smoothed * 1000f:F1} ms, worst {_worst * 1000f:F0} ms)", _label);
            GUILayout.Label($"Generated objects: {generated}    Fires burning: {Flammable.BurningCount}", _label);
            GUILayout.Label("Switch one off and watch the number above:", _label);
            foreach (Switch s in _switches)
            {
                string state = s.on ? "<color=#7CFC7C>ON </color>" : "<color=#ff6a5a>OFF</color>";
                GUILayout.Label($"{s.hint}   {state}   {s.name}", _label);
            }
            GUILayout.Label("F10   everything back on", _label);
            GUILayout.EndArea();
        }
    }
}
