using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>The game's sound effects (each is Resources/Sfx/&lt;name&gt;).</summary>
    public enum Sfx { Tap, Page, Pop, Bell, Coin, Build, Error, LevelUp, Select, Chime, Demolish }

    /// <summary>
    /// Plays the game's music and sounds: the title theme on the title screens, the swing theme at
    /// school, and small sounds for buttons, pages, letters, money, building, selections and
    /// level-ups. All are recorded with real sampled instruments by Tools/Music/render.py (the
    /// same band for the music and the sounds) and loaded from Resources/Music and Resources/Sfx;
    /// each sound is already levelled against the others. Volumes come from <see cref="GameSettings"/>.
    /// It listens to the game itself; the HUD only reports clicks, page turns, toasts and the
    /// assistant speaking.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        const float MusicLevel = 0.8f, FadeSeconds = 1.5f, LetterQuiet = 2.5f;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        AudioSource[] _voices;
        int _nextVoice;

        AudioSource _current, _previous;
        float _currentFade, _previousFade;
        AudioClip _titleTrack, _campusTrack;

        GameController _game;
        CampusController _campus;
        Simulation _sim;
        bool _listening;
        int _selectedRoom = -1;
        long _selectedPerson = -1;

        void Awake()
        {
            Instance = this;
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            _voices = new AudioSource[6];
            for (int i = 0; i < _voices.Length; i++) _voices[i] = NewSource(false);
            _current = NewSource(true);
            _previous = NewSource(true);

            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                var clip = Resources.Load<AudioClip>("Sfx/" + sfx);
                if (clip != null) _clips[sfx] = clip;
                else Debug.LogWarning($"No sound for {sfx} (Resources/Sfx/{sfx})");
            }
            _titleTrack = Resources.Load<AudioClip>("Music/Title");
            _campusTrack = Resources.Load<AudioClip>("Music/Campus");
        }

        AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Plays a sound effect (nothing if the game has no audio yet).</summary>
        public static void Play(Sfx sfx)
        {
            if (Instance != null) Instance.PlaySound(sfx);
        }

        void PlaySound(Sfx sfx)
        {
            if (!_clips.TryGetValue(sfx, out var clip)) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) && now - last < 0.06f) return;   // one at a time, not a pile-up
            _lastPlayed[sfx] = now;
            float volume = Curve(GameSettings.Sound);
            if (volume <= 0.001f) return;
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.pitch = sfx == Sfx.Tap || sfx == Sfx.Select ? UnityEngine.Random.Range(0.96f, 1.04f) : 1f;
            voice.PlayOneShot(clip, volume);
        }

        /// <summary>Sliders feel even when the level is squared.</summary>
        static float Curve(float level) => level * level;

        void Update()
        {
            UpdateMusic(Time.unscaledDeltaTime);
            WatchGame();
        }

        void UpdateMusic(float dt)
        {
            if (_game == null) _game = GameController.Instance;
            bool playing = _game != null && _game.Phase == GamePhase.Playing;
            var wanted = playing ? _campusTrack : _titleTrack;
            if (wanted != null && _current.clip != wanted)
            {
                // Cross-fade: the old tune fades out while the new one starts from its beginning.
                (_current, _previous) = (_previous, _current);
                _previousFade = _currentFade;
                _current.clip = wanted;
                _current.Play();
                _currentFade = 0f;
            }
            _currentFade = Mathf.MoveTowards(_currentFade, 1f, dt / FadeSeconds);
            _previousFade = Mathf.MoveTowards(_previousFade, 0f, dt / FadeSeconds);
            if (_previousFade <= 0f && _previous.isPlaying) _previous.Stop();
            float level = MusicLevel * Curve(GameSettings.Music);
            _current.volume = level * _currentFade;
            _previous.volume = level * _previousFade;
        }

        /// <summary>Sounds for what happens in the school: letters, level-ups, building and buying, selecting.</summary>
        void WatchGame()
        {
            if (_game == null) return;
            if (!_listening)
            {
                _game.MessageArrived += OnMessage;
                _listening = true;
            }
            var sim = _game.Sim;
            if (sim != _sim)
            {
                if (_sim != null) _sim.LeveledUp -= OnLevelUp;
                _sim = sim;
                if (_sim != null) _sim.LeveledUp += OnLevelUp;
            }

            var campus = CampusController.Instance;
            if (campus != _campus)
            {
                if (_campus != null) _campus.CommandDone -= OnCampusCommand;
                _campus = campus;
                if (_campus != null) _campus.CommandDone += OnCampusCommand;
            }
            if (_campus == null || _game.Phase != GamePhase.Playing) return;
            int room = _campus.SelectedRoomId;
            long person = _campus.SelectedPerson;
            if ((room >= 0 && room != _selectedRoom) || (person >= 0 && person != _selectedPerson)) Play(Sfx.Select);
            _selectedRoom = room;
            _selectedPerson = person;
        }

        void OnMessage(InboxMessage message)
        {
            if (_lastPlayed.TryGetValue(Sfx.Bell, out float last) && Time.unscaledTime - last < LetterQuiet) return;
            Play(Sfx.Bell);
        }

        void OnLevelUp(int level) => Play(Sfx.LevelUp);

        void OnCampusCommand(CommandResult result)
        {
            if (!result.Ok) Play(Sfx.Error);
            else Play(_campus != null && _campus.Tool == CampusTool.Land ? Sfx.Coin : Sfx.Build);
        }

        void OnDestroy()
        {
            if (_game != null && _listening) _game.MessageArrived -= OnMessage;
            if (_sim != null) _sim.LeveledUp -= OnLevelUp;
            if (_campus != null) _campus.CommandDone -= OnCampusCommand;
            if (Instance == this) Instance = null;
        }
    }
}
