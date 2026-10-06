using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// How a student or staff member looks: skin, hair, outfit and a few accessories, derived from
    /// their id so the same person always looks the same. The HUD portraits and the people walking
    /// around the campus both draw from this, so a face in a file matches the kid on the map.
    /// </summary>
    public readonly struct PersonLook
    {
        public static readonly Color[] SkinTones =
        {
            InkIcons.Hex(0xFFF6EE), InkIcons.Hex(0xF9E1CF), InkIcons.Hex(0xEDC8A8),
            InkIcons.Hex(0xD9A47E), InkIcons.Hex(0xB57A54), InkIcons.Hex(0x83563B),
        };

        public static readonly Color[] HairColors =
        {
            InkIcons.Ink, InkIcons.Hex(0x3B2A22), InkIcons.Hex(0x6B4430), InkIcons.Hex(0x9A6A45),
            InkIcons.Marker, InkIcons.Coral, InkIcons.Hex(0x2F6FD0),
        };

        /// <summary>Uniform colour by grade, from the design: grade 1 sky to grade 4 navy.</summary>
        public static readonly Color[] GradeColors =
        {
            InkIcons.Sky, InkIcons.Mid, InkIcons.Hex(0x2F6FD0), InkIcons.Ink,
        };

        public const int HairStyleCount = 8;

        public readonly Color Skin, Hair, Outfit;
        public readonly int HairStyle;        // 0 short, 1 bob, 2 bun, 3 curls, 4 pigtails, 5 spiky, 6 ponytail, 7 long
        public readonly bool Glasses, Freckles, Bow, Headband;
        public readonly bool Adult;
        public readonly StaffRole Role;

        PersonLook(Color skin, Color hair, Color outfit, int style, bool glasses, bool freckles, bool bow, bool headband, bool adult, StaffRole role)
        {
            Skin = skin;
            Hair = hair;
            Outfit = outfit;
            HairStyle = style;
            Glasses = glasses;
            Freckles = freckles;
            Bow = bow;
            Headband = headband;
            Adult = adult;
            Role = role;
        }

        public static PersonLook ForStudent(Student s)
        {
            uint h = Mix((uint)s.Id * 2654435761u + 17u);
            int style = (int)(h % HairStyleCount);
            var hair = HairColors[(int)(h / 8 % 5)];                          // natural colours mostly
            if (h / 997 % 11 == 0) hair = HairColors[5 + (int)(h / 31 % 2)]; // the odd ginger or dyed blue
            var skin = SkinTones[(int)(h / 61 % (uint)SkinTones.Length)];
            int grade = Mathf.Clamp(s.Grade, 1, 4);
            bool glasses = s.Has(StudentTrait.Bookworm) || h / 7 % 9 == 0;
            bool freckles = h / 13 % 6 == 0;
            bool bow = (style == 4 || style == 6 || style == 1) && h / 17 % 3 == 0;
            bool band = !bow && style != 2 && h / 19 % 7 == 0;
            return new PersonLook(skin, hair, GradeColors[grade - 1], style, glasses, freckles, bow, band, false, StaffRole.Teacher);
        }

        public static PersonLook ForStaff(StaffMember m)
        {
            uint h = Mix((uint)m.Id * 2246822519u + 101u);
            int style = (int)(h % HairStyleCount);
            if (style == 4) style = 1;                                         // no pigtails for grown-ups
            var hair = HairColors[(int)(h / 8 % 4)];
            if (m.Experience > 18f && h / 3 % 2 == 0) hair = InkIcons.Hex(0xC9D3E0); // some grey
            var skin = SkinTones[(int)(h / 61 % (uint)SkinTones.Length)];
            bool glasses = m.Role == StaffRole.Teacher && h / 5 % 2 == 0 || m.Role == StaffRole.Counselor;
            return new PersonLook(skin, hair, OutfitFor(m.Role), style, glasses, false, false, false, true, m.Role);
        }

        public static Color OutfitFor(StaffRole role) => role switch
        {
            StaffRole.Teacher => InkIcons.Hex(0xFFFFFF),
            StaffRole.Coach => InkIcons.Coral,
            StaffRole.Custodian => InkIcons.Marker,
            StaffRole.Maintenance => InkIcons.Hex(0xF2B84B),
            StaffRole.Chef => InkIcons.Hex(0xFFFFFF),
            StaffRole.Nurse => InkIcons.Hex(0xFFE3DA),
            StaffRole.Security => InkIcons.Hex(0x3F5F93),
            StaffRole.Counselor => InkIcons.Pale,
            _ => InkIcons.Ink,                                                 // administrators wear suits
        };

        static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }

    /// <summary>How a face looks right now.</summary>
    public enum Expression { Joyful, Happy, Calm, Glum, Sad, Stressed, Sleepy }

    public static class Expressions
    {
        public static Expression For(Student s)
        {
            if (s.Stress > 75f) return Expression.Stressed;
            if (s.Happiness >= 75f) return Expression.Joyful;
            if (s.Happiness >= 58f) return Expression.Happy;
            if (s.Happiness >= 42f) return Expression.Calm;
            if (s.Happiness >= 25f) return Expression.Glum;
            return Expression.Sad;
        }

        public static Expression For(StaffMember m)
        {
            if (m.Energy < 25f) return Expression.Sleepy;
            if (m.Morale >= 72f) return Expression.Joyful;
            if (m.Morale >= 55f) return Expression.Happy;
            if (m.Morale >= 40f) return Expression.Calm;
            return m.Morale >= 25f ? Expression.Glum : Expression.Sad;
        }
    }
}
