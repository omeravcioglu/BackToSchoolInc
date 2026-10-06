using System;
using UnityEngine;

namespace SchoolSim.Game
{
    public enum GamePhase { Title, Playing }

    /// <summary>
    /// Owns the running <see cref="Simulation"/> and advances it in real time at one fixed pace:
    /// there is no pause and no fast-forward. The clock only stands still on the title screens,
    /// before a school is founded or after the director steps away to the main menu. The school
    /// saves itself into its slot at the start of every month, when the director leaves for the
    /// main menu, when it closes and when the game quits.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        /// <summary>Real seconds per school day: two minutes a month, 24 minutes a school year.</summary>
        public const float SecondsPerDay = 4f;

        public Simulation Sim { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.Title;

        /// <summary>The save slot the running school lives in (-1 before one is founded or loaded).</summary>
        public int Slot { get; private set; } = -1;

        /// <summary>True while the school's clock runs.</summary>
        public bool Running => Phase == GamePhase.Playing && Sim != null && !Sim.State.GameOver && !TourHold;

        /// <summary>How far through the current day we are, 0 to 1 (the HUD's day dial).</summary>
        public float DayProgress => Mathf.Clamp01(_elapsed / SecondsPerDay);

        /// <summary>Real time (since startup) of the last save that reached the disk; negative before any.</summary>
        public float LastSavedAt { get; private set; } = -1f;

        /// <summary>Holds the clock for the automated screenshot tour. Players have no way to stop time.</summary>
        internal bool TourHold { get; set; }

        public event Action<InboxMessage> MessageArrived;

        /// <summary>A school was founded or loaded (the HUD resets its panels).</summary>
        public event Action GameStarted;

        /// <summary>A save of the running school finished: true when it reached the disk.</summary>
        public event Action<bool> Saved;

        float _elapsed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartNewGame(NewGameSetup setup)
        {
            Play(NewGame.Create(setup), SaveSlots.SlotForNewSchool());
            SaveNow();
        }

        /// <summary>
        /// Opens the school saved in a slot. Returns false (and keeps whatever was open) when the
        /// file can't be read or the school has closed.
        /// </summary>
        public bool LoadGame(int slot)
        {
            if (slot == Slot && Sim != null && !Sim.State.GameOver)
            {
                // Already open (and saved when the director left it): just go back in.
                Phase = GamePhase.Playing;
                return true;
            }
            Simulation sim;
            try
            {
                sim = SaveSlots.Load(slot);
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't load save slot {slot + 1}: {e}");
                return false;
            }
            if (sim.State.GameOver) return false;
            Play(sim, slot);
            return true;
        }

        void Play(Simulation sim, int slot)
        {
            if (Sim != null) Sim.MessageArrived -= OnMessageArrived;
            Sim = sim;
            Slot = slot;
            Sim.MessageArrived += OnMessageArrived;
            _elapsed = 0f;
            Phase = GamePhase.Playing;
            GameStarted?.Invoke();
        }

        /// <summary>Saves the running school into its slot (on a worker unless <paramref name="wait"/>).</summary>
        public void SaveNow(bool wait = false)
        {
            if (Sim == null || Slot < 0) return;
            SaveSlots.Write(Slot, Sim, wait);
        }

        /// <summary>A slot was emptied on the Load screen: drop the school if it's the one in memory.</summary>
        public void Forget(int slot)
        {
            if (slot != Slot) return;
            if (Sim != null) Sim.MessageArrived -= OnMessageArrived;
            Sim = null;
            Slot = -1;
            Phase = GamePhase.Title;
        }

        /// <summary>Leave the school for the main menu. Time stands still until the director comes back.</summary>
        public void ShowTitle()
        {
            if (Phase == GamePhase.Playing) SaveNow();
            Phase = GamePhase.Title;
        }

        /// <summary>There's a school to go back to: the one in memory, or the last one saved.</summary>
        public bool CanContinue => (Sim != null && !Sim.State.GameOver) || SaveSlots.Latest() != null;

        public void Continue()
        {
            if (Sim != null && !Sim.State.GameOver)
            {
                Phase = GamePhase.Playing;
                return;
            }
            var latest = SaveSlots.Latest();
            if (latest != null) LoadGame(latest.Index);
        }

        void Update()
        {
            while (SaveSlots.TryTakeFinished(out int slot, out bool ok))
            {
                if (slot != Slot) continue;
                if (ok) LastSavedAt = Time.realtimeSinceStartup;
                Saved?.Invoke(ok);
            }

            if (!Running) return;
            _elapsed += GameClock.Delta;
            int steps = 0;
            while (_elapsed >= SecondsPerDay && steps < 4)
            {
                _elapsed -= SecondsPerDay;
                Sim.AdvanceDay();
                steps++;
                if (Sim.State.GameOver || Calendar.IsFirstDayOfMonth(Sim.Day)) SaveNow();
                if (Sim.State.GameOver) break;
            }
            if (steps >= 4) _elapsed = 0f;   // a long hitch doesn't skip ahead more than a few days
        }

        void OnApplicationQuit()
        {
            if (Phase == GamePhase.Playing) SaveNow(wait: true);
            else SaveSlots.Flush();
        }

        void OnMessageArrived(InboxMessage msg) => MessageArrived?.Invoke(msg);
    }
}
