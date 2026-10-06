using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The design canvas's illustrations as <see cref="InkDrawing"/>s: the title-screen school on
    /// its hill, the kids, the level-up confetti. Coordinates are the canvas's (1440 x 810).
    /// </summary>
    public static class Illustrations
    {
        static readonly Color Ink = InkIcons.Ink;
        static readonly Color Paper = InkIcons.Paper;
        static readonly Color White = Color.white;
        static readonly Color Marker = InkIcons.Marker;
        static readonly Color Sky = InkIcons.Sky;
        static readonly Color Mid = InkIcons.Mid;
        static readonly Color Pale = InkIcons.Pale;
        static readonly Color Coral = InkIcons.Coral;
        static readonly Color Blue = InkIcons.Hex(0x2F6FD0);
        static readonly Color Bush = InkIcons.Hex(0xBFDCF6);
        static readonly Color CheekPink = new Color(1f, 0.549f, 0.431f, 0.55f);

        static InkDrawing _title, _confetti;

        public static InkDrawing Title => _title ??= BuildTitle();
        public static InkDrawing Confetti => _confetti ??= BuildConfetti();

        static InkDrawing BuildTitle()
        {
            var d = new InkDrawing(1440f, 810f);

            // Sparkles
            foreach (var s in new[]
            {
                "M842 66Q843 77 854 78Q843 79 842 90Q841 79 830 78Q841 77 842 66Z",
                "M1226 169Q1227 177 1235 178Q1227 179 1226 187Q1225 179 1217 178Q1225 177 1226 169Z",
                "M700 192Q701 199 708 200Q701 201 700 208Q699 201 692 200Q699 199 700 192Z",
                "M1404 228Q1405 235 1412 236Q1405 237 1404 244Q1403 237 1396 236Q1403 235 1404 228Z",
            })
                d.Shape(s, Marker, Ink, 1.6f);

            // Clouds: an outline pass, then a fill pass so the puffs merge into one outline.
            float[][] clouds =
            {
                new[] { 728f, 128f, 24f, 758f, 110f, 30f, 792f, 124f, 22f, 760f, 136f, 56f, 14f },
                new[] { 1114f, 70f, 17f, 1136f, 60f, 21f, 1157f, 71f, 15f, 1136f, 78f, 40f, 9f },
                new[] { 1152f, 256f, 21f, 1180f, 244f, 27f, 1209f, 257f, 19f, 1181f, 266f, 50f, 11f },
                new[] { 622f, 306f, 15f, 642f, 298f, 19f, 661f, 307f, 13f, 642f, 314f, 33f, 8f },
            };
            foreach (bool outline in new[] { true, false })
                foreach (var c in clouds)
                {
                    for (int i = 0; i < 9; i += 3) d.Circle(c[i], c[i + 1], c[i + 2], Paper, outline ? Ink : Color.clear, outline ? 5f : 0f);
                    d.Ellipse(c[9], c[10], c[11], c[12], Paper, outline ? Ink : Color.clear, outline ? 5f : 0f);
                }

            // Sun with a face
            d.Line("M1368 118H1386M1356.9 152.1L1371.5 162.7M1327.9 173.2L1333.5 190.3M1292.1 173.2L1286.5 190.3M1263.1 152.1L1248.5 162.7" +
                   "M1252 118H1234M1263.1 83.9L1248.5 73.3M1292.1 62.8L1286.5 45.7M1327.9 62.8L1333.5 45.7M1356.9 83.9L1371.5 73.3", Ink, 3f);
            d.Circle(1310f, 118f, 46f, Marker, Ink, 3f);
            d.Circle(1296f, 112f, 3.6f, Ink);
            d.Circle(1324f, 112f, 3.6f, Ink);
            d.Ellipse(1287f, 125f, 6f, 3.5f, CheekPink);
            d.Ellipse(1333f, 125f, 6f, 3.5f, CheekPink);
            d.Line("M1301 126Q1310 135 1319 126", Ink, 3f);

            // Birds, paper plane, kite
            d.Line("M1036 150q8-8 16 0q8-8 16 0M1080 132q6-6 12 0q6-6 12 0", Ink, 2.2f);
            d.Dashed("M612 256C670 226 716 292 756 258C786 232 752 212 738 234C724 258 800 262 876 215", Mid, 2.4f, 6f, 8f, round: true);
            d.Shape("M878 214L934 190L904 226Z", Paper, Ink, 2.2f);
            d.Line("M904 226L908 210L934 190M878 214L908 210", Ink, 1.8f);
            d.Dashed("M1348 320C1336 440 1300 560 1281 671", Ink, 1.4f, 3f, 4f);
            d.Line("M1352 346C1340 364 1364 380 1350 398C1338 414 1360 426 1348 444", Ink, 1.8f);
            d.Shape("M1352 262L1380 300L1352 346L1324 300Z", Mid, Ink, 2.6f);
            d.Line("M1352 265V343M1327 300H1377", Paper, 1.8f);
            d.Shape("M1338 364L1349 371L1338 378ZM1360 364L1349 371L1360 378ZM1340 400L1350 407L1340 414ZM1360 400L1350 407L1360 414Z", Marker, Ink, 1.3f);

            // Hill and lawn, hatched like a pencil sketch
            const string hill = "M420 712C560 636 780 532 1000 528C1180 526 1330 566 1460 616V830H420Z";
            const string lawn = "M-20 716C300 690 700 700 1000 712C1200 718 1350 704 1460 694V830H-20Z";
            d.Shape(hill, InkIcons.Hex(0xE4F0FC));
            d.Hatch(hill, InkIcons.Hex(0xCFE2F6), 2f, 9f, 128f);
            d.Line("M420 712C560 636 780 532 1000 528C1180 526 1330 566 1460 616", Ink, 3f);
            d.Shape(lawn, InkIcons.Hex(0xD9EAFB));
            d.Hatch(lawn, InkIcons.Hex(0xCFE2F6), 2f, 9f, 128f);
            d.Dashed("M-20 716C300 690 700 700 1000 712C1200 718 1350 704 1460 694", InkIcons.Hex(0x9CC3EC), 2.5f, 12f, 8f);

            // The path to the front door
            d.Line("M1000 670C1000 724 952 744 936 830", Ink, 46f);
            d.Line("M1000 670C1000 724 952 744 936 830", InkIcons.Hex(0xF8FBFF), 40f);
            d.Dashed("M998 692C996 732 956 752 944 822", InkIcons.Hex(0xC3DAF3), 3f, 1f, 14f, round: true);

            // School: wings, main block, roof with shingles
            d.Rect(690f, 470f, 110f, 182f, Paper, Ink, 3f);
            d.Shape("M680 474L700 440H790L808 474Z", Blue, Ink, 3f);
            d.Rect(1200f, 470f, 110f, 182f, Paper, Ink, 3f);
            d.Shape("M1192 474L1210 440H1300L1320 474Z", Blue, Ink, 3f);
            d.Rect(800f, 400f, 400f, 252f, Paper, Ink, 3.5f);
            d.Line("M820 590h16v8h-16zM842 602h16v8h-16zM1150 588h16v8h-16zM1172 600h16v8h-16zM706 604h14v7h-14zM1276 606h14v7h-14z",
                new Color(Ink.r, Ink.g, Ink.b, 0.6f), 1.4f);
            d.Shape("M780 404L1000 322L1220 404Z", Blue, Ink, 3.5f);
            Shingles(d);

            // Windows
            float[][] windows =
            {
                new[] { 826f, 424f, 46f, 54f, 0f }, new[] { 886f, 424f, 46f, 54f, 1f }, new[] { 1068f, 424f, 46f, 54f, 0f },
                new[] { 1128f, 424f, 46f, 54f, 0f }, new[] { 826f, 500f, 46f, 54f, 0f }, new[] { 886f, 500f, 46f, 54f, 0f },
                new[] { 1068f, 500f, 46f, 54f, 0f }, new[] { 1128f, 500f, 46f, 54f, 0f }, new[] { 722f, 494f, 46f, 52f, 0f },
                new[] { 722f, 574f, 46f, 52f, 0f }, new[] { 1232f, 494f, 46f, 52f, 0f }, new[] { 1232f, 574f, 46f, 52f, 1f },
            };
            foreach (var w in windows) d.Rect(w[0], w[1], w[2], w[3], w[4] > 0f ? Marker : Sky, Ink, 2.5f, 9f);
            d.Line("M849 424V478M826 451H872M909 424V478M886 451H932M1091 424V478M1068 451H1114M1151 424V478M1128 451H1174" +
                   "M849 500V554M826 527H872M909 500V554M886 527H932M1151 500V554M1128 527H1174M745 494V546M722 520H768" +
                   "M745 574V626M722 600H768M1255 494V546M1232 520H1278M1255 574V626M1232 600H1278", Ink, 2f, round: false);
            d.Line("M833 437l9-9M893 437l9-9M1075 437l9-9M1135 437l9-9M833 513l9-9M893 513l9-9M1135 513l9-9M729 507l9-9M729 587l9-9M1239 507l9-9", Paper, 2.2f);

            // A student peeking out of a window
            d.Circle(1091f, 538f, 12f, White, Ink, 2f);
            d.Shape("M1079.6 536.4c.6-7 5.4-10.4 11.4-10.4s10.8 3.4 11.4 10.4c-3.2-2.6-7-3.8-11.4-3.8s-8.2 1.2-11.4 3.8z", Coral, Ink, 1.6f);
            d.Circle(1086.5f, 540f, 1.6f, Ink);
            d.Circle(1095.5f, 540f, 1.6f, Ink);
            d.Line("M1088 545.5q3 2.4 6 0", Ink, 1.5f);
            d.Circle(1106f, 532f, 3.5f, White, Ink, 1.4f);

            // Crest, centre window, door and steps
            d.Shape("M984 436h32v12c0 12-8 19-16 22c-8-3-16-10-16-22z", Pale, Ink, 2.2f);
            d.Shape("M994 450h12c0 6-3 10-6 12c-3-2-6-6-6-12z", Marker, Ink, 1.4f);
            d.Shape("M992.5 450c0-3 3.5-5 7.5-5s7.5 2 7.5 5z", Mid, Ink, 1.4f);
            d.Shape("M976 544V510a24 24 0 0 1 48 0V544Z", Sky, Ink, 2.5f);
            d.Line("M1000 486V544M976 520H1024", Ink, 2f, round: false);
            d.Shape("M966 652V600a34 34 0 0 1 68 0V652Z", Mid, Ink, 3f);
            d.Line("M1000 566V652", Ink, 2f, round: false);
            d.Circle(992f, 618f, 3f, Marker, Ink, 1.4f);
            d.Circle(1008f, 618f, 3f, Marker, Ink, 1.4f);
            d.Rect(950f, 652f, 100f, 10f, Pale, Ink, 2.5f);
            d.Rect(940f, 662f, 120f, 10f, Pale, Ink, 2.5f);

            // Bell tower with a clock
            d.Rect(948f, 236f, 104f, 184f, Paper, Ink, 3.5f);
            d.Shape("M936 240L1000 170L1064 240Z", Mid, Ink, 3.5f);
            d.Line("M1000 170V116", Ink, 3f);
            d.Shape("M1001 118L1038 128L1001 140Z", Marker, Ink, 2f);
            d.Circle(1000f, 286f, 30f, Paper, Ink, 3f);
            d.Line("M1000 260v6M1026 286h-6M1000 312v-6M974 286h6", Ink, 2.2f);
            d.Line("M1000 286V268M1000 286l13 7", Ink, 3f);
            d.Circle(1000f, 286f, 3f, Ink);
            d.Shape("M982 398V370a18 18 0 0 1 36 0V398Z", Ink, Ink, 2.5f);
            d.Shape("M991 390c0-11 4-17 9-17s9 6 9 17z", Marker, Ink, 1.6f);
            d.Circle(1000f, 392f, 2.4f, Marker, Ink, 1.2f);

            // Bushes
            float[][] bushes =
            {
                new[] { 814f, 652f, 14f }, new[] { 838f, 656f, 12f }, new[] { 862f, 652f, 14f }, new[] { 1138f, 652f, 14f },
                new[] { 1162f, 656f, 12f }, new[] { 1186f, 652f, 14f }, new[] { 704f, 654f, 11f }, new[] { 1296f, 654f, 11f },
            };
            foreach (var b in bushes) d.Circle(b[0], b[1], b[2], Bush, Ink, 4.4f);
            foreach (var b in bushes) d.Circle(b[0], b[1], b[2], Bush);

            // The little oak with a swing
            d.Ellipse(618f, 670f, 74f, 12f, InkIcons.Hex(0xCBE0F6));
            d.Shape("M600 672c4-40 4-92 0-142h30c-4 50-4 102 0 142z", Paper, Ink, 2.5f);
            d.Line("M608 566C586 560 562 556 538 560", Ink, 6f);
            d.Line("M544 559V636M590 563V636", Ink, 2f, round: false);
            d.Rect(534f, 634f, 64f, 10f, Marker, Ink, 2f, 2f);
            float[][] leaves =
            {
                new[] { 566f, 470f, 46f }, new[] { 622f, 440f, 56f }, new[] { 678f, 468f, 46f }, new[] { 592f, 512f, 40f }, new[] { 652f, 514f, 42f },
            };
            foreach (var l in leaves) d.Circle(l[0], l[1], l[2], Bush, Ink, 5f);
            foreach (var l in leaves) d.Circle(l[0], l[1], l[2], Bush);
            d.Line("M586 420c8-10 22-12 32-6M648 430c9-4 18-2 24 5M540 468c2-9 8-14 16-15", Paper, 2.8f);
            foreach (var a in new[] { new[] { 590f, 470f }, new[] { 642f, 486f }, new[] { 624f, 430f }, new[] { 672f, 506f }, new[] { 560f, 505f } })
                d.Ellipse(a[0], a[1], 4.5f, 5.5f, Marker, Ink, 1.5f);

            // Mailbox with the flag up, a football, flowers
            d.Rect(1052f, 718f, 8f, 40f, Paper, Ink, 2f);
            d.RotatedRect(1040f, 680f, 22f, 14f, 2f, -14f, 1051f, 687f, Paper, Ink, 1.6f);
            d.Shape("M1032 720V704a16 16 0 0 1 16-16h16a16 16 0 0 1 16 16V720Z", Blue, Ink, 2.5f);
            d.Line("M1080 708V682", Ink, 2.2f);
            d.Shape("M1080 683h14v9h-14z", Coral, Ink, 1.8f);
            d.Circle(1214f, 716f, 11f, Paper, Ink, 2.2f);
            d.Shape("M1214 710.5l4.6 3.3-1.8 5.4h-5.6l-1.8-5.4z", Ink);
            foreach (var f in new[] { new[] { 756f, 724f }, new[] { 804f, 738f }, new[] { 1170f, 742f }, new[] { 1246f, 728f }, new[] { 1392f, 722f }, new[] { 512f, 728f } })
            {
                d.Circle(f[0] - 5f, f[1], 4f, Sky, Ink, 1.2f);
                d.Circle(f[0] + 5f, f[1], 4f, Sky, Ink, 1.2f);
                d.Circle(f[0], f[1] - 5f, 4f, Sky, Ink, 1.2f);
                d.Circle(f[0], f[1] + 5f, 4f, Sky, Ink, 1.2f);
                d.Circle(f[0], f[1], 3f, Marker, Ink, 1f);
            }

            // Picket fence with a gap for the path
            d.Rect(-10f, 766f, 892f, 8f, Pale, Ink, 2f);
            d.Rect(-10f, 790f, 892f, 8f, Pale, Ink, 2f);
            d.Rect(1012f, 766f, 440f, 8f, Pale, Ink, 2f);
            d.Rect(1012f, 790f, 440f, 8f, Pale, Ink, 2f);
            for (int m = 0; m <= 55; m++)
            {
                if (m > 33 && m < 39) continue;
                float x = m * 26f;
                d.Shape($"M{x + 6f} 816V766L{x + 13f} 756L{x + 20f} 766V816Z", Paper, Ink, 2f);
            }

            // Kids: on the swing, waving, with a football, flying the kite
            d.Place(Kid(Sky, Marker, KidFace.Happy), 542f, 576f, 1.5f);
            d.Place(Kid(Mid, Ink, KidFace.Open, wave: true), 872f, 648f, 1.5f);
            d.Place(Kid(Blue, Coral, KidFace.Smile), 1150f, 668f, 1.5f);
            d.Place(Kid(Ink, Marker, KidFace.HappyOpen, kite: true), 1276f, 640f, 1.5f);
            return d;
        }

        static void Shingles(InkDrawing d)
        {
            var color = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
            var sb = new System.Text.StringBuilder();
            for (float bottom = 336f; bottom <= 404f; bottom += 12f)
            {
                float top = bottom - 5f;
                float left = 780f + (404f - top) * (220f / 82f), right = 1220f - (404f - top) * (220f / 82f);
                for (float x = Mathf.Ceil(left / 11f) * 11f; x + 11f <= right; x += 11f)
                    sb.Append($"M{x} {bottom}c3-7 8-7 11 0");
            }
            if (sb.Length > 0) d.Line(sb.ToString(), color, 1.5f);
        }

        enum KidFace { Happy, Open, Smile, HappyOpen }

        /// <summary>A kid from the canvas, in its own 32 x 40 box.</summary>
        static InkDrawing Kid(Color shirt, Color hair, KidFace face, bool wave = false, bool kite = false)
        {
            var d = new InkDrawing(32f, 40f);
            if (wave)
            {
                d.Line("M24 31l6-8", Ink, 4f);
                d.Line("M24 31l6-8", Mid, 2.2f);
                d.Circle(30.4f, 22.4f, 2.3f, White, Ink, 1.3f);
            }
            if (kite)
            {
                d.Line("M8 31l-5.4-9", Ink, 4f);
                d.Circle(2.4f, 21.4f, 2.3f, White, Ink, 1.3f);
            }
            d.Shape("M5 39c0-7.5 4.8-11.5 11-11.5S27 31.5 27 39z", shirt, Ink, 1.8f);
            d.Circle(16f, 16f, 10f, White, Ink, 1.8f);
            d.Shape("M6.3 14.6C6.8 8.8 11 6 16 6s9.2 2.8 9.7 8.6c-2.6-2.2-6-3.2-9.7-3.2s-7.1 1-9.7 3.2z", hair, Ink, 1.5f);
            bool closed = face == KidFace.Happy || face == KidFace.HappyOpen;
            if (closed) d.Line("M10.8 17.8q1.5-1.6 3 0M18.2 17.8q1.5-1.6 3 0", Ink, 1.3f);
            else
            {
                d.Circle(12.4f, 17.4f, 1.4f, Ink);
                d.Circle(19.6f, 17.4f, 1.4f, Ink);
            }
            float cheekY = closed ? 20.6f : 20.4f;
            d.Ellipse(9.8f, cheekY, 1.9f, 1.1f, CheekPink);
            d.Ellipse(22.2f, cheekY, 1.9f, 1.1f, CheekPink);
            switch (face)
            {
                case KidFace.Happy: d.Line("M13.8 21.2c1.2 1.5 3.2 1.5 4.4 0", Ink, 1.3f); break;
                case KidFace.Smile: d.Line("M13.8 21c1.2 1.4 3.2 1.4 4.4 0", Ink, 1.3f); break;
                case KidFace.Open: d.Shape("M14 20.4h4c0 1.7-.9 2.7-2 2.7s-2-1-2-2.7z", Ink); break;
                default: d.Shape("M14 20.6h4c0 1.7-.9 2.7-2 2.7s-2-1-2-2.7z", Ink); break;
            }
            return d;
        }

        static InkDrawing BuildConfetti()
        {
            var d = new InkDrawing(1440f, 810f);
            void Bit(float x, float y, float w, float h, float deg, Color c) =>
                d.RotatedRect(x, y, w, h, 2f, deg, x + w / 2f, y + h / 2f, c, Ink, 1.8f);
            Bit(330, 190, 16, 9, -24, Marker); Bit(300, 330, 14, 8, 18, Sky); Bit(340, 470, 16, 9, -40, Coral); Bit(290, 600, 14, 8, 30, Mid);
            Bit(1100, 200, 16, 9, 22, Sky); Bit(1140, 340, 14, 8, -30, Marker); Bit(1090, 480, 16, 9, 40, Mid); Bit(1150, 620, 14, 8, -18, Coral);
            Bit(480, 112, 14, 8, -12, Sky); Bit(960, 116, 14, 8, 28, Marker); Bit(520, 730, 16, 9, 20, Marker); Bit(900, 736, 16, 9, -26, Sky);
            foreach (var c in new[]
            {
                (360f, 260f, 6f, Mid), (316f, 410f, 5f, Marker), (368f, 560f, 6f, Sky), (1120f, 270f, 6f, Coral), (1160f, 420f, 5f, Sky),
                (1110f, 560f, 6f, Marker), (560f, 120f, 5f, Coral), (900f, 130f, 5f, Mid), (700f, 752f, 5f, Coral),
            })
                d.Circle(c.Item1, c.Item2, c.Item3, c.Item4, Ink, 1.8f);
            d.Line("M270 250c6-6 12 0 18-6s12 0 18-6", Marker, 3.5f);
            d.Line("M1170 270c6-6 12 0 18-6s12 0 18-6", Sky, 3.5f);
            d.Line("M280 700c6-6 12 0 18-6s12 0 18-6", Mid, 3.5f);
            d.Line("M1160 700c6-6 12 0 18-6s12 0 18-6", Coral, 3.5f);
            d.Shape("M420 150l3 7 7 3-7 3-3 7-3-7-7-3 7-3z", Marker, Ink, 1.8f);
            d.Shape("M1030 160l3 7 7 3-7 3-3 7-3-7-7-3 7-3z", Paper, Ink, 1.8f);
            d.Shape("M250 520l2.5 6 6 2.5-6 2.5-2.5 6-2.5-6-6-2.5 6-2.5z", Paper, Ink, 1.8f);
            d.Shape("M1200 520l2.5 6 6 2.5-6 2.5-2.5 6-2.5-6-6-2.5 6-2.5z", Marker, Ink, 1.8f);
            return d;
        }
    }

    /// <summary>The title screen's backdrop: graph paper and the school on its hill, cropped to fill the screen.</summary>
    public sealed class TitleBackdrop : InkPicture
    {
        static readonly Color PaperBlue = InkIcons.Hex(0xEEF5FD);
        static readonly Color Grid = InkIcons.Hex(0xDCE9F7);

        public TitleBackdrop() : base(Illustrations.Title, cover: true) { }

        protected override void Background(Painter2D p, float w, float h, Vector2 offset, float scale)
        {
            p.fillColor = PaperBlue;
            p.BeginPath();
            p.MoveTo(Vector2.zero);
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            p.Fill();

            float step = 32f * scale;
            if (step < 4f) return;
            p.strokeColor = Grid;
            p.lineWidth = Mathf.Max(1f, scale);
            p.lineCap = LineCap.Butt;
            p.BeginPath();
            for (float x = offset.x % step; x < w; x += step)
            {
                p.MoveTo(new Vector2(x, 0f));
                p.LineTo(new Vector2(x, h));
            }
            for (float y = offset.y % step; y < h; y += step)
            {
                p.MoveTo(new Vector2(0f, y));
                p.LineTo(new Vector2(w, y));
            }
            p.Stroke();
        }
    }
}
