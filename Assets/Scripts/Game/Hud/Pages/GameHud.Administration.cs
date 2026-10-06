using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The administration page: a desk for each director role (who sits there, how skilled they are,
    /// how many letters a day they take and which ones, or that the desk is empty), the office desks,
    /// and the standing orders directors follow when they answer letters for you.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly AdminRole[] AdmRoles =
        {
            AdminRole.HRManager, AdminRole.AdmissionsDirector, AdminRole.FinancialDirector,
            AdminRole.DormDirector, AdminRole.StudentAffairsDirector, AdminRole.AthleticDirector,
        };

        // A director's first answer to fights and parties: the option ids the policy stores.
        static readonly string[] AdmResponses = { "warning", "detention", "parents", "suspend" };
        static readonly string[] AdmResponseNames = { "Warning", "Detention", "Call parents", "Suspend" };

        static InkDrawing _admChair;

        readonly List<Action> _admOrders = new List<Action>();
        VisualElement _admRoot, _admBody, _admGrid, _admDeskNote;
        Label _admDeskCount, _admDeskText;
        int _admSignature;
        bool _admUnlocked;

        AdminPolicies AdmPolicy => S.AdminPolicies;

        partial void BuildAdminPage()
        {
            AdmDefineIcons();
            var scroll = MonPageScroll("adm-scroll");
            _admBody = Box("adm-body");
            scroll.Add(_admBody);
            _pageHost.Add(scroll);
            _admRoot = scroll;
            _admOrders.Clear();
        }

        partial void RefreshAdminPage(bool newDay)
        {
            if (_admRoot == null) return;
            bool unlocked = _sim.IsUnlocked(Feature.Administrators);
            if (_admBody.childCount == 0 || unlocked != _admUnlocked)
            {
                _admUnlocked = unlocked;
                AdmBuildBody();
                newDay = true;
            }
            if (!unlocked) return;
            int signature = AdmSignature();
            if (!newDay && signature == _admSignature) return;
            _admSignature = signature;
            AdmShowDesks();
            foreach (var refresh in _admOrders) refresh();
        }

        partial void ResetAdminPage()
        {
            _admRoot = null;
            _admOrders.Clear();
        }

        /// <summary>Changes when a director arrives, leaves, goes away or improves, or a desk or candidate appears.</summary>
        int AdmSignature()
        {
            int h = _sim.Staff.AdminDesks();
            foreach (var s in S.Staff)
            {
                if (s.AdminRole == AdminRole.None) continue;
                h = h * 31 + s.Id;
                h = h * 31 + Mathf.RoundToInt(s.Skill);
                h = h * 31 + (s.IsAway(S.Day) ? 1 : 0) + (s.LeavingDay >= 0 ? 2 : 0);
            }
            foreach (var c in S.Candidates)
                if (c.Staff.Role == StaffRole.Administrator) h = h * 31 + c.Staff.Id;
            return h * 31 + (_sim.IsUnlocked(Feature.SportsPrograms) ? 1 : 0);
        }

        void AdmBuildBody()
        {
            _admBody.Clear();
            _admOrders.Clear();
            if (!_admUnlocked)
            {
                _admBody.Add(AdmLocked());
                return;
            }
            _admBody.Add(AdmBuildOffice());
            _admBody.Add(AdmBuildOrders());
        }

        static VisualElement AdmLocked()
        {
            var card = Ink("adm-locked");
            var badge = Ink("adm-locked-badge");
            badge.Add(Icon("lock", "icon-34"));
            card.Add(badge);
            card.Add(Text($"Directors arrive at school level {Balance.UnlockLevel(Feature.Administrators)}", "font-hand", "adm-locked-title"));
            card.Add(Text("Until then, every letter is yours to answer.", "font-note", "adm-locked-note"));
            return card;
        }

        // --- The office -------------------------------------------------------------------------

        VisualElement AdmBuildOffice()
        {
            var office = Box("adm-office");
            var head = Box("adm-head");
            head.Add(Text("The office", "font-hand", "adm-title"));
            var desks = Ink("adm-desks");
            desks.Add(Icon("adm-desk", "icon-20"));
            _admDeskCount = Text("", "font-heavy", "adm-desks-text");
            desks.Add(_admDeskCount);
            head.Add(desks);
            office.Add(head);
            office.Add(Text("Directors answer the routine letters in their area a day after they arrive, the way your standing orders say. " +
                            "Weaker ones get some wrong.", "font-note", "adm-intro"));
            _admDeskNote = Box("adm-desk-note", "hidden");
            _admDeskText = Text("", "font-semi", "adm-desk-text");
            _admDeskNote.Add(_admDeskText);
            _admDeskNote.Add(Button("pencil", "Build an office", () =>
            {
                CloseSheet();
                CampusController.Instance?.BeginBuild(RoomType.Office);
            }, small: true));
            office.Add(_admDeskNote);
            _admGrid = Box("adm-grid");
            office.Add(_admGrid);
            return office;
        }

        void AdmShowDesks()
        {
            int used = _sim.CountStaff(StaffRole.Administrator, activeOnly: false), desks = _sim.Staff.AdminDesks();
            SetText(_admDeskCount, $"desks {used} / {desks}");
            int vacant = 0;
            _admGrid.Clear();
            foreach (var role in AdmRoles)
            {
                var admin = AdmHolder(role);
                if (admin == null) vacant++;
                _admGrid.Add(AdmDesk(role, admin));
            }
            // Hiring a director needs a free desk (the staff room refuses otherwise).
            Show(_admDeskNote, used >= desks && vacant > 0);
            SetText(_admDeskText, desks == 0
                ? "No office yet: directors need a desk in an Administration Office."
                : "Every desk is taken. A bigger office seats more directors.");
        }

        StaffMember AdmHolder(AdminRole role)
        {
            StaffMember holder = null;
            foreach (var s in S.Staff)
                if (s.AdminRole == role) holder = s;
            return holder;
        }

        VisualElement AdmDesk(AdminRole role, StaffMember admin)
        {
            var card = Ink("adm-desk", admin != null ? "adm-desk--filled" : "adm-desk--empty");
            var top = Box("adm-desk-top");
            var frame = Ink("adm-face");
            if (admin != null)
            {
                var face = new PersonPortrait();
                face.AddToClassList("adm-portrait");
                face.ShowStaff(admin);
                frame.Add(face);
            }
            else
            {
                var chair = new InkPicture(AdmChair());
                chair.AddToClassList("adm-chair");
                frame.Add(chair);
            }
            top.Add(frame);
            var who = Box("adm-who");
            var title = Text("", "adm-role");
            SetHandText(title, Names.AdminRoleTitle(role));
            who.Add(title);
            who.Add(Text(admin != null ? admin.Name : "vacant", "font-semi", "adm-name"));
            top.Add(who);
            card.Add(top);

            if (admin != null)
            {
                // From the administration system: a few letters a day by skill, and a skill-based chance to misjudge.
                int letters = Mathf.Max(2, (int)(admin.Skill / 20f));
                int wrong = Mathf.RoundToInt((1f - admin.Skill / 100f) * 25f);
                var skill = Box("adm-stat");
                skill.Add(Icon("star", "icon-13"));
                var bar = Box("bar", "adm-bar");
                var fill = Box("bar-fill");
                SetPercent(fill, admin.Skill);
                bar.Add(fill);
                skill.Add(bar);
                skill.Add(Text(Mathf.RoundToInt(admin.Skill).ToString(), "font-heavy", "adm-stat-value"));
                card.Add(skill);
                card.Add(AdmFact("envelope", $"up to {letters} letters a day"));
                card.Add(AdmFact("help", wrong < 1 ? "hardly ever wrong" : $"gets about {wrong}% wrong"));
                if (admin.IsAway(S.Day)) card.Add(Text("away till " + MonthDay(admin.AwayUntilDay), "font-heavy", "adm-stamp"));
                else if (admin.LeavingDay >= 0) card.Add(Text("leaving " + MonthDay(admin.LeavingDay), "font-heavy", "adm-stamp", "adm-stamp--bad"));
                int id = admin.Id;
                OnClick(card, () =>
                {
                    _staffFocus = id;
                    ShowPageAt(HudPage.Staff);
                });
            }

            var jobs = Box("adm-jobs");
            foreach (var job in AdmJobs(role)) jobs.Add(Text(job, "font-semi", "adm-job"));
            card.Add(jobs);
            if (admin == null) card.Add(AdmVacancy(role));
            return card;
        }

        static VisualElement AdmFact(string icon, string text)
        {
            var row = Box("adm-stat");
            row.Add(Icon(icon, "icon-13", "icon--soft"));
            row.Add(Text(text, "font-semi", "adm-fact"));
            return row;
        }

        /// <summary>How to fill an empty desk: the staff room (naming a candidate who applied), or why not yet.</summary>
        VisualElement AdmVacancy(AdminRole role)
        {
            if (role == AdminRole.AthleticDirector && !_sim.IsUnlocked(Feature.SportsPrograms))
                return AdmFact("lock", $"comes with sports teams at Lv {Balance.UnlockLevel(Feature.SportsPrograms)}");
            Candidate candidate = null;
            foreach (var c in S.Candidates)
                if (c.Staff.Role == StaffRole.Administrator && c.Staff.AdminRole == role) candidate = c;
            var link = Box("adm-link");
            link.Add(Text(candidate != null ? $"{candidate.Staff.Name} applied, skill {Mathf.RoundToInt(candidate.Staff.Skill)}" : "hire one in the staff room",
                "font-note", "adm-link-label"));
            link.Add(Icon("next", "icon-13", "icon--blue"));
            OnClick(link, () => ShowPageAt(HudPage.Staff));
            return link;
        }

        /// <summary>The letters each director answers (the event templates they handle).</summary>
        static string[] AdmJobs(AdminRole role) => role switch
        {
            AdminRole.HRManager => new[] { "raises", "rival offers", "renewals", "burnout" },
            AdminRole.AdmissionsDirector => new[] { "transfers", "summer intake" },
            AdminRole.FinancialDirector => new[] { "grants", "discounts", "donations" },
            AdminRole.DormDirector => new[] { "roommates", "dorm complaints", "plumbing" },
            AdminRole.StudentAffairsDirector => new[] { "food complaints", "worried parents", "discipline", "bullying", "outbreaks" },
            AdminRole.AthleticDirector => new[] { "sponsors", "recruits", "injuries" },
            _ => Array.Empty<string>(),
        };

        // --- Standing orders ----------------------------------------------------------------------

        VisualElement AdmBuildOrders()
        {
            var panel = Ink("adm-orders");
            var head = Box("adm-head");
            head.Add(Text("Standing orders", "font-hand", "adm-title"));
            head.Add(Text("what directors do for you", "font-note", "adm-head-note"));
            panel.Add(head);

            AdmGroup(panel, "coin", "Discounts and aid", "Financial and Admissions directors");
            AdmNumber(panel, "Say yes to discounts up to", "also the most aid a new family gets",
                () => AdmPolicy.AutoApproveDiscountBelow, v => AdmPolicy.AutoApproveDiscountBelow = v, 0f, 0.5f, 0.05f, AdmPercent);
            AdmToggle(panel, "Turn bigger requests down", null,
                () => AdmPolicy.RejectLargerDiscounts, v => AdmPolicy.RejectLargerDiscounts = v);

            AdmGroup(panel, "staff", "Raises and rival offers", "HR Manager");
            AdmNumber(panel, "Approve raises up to", null,
                () => AdmPolicy.AutoApproveRaiseBelow, v => AdmPolicy.AutoApproveRaiseBelow = v, 0f, 0.2f, 0.01f, AdmPercent);
            AdmToggle(panel, "Offer half of a bigger raise", null,
                () => AdmPolicy.CounterLargerRaises, v => AdmPolicy.CounterLargerRaises = v);
            AdmNumber(panel, "Match rival offers for staff with skill of at least", "others are let go",
                () => AdmPolicy.AutoMatchOffersSkillAbove, v => AdmPolicy.AutoMatchOffersSkillAbove = v, 40f, 100f, 1f, AdmWhole);

            AdmGroup(panel, "house", "Complaints", "Dorm and Student Affairs directors");
            AdmNumber(panel, "Pay families back up to", null,
                () => AdmPolicy.AutoCompensateComplaintsBelow, v => AdmPolicy.AutoCompensateComplaintsBelow = v, 0f, 2000f, 50f, AdmCash);

            AdmGroup(panel, "letters", "Admissions", "Admissions Director");
            AdmNumber(panel, "Admit students with academics of at least", null,
                () => AdmPolicy.AutoAdmitAcademicAbove, v => AdmPolicy.AutoAdmitAcademicAbove = v, 50f, 100f, 1f, AdmWhole);
            AdmToggle(panel, "Fill the last beds before the season ends", null,
                () => AdmPolicy.FillRemainingBedsAtDeadline, v => AdmPolicy.FillRemainingBedsAtDeadline = v);

            AdmGroup(panel, "shield", "Fights and parties", "Student Affairs Director");
            var pills = Pills(AdmResponseNames, () => Mathf.Max(0, Array.IndexOf(AdmResponses, AdmPolicy.DefaultDisciplineResponse)),
                i => AdmPolicy.DefaultDisciplineResponse = AdmResponses[i]);
            pills.AddToClassList("adm-pills");
            panel.Add(pills);
            return panel;
        }

        static void AdmGroup(VisualElement panel, string icon, string title, string who)
        {
            var row = Box("adm-group");
            row.Add(Icon(icon, "icon-20"));
            row.Add(Text(title, "font-heavy", "adm-group-title"));
            row.Add(Text(who, "font-note", "adm-group-who"));
            panel.Add(row);
        }

        static VisualElement AdmLabel(string label, string note)
        {
            var text = Box("adm-order-text");
            text.Add(Text(label, "font-semi", "adm-order-label"));
            if (note != null) text.Add(Text(note, "font-note", "adm-order-note"));
            return text;
        }

        /// <summary>A threshold the directors follow, stepped like the debug sliders (and written straight into the policies).</summary>
        void AdmNumber(VisualElement panel, string label, string note, Func<float> get, Action<float> set,
            float min, float max, float step, Func<float, string> format)
        {
            var row = Box("adm-order");
            row.Add(AdmLabel(label, note));
            var value = Text("", "font-hand", "adm-value");
            void Step(float direction)
            {
                float v = Mathf.Clamp(Mathf.Round((get() + direction * step) / step) * step, min, max);
                set(v);
                SetText(value, format(v));
            }
            row.Add(MonStepper("minus", () => Step(-1f), true, "mon-step--small"));
            row.Add(value);
            row.Add(MonStepper("plus", () => Step(1f), true, "mon-step--small", "mon-step--b"));
            panel.Add(row);
            _admOrders.Add(() => SetText(value, format(get())));
        }

        /// <summary>A yes/no order with the settings switch; the whole row toggles it.</summary>
        void AdmToggle(VisualElement panel, string label, string note, Func<bool> get, Action<bool> set)
        {
            var row = Box("adm-order", "adm-order--toggle");
            row.Add(AdmLabel(label, note));
            var toggle = Ink("switch", "adm-switch");
            var knob = Ink("switch-knob");
            var check = Icon("check", "icon-13", "icon--blue");
            knob.Add(check);
            toggle.Add(knob);
            row.Add(toggle);
            void Refresh()
            {
                bool on = get();
                toggle.EnableInClassList("switch--on", on);
                check.EnableInClassList("hidden", !on);
            }
            OnClick(row, () =>
            {
                set(!get());
                Refresh();
            });
            Refresh();
            _admOrders.Add(Refresh);
            panel.Add(row);
        }

        static string AdmPercent(float v) => v < 0.005f ? "none" : Mathf.RoundToInt(v * 100f) + "%";
        static string AdmWhole(float v) => Mathf.RoundToInt(v).ToString();
        static string AdmCash(float v) => v < 1f ? "never" : Money(v);

        // --- Pictures -----------------------------------------------------------------------------

        static void AdmDefineIcons()
        {
            if (InkIcons.Get("adm-desk") != null) return;
            InkIcons.Define("adm-desk", 2f, "fill:#DCEBFB rect:7,3.5,7,5.5,1", "fill:#FFE066 M3 9h18v3H3z", "M5 12v8M19 12v8M14 12v4.5h5");
        }

        /// <summary>An empty desk chair, for a director nobody has hired yet.</summary>
        static InkDrawing AdmChair() => _admChair ??= new InkDrawing(48f, 48f)
            .Shape("M15 25V9c0-2 1.5-3.5 3.5-3.5h11c2 0 3.5 1.5 3.5 3.5v16z", InkIcons.Paper, InkIcons.Ink, 2.4f)
            .Line("M19.5 12h9M19.5 17h9", InkIcons.Sky, 2f)
            .Shape("M11 25h26c1.4 0 2.5 1.1 2.5 2.5V29c0 1.4-1.1 2.5-2.5 2.5H11c-1.4 0-2.5-1.1-2.5-2.5v-1.5c0-1.4 1.1-2.5 2.5-2.5z",
                InkIcons.Sky, InkIcons.Ink, 2.4f)
            .Line("M12 31.5v11M36 31.5v11M17.5 31.5v7M30.5 31.5v7", InkIcons.Ink, 2.4f);
    }
}
