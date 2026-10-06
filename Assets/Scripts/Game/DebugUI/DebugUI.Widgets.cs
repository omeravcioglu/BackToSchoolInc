using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        static readonly Color Good = new Color(0.55f, 0.9f, 0.6f);
        static readonly Color Warn = new Color(1f, 0.82f, 0.4f);
        static readonly Color Bad = new Color(1f, 0.5f, 0.45f);
        static readonly Color Neutral = new Color(0.85f, 0.88f, 0.95f);
        static readonly Color Dim = new Color(0.65f, 0.67f, 0.72f);

        GUIStyle _h1, _h2, _bold, _wrap, _small, _note, _panel, _rowButton, _mono;
        Texture2D _panelTex, _edgeTex, _cardTex;
        bool _stylesReady;
        string _confirmKey;
        float _confirmUntil;

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _panelTex = new Texture2D(1, 1);
            _panelTex.SetPixel(0, 0, new Color(0.16f, 0.17f, 0.2f, 1f));
            _panelTex.Apply();
            _edgeTex = new Texture2D(1, 1);
            _edgeTex.SetPixel(0, 0, new Color(0.32f, 0.35f, 0.42f, 1f));
            _edgeTex.Apply();
            _cardTex = new Texture2D(1, 1);
            _cardTex.SetPixel(0, 0, new Color(0.2f, 0.215f, 0.25f, 1f));
            _cardTex.Apply();

            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _h2 = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _small.normal.textColor = Dim;
            _note = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };   // small but tintable
            _mono = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            // Card style for GUILayout groups, built from scratch: copying the skin's box would
            // inherit its semi-transparent background images.
            _panel = new GUIStyle
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(4, 4, 4, 4),
            };
            _panel.normal.background = _cardTex;
            _panel.normal.textColor = Color.white;
            _rowButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true };
        }

        /// <summary>Opaque panel background with a thin border (tinted by GUI.color).</summary>
        void Panel(Rect r)
        {
            GUI.DrawTexture(r, _panelTex, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), _edgeTex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), _edgeTex);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), _edgeTex);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), _edgeTex);
        }

        static Color ValueColor(float v) => v < 35f ? Bad : v < 55f ? Warn : Good;

        /// <summary>A 0-100 bar with the value printed beside it.</summary>
        void Bar(float value, float width = 120f, Color? color = null)
        {
            var rect = GUILayoutUtility.GetRect(width, 14f, GUILayout.Width(width), GUILayout.Height(14f));
            rect.y += 4f;
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color ?? ValueColor(value);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value / 100f), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Label(value.ToString("0"), GUILayout.Width(28));
        }

        void LabeledBar(string label, float value, float labelWidth = 140f, float barWidth = 120f, string tooltip = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(labelWidth));
            Bar(value, barWidth);
            GUILayout.EndHorizontal();
        }

        void KeyValue(string key, string value, float keyWidth = 160f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, _small, GUILayout.Width(keyWidth));
            GUILayout.Label(value);
            GUILayout.EndHorizontal();
        }

        void Section(string title)
        {
            GUILayout.Space(6);
            GUILayout.Label(title, _h2);
        }

        void Colored(string text, Color color, GUIStyle style = null)
        {
            GUI.color = color;
            GUILayout.Label(text, style == _small ? _note : style ?? _wrap);
            GUI.color = Color.white;
        }

        void BreakdownView(Breakdown b)
        {
            if (b == null || b.Factors.Count == 0)
            {
                GUILayout.Label("(no data)", _small);
                return;
            }
            var list = new System.Collections.Generic.List<Factor>(b.Factors);
            list.Sort((x, y) => Mathf.Abs(y.Value).CompareTo(Mathf.Abs(x.Value)));
            foreach (var f in list)
            {
                if (Mathf.Abs(f.Value) < 0.05f) continue;
                GUILayout.BeginHorizontal();
                GUI.color = f.Value >= 0 ? Good : Bad;
                GUILayout.Label(MathUtil.Signed(f.Value, "0.0"), GUILayout.Width(52));
                GUI.color = Color.white;
                GUILayout.Label(f.Label, _wrap);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"= {b.Clamped:0}", _bold);
        }

        /// <summary>A button that must be clicked twice within 3 seconds.</summary>
        bool ConfirmButton(string label, string key, params GUILayoutOption[] options)
        {
            bool armed = _confirmKey == key && Time.unscaledTime < _confirmUntil;
            GUI.color = armed ? Bad : Color.white;
            bool clicked = GUILayout.Button(armed ? "Confirm?" : label, options);
            GUI.color = Color.white;
            if (!clicked) return false;
            if (armed)
            {
                Defer(() => _confirmKey = null);
                return true;
            }
            Defer(() =>
            {
                _confirmKey = key;
                _confirmUntil = Time.unscaledTime + 3f;
            });
            return false;
        }

        /// <summary>A button that is disabled (with the reason as tooltip) when <paramref name="whyNot"/> is set.</summary>
        bool ActionButton(string label, string whyNot, params GUILayoutOption[] options)
        {
            bool enabled = whyNot == null;
            GUI.enabled = enabled;
            bool clicked = GUILayout.Button(new GUIContent(label, whyNot), options);
            GUI.enabled = true;
            return clicked && enabled;
        }

        static string Money(float v) => MathUtil.Money(v);
    }
}
