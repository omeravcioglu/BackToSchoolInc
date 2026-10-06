using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>One of the save slots and what its file says.</summary>
    public sealed class SaveSlot
    {
        public int Index;
        public SaveSummary Summary;     // null when the slot is empty or its file can't be read
        public bool Damaged;            // there's a file, but neither it nor its backup reads

        public bool Empty => Summary == null && !Damaged;
        /// <summary>A school that can be played on (closed schools stay as a keepsake).</summary>
        public bool Playable => Summary != null && !Summary.GameOver;
    }

    /// <summary>
    /// The three schools on disk, one per slot like the Load screen's three cards. A save is captured
    /// on the game's thread and written on a worker through a temporary file, keeping the previous
    /// version as a backup, so neither a hitch nor a crash in the middle of a save costs a school.
    /// </summary>
    public static class SaveSlots
    {
        public const int Count = 3;
        const string Extension = ".school";

        static string _folder;
        static SaveSlot[] _slots;
        static Task _writing = Task.CompletedTask;
        static readonly object WriteLock = new object();
        static readonly ConcurrentQueue<(int slot, bool ok)> Finished = new ConcurrentQueue<(int, bool)>();

        /// <summary>Where saves live. The screenshot tour points this at its own folder.</summary>
        public static string Folder
        {
            get => _folder ??= Path.Combine(Application.persistentDataPath, "Saves");
            set
            {
                Flush();
                _folder = value;
                _slots = null;
            }
        }

        static string PathOf(int slot) => Path.Combine(Folder, $"school{slot + 1}{Extension}");
        static string BackupOf(string path) => path + ".bak";

        /// <summary>Every slot (cached: reading the disk only happens on <see cref="Refresh"/>).</summary>
        public static SaveSlot[] All
        {
            get
            {
                if (_slots == null) Refresh();
                return _slots;
            }
        }

        /// <summary>Reads every slot's header again.</summary>
        public static void Refresh()
        {
            Flush();
            var slots = new SaveSlot[Count];
            for (int i = 0; i < Count; i++) slots[i] = ReadSlot(i);
            _slots = slots;
        }

        static SaveSlot ReadSlot(int index)
        {
            var slot = new SaveSlot { Index = index };
            string path = PathOf(index);
            bool any = File.Exists(path) || File.Exists(BackupOf(path));
            slot.Summary = TryReadSummary(path) ?? TryReadSummary(BackupOf(path));
            slot.Damaged = any && slot.Summary == null;
            return slot;
        }

        static SaveSummary TryReadSummary(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return SaveGame.ReadSummary(file);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save {path} couldn't be read: {e.Message}");
                return null;
            }
        }

        /// <summary>The most recently saved school that can be played on, or null.</summary>
        public static SaveSlot Latest()
        {
            SaveSlot best = null;
            foreach (var s in All)
                if (s.Playable && (best == null || s.Summary.SavedAtUtc > best.Summary.SavedAtUtc)) best = s;
            return best;
        }

        /// <summary>Where a new school goes: an empty slot, else the oldest closed school, else the oldest school.</summary>
        public static int SlotForNewSchool()
        {
            var all = All;
            foreach (var s in all)
                if (s.Empty) return s.Index;
            foreach (var s in all)
                if (s.Damaged) return s.Index;
            int pick = -1;
            foreach (var s in all)
            {
                if (!s.Summary.GameOver) continue;
                if (pick < 0 || s.Summary.SavedAtUtc < all[pick].Summary.SavedAtUtc) pick = s.Index;
            }
            if (pick >= 0) return pick;
            foreach (var s in all)
                if (pick < 0 || s.Summary.SavedAtUtc < all[pick].Summary.SavedAtUtc) pick = s.Index;
            return pick;
        }

        /// <summary>
        /// Saves a school into a slot. The school is captured right away; the file is written on a
        /// worker unless <paramref name="wait"/> (quitting) asks for it to be finished now.
        /// </summary>
        public static void Write(int slot, Simulation sim, bool wait = false)
        {
            if (slot < 0 || slot >= Count || sim == null) return;
            string text;
            try
            {
                text = SaveGame.Capture(sim, DateTime.UtcNow, Application.version);
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't save the school: {e}");
                Finished.Enqueue((slot, false));
                return;
            }

            // The cards show the new state straight away; a failed write re-reads the slot.
            if (_slots != null) _slots[slot] = new SaveSlot { Index = slot, Summary = SaveSummary.Of(sim, DateTime.UtcNow) };

            string path = PathOf(slot);
            lock (WriteLock)
                _writing = _writing.ContinueWith(_ => WriteFile(slot, path, text), TaskScheduler.Default);
            if (wait) Flush();
        }

        static void WriteFile(int slot, string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    SaveGame.Compress(text, file);
                    file.Flush(true);
                }
                if (File.Exists(path))
                {
                    string backup = BackupOf(path);
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                File.Move(temp, path);
                Finished.Enqueue((slot, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't write {path}: {e}");
                Finished.Enqueue((slot, false));
            }
        }

        /// <summary>Waits for saves still being written.</summary>
        public static void Flush()
        {
            Task pending;
            lock (WriteLock) pending = _writing;
            try
            {
                pending.Wait();
            }
            catch (AggregateException e)
            {
                Debug.LogError($"A save failed: {e.InnerException}");
            }
        }

        /// <summary>Saves that finished since the last call (slot, ok), for the game thread.</summary>
        public static bool TryTakeFinished(out int slot, out bool ok)
        {
            if (Finished.TryDequeue(out var done))
            {
                slot = done.slot;
                ok = done.ok;
                if (!ok && _slots != null) _slots[slot] = ReadSlot(slot);
                return true;
            }
            slot = -1;
            ok = false;
            return false;
        }

        /// <summary>Loads a slot's school, falling back to its backup if the file itself is damaged.</summary>
        public static Simulation Load(int slot)
        {
            Flush();
            string path = PathOf(slot);
            try
            {
                return LoadFile(path);
            }
            catch (Exception e) when (File.Exists(BackupOf(path)))
            {
                Debug.LogWarning($"Save {path} couldn't be loaded ({e.Message}); using its backup.");
                return LoadFile(BackupOf(path));
            }
        }

        static Simulation LoadFile(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return SaveGame.ReadFile(file, out _);
        }

        public static void Delete(int slot)
        {
            Flush();
            string path = PathOf(slot);
            foreach (var p in new[] { path, BackupOf(path), path + ".tmp" })
            {
                try
                {
                    if (File.Exists(p)) File.Delete(p);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Couldn't delete {p}: {e.Message}");
                }
            }
            if (_slots != null) _slots[slot] = ReadSlot(slot);
        }
    }
}
