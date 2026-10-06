using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>Player preferences that outlive a school: window mode, notebook size, assistant tips, volumes.</summary>
    public static class GameSettings
    {
        const string SizeKey = "settings.uiSize";
        const string TipsKey = "settings.tips";
        const string MusicKey = "settings.music";
        const string SoundKey = "settings.sound";

        /// <summary>0 = small (more fits on screen), 1 = normal (the design size).</summary>
        public static int UiSize
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SizeKey, 1), 0, 1);
            set
            {
                PlayerPrefs.SetInt(SizeKey, Mathf.Clamp(value, 0, 1));
                PlayerPrefs.Save();
            }
        }

        /// <summary>The assistant pops up with problems, news and new features.</summary>
        public static bool Tips
        {
            get => PlayerPrefs.GetInt(TipsKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(TipsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Music volume, 0 to 1.</summary>
        public static float Music
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.6f));
            set
            {
                PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Sound effects volume, 0 to 1.</summary>
        public static float Sound
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SoundKey, 0.8f));
            set
            {
                PlayerPrefs.SetFloat(SoundKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set
            {
                if (Screen.fullScreen == value) return;
                if (value)
                {
                    var r = Screen.currentResolution;
                    Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
                }
                else Screen.SetResolution(Mathf.Max(1280, Screen.width * 3 / 4), Mathf.Max(720, Screen.height * 3 / 4), FullScreenMode.Windowed);
            }
        }
    }
}
