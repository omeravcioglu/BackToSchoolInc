using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The clock everything on screen moves by: people walking, the camera, speech bubbles, toasts,
    /// the assistant and the calendar. Normally it is real time (unscaled, so no menu ever stops
    /// it). While the trailer is recorded it steps exactly one video frame per rendered frame, so
    /// a frame that takes long to capture can't make anything jump: the video is perfectly smooth.
    /// The UI Toolkit panel's own clock (transitions, scheduled items) follows it too.
    /// Sound keeps real time: it isn't recorded with the frames.
    /// </summary>
    public static class GameClock
    {
        static float _step;
        static int _firstFrame;
        static double _start;

        /// <summary>Seconds since the last frame.</summary>
        public static float Delta => _step > 0f ? _step : Time.unscaledDeltaTime;

        /// <summary>Seconds since the game started.</summary>
        public static float Now => _step > 0f ? (float)(_start + (Time.frameCount - _firstFrame) * (double)_step) : Time.unscaledTime;

        /// <summary>
        /// From this frame on, every frame lasts exactly <paramref name="seconds"/>, however long it
        /// really takes. Returns whether the UI panel's clock could follow as well.
        /// </summary>
        public static bool FixStep(float seconds)
        {
            _start = Now;
            _firstFrame = Time.frameCount;
            _step = seconds;
            return HookPanelClock();
        }

        static long PanelMilliseconds() => (long)(Now * 1000.0);

        /// <summary>UI Toolkit times its transitions with an internal Panel.TimeSinceStartup hook.</summary>
        static bool HookPanelClock()
        {
            try
            {
                var panel = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel");
                var prop = panel?.GetProperty("TimeSinceStartup", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop == null || !prop.CanWrite) return false;
                var method = typeof(GameClock).GetMethod(nameof(PanelMilliseconds), BindingFlags.Static | BindingFlags.NonPublic);
                prop.SetValue(null, Delegate.CreateDelegate(prop.PropertyType, method));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("The UI clock couldn't follow the fixed step: " + e.Message);
                return false;
            }
        }
    }
}
