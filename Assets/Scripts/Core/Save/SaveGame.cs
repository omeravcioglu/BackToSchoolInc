using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SchoolSim
{
    /// <summary>
    /// A whole school as JSON: the game state plus the random number generator, so a loaded school
    /// carries on exactly as it would have. A save file is gzip-compressed text with two lines: a
    /// small header with the <see cref="SaveSummary"/> (all the Load screen reads) and the school.
    /// The Unity layer decides where files go.
    /// </summary>
    public static class SaveGame
    {
        public const string Format = "school-game";
        public const int Version = 1;

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>The school as one line of JSON. Two schools in the same state give the same text.</summary>
        public static string Write(Simulation sim)
        {
            var sb = new StringBuilder(1 << 20);
            var writer = new SaveSerializer.Writer(sb);
            sb.Append("{\"format\":\"").Append(Format).Append("\",\"version\":").Append(Version).Append(",\"rng\":");
            writer.Value(sim.Rng, typeof(SimRandom));
            sb.Append(",\"state\":");
            writer.Value(sim.State, typeof(GameState));
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Reads a school written by <see cref="Write"/>, or a whole save file's text (header line
        /// included). Throws <see cref="FormatException"/> for anything that isn't a save.
        /// </summary>
        public static Simulation Read(string text)
        {
            if (text == null) throw new FormatException("This isn't a Back To School Inc. save.");
            int newline = text.IndexOf('\n');
            if (newline >= 0)
            {
                ParseHeader(text.Substring(0, newline));
                text = text.Substring(newline + 1);
            }

            JsonNode root;
            try
            {
                root = Json.Parse(text);
            }
            catch (Exception e) when (!(e is FormatException))
            {
                throw new FormatException("This save file is damaged.", e);
            }
            CheckFormat(root);
            var state = root["state"];
            if (state == null || state.Type != JsonNode.Kind.Object) throw new FormatException("This save file has no school in it.");

            var gameState = new GameState();
            SimRandom rng;
            try
            {
                var reader = new SaveSerializer.Reader(root);
                reader.Populate(state, gameState);
                rng = reader.Read(root["rng"], typeof(SimRandom), null) as SimRandom;
                reader.Finish();
            }
            catch (Exception e) when (!(e is FormatException))
            {
                throw new FormatException("This save file is damaged.", e);
            }
            if (rng == null) throw new FormatException("This save file is damaged.");
            return Simulation.FromSave(gameState, rng);
        }

        /// <summary>The text of a save file: the header line, then the school. Must run on the game's thread.</summary>
        public static string Capture(Simulation sim, DateTime savedAtUtc, string gameVersion = null)
        {
            var summary = SaveSummary.Of(sim, savedAtUtc);
            summary.GameVersion = gameVersion ?? "";
            var sb = new StringBuilder(4096);
            sb.Append("{\"format\":\"").Append(Format).Append("\",\"version\":").Append(Version).Append(",\"summary\":");
            new SaveSerializer.Writer(sb).Value(summary, typeof(SaveSummary));
            sb.Append("}\n");
            sb.Append(Write(sim));
            return sb.ToString();
        }

        /// <summary>Compresses a captured save into a stream. Safe to run on a worker thread.</summary>
        public static void Compress(string fileText, Stream output)
        {
            using (var zip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            using (var writer = new StreamWriter(zip, Utf8, 1 << 16))
                writer.Write(fileText);
        }

        /// <summary>Reads only a save file's header (fast: the school itself isn't touched).</summary>
        public static SaveSummary ReadSummary(Stream input)
        {
            using (var reader = OpenText(input))
                return ParseHeader(reader.ReadLine());
        }

        /// <summary>Reads a save file (compressed or plain text) back into a running simulation.</summary>
        public static Simulation ReadFile(Stream input, out SaveSummary summary)
        {
            using (var reader = OpenText(input))
            {
                summary = ParseHeader(reader.ReadLine());
                return Read(reader.ReadToEnd());
            }
        }

        static StreamReader OpenText(Stream input)
        {
            // Gzip files start with 1F 8B; anything else is read as plain text.
            int b1 = input.ReadByte(), b2 = input.ReadByte();
            if (b1 < 0 || b2 < 0) throw new FormatException("This save file is empty.");
            if (input.CanSeek) input.Seek(-2, SeekOrigin.Current);
            else throw new NotSupportedException("Save files are read from seekable streams.");
            var stream = b1 == 0x1F && b2 == 0x8B ? new GZipStream(input, CompressionMode.Decompress, leaveOpen: true) : input;
            return new StreamReader(stream, Utf8, false, 1 << 16, leaveOpen: stream == input);
        }

        static SaveSummary ParseHeader(string line)
        {
            if (string.IsNullOrEmpty(line)) throw new FormatException("This isn't a Back To School Inc. save.");
            try
            {
                var root = Json.Parse(line);
                CheckFormat(root);
                var node = root["summary"];
                if (node == null || node.Type != JsonNode.Kind.Object) throw new FormatException("This save file has no summary.");
                var reader = new SaveSerializer.Reader(root);
                var summary = (SaveSummary)reader.Read(node, typeof(SaveSummary), null);
                reader.Finish();
                return summary;
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception e)
            {
                // Garbage can trip the parser in other ways (a cut-off escape, a bad number).
                throw new FormatException("This isn't a Back To School Inc. save.", e);
            }
        }

        static void CheckFormat(JsonNode root)
        {
            if (root == null || root.Type != JsonNode.Kind.Object || root["format"]?.Text != Format)
                throw new FormatException("This isn't a Back To School Inc. save.");
            if (!int.TryParse(root["version"]?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                throw new FormatException("This save file is damaged.");
            if (version > Version) throw new FormatException("This save is from a newer version of the game.");
        }
    }

    /// <summary>What the Load screen shows about a save without reading the whole school.</summary>
    public sealed class SaveSummary
    {
        public string GameVersion;
        public string SavedAt;               // round-trip date-time text, UTC
        public string SchoolName;
        public int Emblem;
        public int CrestColor;
        public int Day;
        public int Level;
        public float Prestige;
        public int Students;
        public float Cash;
        public int Rank;
        public bool GameOver;

        // A sketch of the campus for the Load screen's photo: owned land and every room.
        public int ParcelsX, ParcelsY, ParcelSize;
        public bool[] Parcels;               // owned land, row by row, ParcelsX * ParcelsY
        public List<MiniRoom> Rooms = new List<MiniRoom>();

        public struct MiniRoom
        {
            public RoomType Type;
            public int X, Y, W, H;
            public bool Building;            // still under construction
        }

        public DateTime SavedAtUtc =>
            DateTime.TryParse(SavedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) ? t.ToUniversalTime() : DateTime.MinValue;

        public static SaveSummary Of(Simulation sim, DateTime savedAtUtc)
        {
            var s = sim.State;
            var map = s.Campus;
            var summary = new SaveSummary
            {
                SavedAt = savedAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                SchoolName = s.SchoolName,
                Emblem = s.Emblem,
                CrestColor = s.CrestColor,
                Day = s.Day,
                Level = s.Progress.SchoolLevel,
                Prestige = s.Prestige,
                Students = s.Students.Count,
                Cash = s.Finance.Cash,
                Rank = s.NationalRank,
                GameOver = s.GameOver,
                ParcelsX = map.ParcelsX,
                ParcelsY = map.ParcelsY,
                ParcelSize = CampusMap.ParcelSize,
                Parcels = new bool[map.ParcelsX * map.ParcelsY],
            };
            for (int py = 0; py < map.ParcelsY; py++)
                for (int px = 0; px < map.ParcelsX; px++)
                    summary.Parcels[py * map.ParcelsX + px] = map.IsParcelOwned(px, py);
            foreach (var r in s.Rooms)
            {
                var f = r.Footprint;
                summary.Rooms.Add(new MiniRoom { Type = r.Type, X = f.X, Y = f.Y, W = f.W, H = f.H, Building = !r.IsBuilt(s.Day) });
            }
            return summary;
        }
    }
}
