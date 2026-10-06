using UnityEngine;
using UnityEngine.InputSystem;

namespace SchoolSim.Game
{
    /// <summary>
    /// Orthographic camera over the campus: WASD / arrow keys or right/middle-drag to pan,
    /// scroll to zoom toward the cursor. Frames the campus whenever a new game starts, and can
    /// follow someone walking around.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CampusCamera : MonoBehaviour
    {
        public static CampusCamera Instance { get; private set; }

        public float MinSize = 4f;
        public float MaxSize = 26f;

        /// <summary>Set by the controller: false while another screen covers the map.</summary>
        public bool InputEnabled { get; set; }

        /// <summary>Set by the controller: the pointer is over a UI panel.</summary>
        public bool PointerBlocked { get; set; }

        Camera _cam;
        Simulation _framedSim;
        bool _dragging;
        Vector3 _dragWorld;
        System.Func<Vector2?> _follow;

        void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 10f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = InkPalette.Hex(0xE3EDF9);
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;
            transform.rotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            var sim = GameController.Instance != null ? GameController.Instance.Sim : null;
            var view = CampusView.Instance;
            if (sim == null || view == null) return;

            if (sim != _framedSim)
            {
                _framedSim = sim;
                _follow = null;
                Frame(view.CampusCenter(), 10f);
            }
            if (_follow != null)
            {
                var target = _follow();
                if (target == null) _follow = null;
                else
                {
                    var p = transform.position;
                    var goal = new Vector3(target.Value.x, target.Value.y + 0.5f, p.z);
                    transform.position = Vector3.Lerp(p, goal, 1f - Mathf.Exp(-5f * GameClock.Delta));
                }
            }
            if (!InputEnabled)
            {
                _dragging = false;
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            float size = _cam.orthographicSize;

            if (keyboard != null && GUIUtility.keyboardControl == 0)
            {
                Vector2 dir = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) dir.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) dir.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) dir.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) dir.y += 1f;
                if (dir != Vector2.zero)
                {
                    _follow = null;
                    transform.position += (Vector3)(dir.normalized * size * 1.3f * GameClock.Delta);
                }
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) ZoomAt(1f / 1.2f, ScreenCenter());
                if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) ZoomAt(1.2f, ScreenCenter());
            }

            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (!PointerBlocked)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f) ZoomAt(scroll > 0f ? 1f / 1.12f : 1.12f, pos);

                    if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                    {
                        _dragging = true;
                        _dragWorld = ScreenToWorld(pos);
                    }
                }
                if (_dragging)
                {
                    if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                    {
                        var move = _dragWorld - ScreenToWorld(pos);
                        if (move.sqrMagnitude > 0.0004f) _follow = null;
                        transform.position += move;
                    }
                    else
                        _dragging = false;
                }
            }

            Clamp(view.MapSize);
        }

        Vector2 ScreenCenter() => new Vector2(Screen.width / 2f, Screen.height / 2f);

        Vector3 ScreenToWorld(Vector2 screen)
        {
            var w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -transform.position.z));
            w.z = 0f;
            return w;
        }

        void ZoomAt(float factor, Vector2 screen)
        {
            var before = ScreenToWorld(screen);
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize * factor, MinSize, MaxSize);
            var after = ScreenToWorld(screen);
            transform.position += before - after;
        }

        void Clamp(Vector2 map)
        {
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, -4f, map.x + 4f);
            p.y = Mathf.Clamp(p.y, -4f, map.y + 4f);
            p.z = -10f;
            transform.position = p;
        }

        public void Frame(Vector2 center, float size)
        {
            _follow = null;
            _cam.orthographicSize = Mathf.Clamp(size, MinSize, MaxSize);
            transform.position = new Vector3(center.x, center.y, -10f);
        }

        /// <summary>Keeps gliding after a moving target until it returns null or the player pans.</summary>
        public void Follow(System.Func<Vector2?> target, float size)
        {
            _cam.orthographicSize = Mathf.Clamp(size, MinSize, MaxSize);
            _follow = target;
        }

        public void StopFollowing() => _follow = null;

        public bool Following => _follow != null;

        /// <summary>True while the right or middle button is dragging the view.</summary>
        public bool IsDragging => _dragging;
    }
}
