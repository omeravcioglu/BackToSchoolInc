using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The sockets the notebook pages plug into. Every page lives in its own file
    /// (GameHud.Page&lt;Name&gt;.cs, with its stylesheet in Resources/Hud/Pages/&lt;Name&gt;.uss) and implements
    /// its Build, Refresh and Reset hooks below; a hook nobody implements simply does nothing.
    ///
    /// Build: add the page's elements to <c>_pageHost</c> (a dotted paper sheet, flex row, padded).
    /// Refresh(newDay): called about five times a second while the page is open. newDay is true on
    /// the first refresh after Build, on each new school day and after <see cref="RunCommand"/>:
    /// rebuild lists then, and only update cheap labels otherwise.
    /// Reset: a new school was founded; forget selections that pointed into the old one.
    /// </summary>
    public sealed partial class GameHud
    {
        // Set before opening a page to land on one item; the page clears it once it has used it.
        int _inboxFocus = -1, _studentFocus = -1, _staffFocus = -1, _roomFocus = -1;

        partial void BuildInboxPage();
        partial void RefreshInboxPage(bool newDay);
        partial void ResetInboxPage();

        partial void BuildStudentsPage();
        partial void RefreshStudentsPage(bool newDay);
        partial void ResetStudentsPage();

        partial void BuildStaffPage();
        partial void RefreshStaffPage(bool newDay);
        partial void ResetStaffPage();

        partial void BuildMoneyPage();
        partial void RefreshMoneyPage(bool newDay);
        partial void ResetMoneyPage();

        partial void BuildRulesPage();
        partial void RefreshRulesPage(bool newDay);
        partial void ResetRulesPage();

        partial void BuildAdminPage();
        partial void RefreshAdminPage(bool newDay);
        partial void ResetAdminPage();

        partial void BuildAdmissionsPage();
        partial void RefreshAdmissionsPage(bool newDay);
        partial void ResetAdmissionsPage();

        partial void BuildClubsPage();
        partial void RefreshClubsPage(bool newDay);
        partial void ResetClubsPage();

        partial void BuildReportPage();
        partial void RefreshReportPage(bool newDay);
        partial void ResetReportPage();

        partial void BuildReportsPage();
        partial void RefreshReportsPage(bool newDay);
        partial void ResetReportsPage();

        partial void BuildLogPage();
        partial void RefreshLogPage(bool newDay);
        partial void ResetLogPage();

        partial void BuildRoomsPage();
        partial void RefreshRoomsPage(bool newDay);
        partial void ResetRoomsPage();

        void BuildHookedPage(HudPage page)
        {
            switch (page)
            {
                case HudPage.Inbox: BuildInboxPage(); break;
                case HudPage.Students: BuildStudentsPage(); break;
                case HudPage.Staff: BuildStaffPage(); break;
                case HudPage.Money: BuildMoneyPage(); break;
                case HudPage.Rules: BuildRulesPage(); break;
                case HudPage.Admin: BuildAdminPage(); break;
                case HudPage.Admissions: BuildAdmissionsPage(); break;
                case HudPage.Clubs: BuildClubsPage(); break;
                case HudPage.Report: BuildReportPage(); break;
                case HudPage.Reports: BuildReportsPage(); break;
                case HudPage.Log: BuildLogPage(); break;
                case HudPage.Rooms: BuildRoomsPage(); break;
            }
        }

        void RefreshHookedPage(HudPage page, bool newDay)
        {
            switch (page)
            {
                case HudPage.Inbox: RefreshInboxPage(newDay); break;
                case HudPage.Students: RefreshStudentsPage(newDay); break;
                case HudPage.Staff: RefreshStaffPage(newDay); break;
                case HudPage.Money: RefreshMoneyPage(newDay); break;
                case HudPage.Rules: RefreshRulesPage(newDay); break;
                case HudPage.Admin: RefreshAdminPage(newDay); break;
                case HudPage.Admissions: RefreshAdmissionsPage(newDay); break;
                case HudPage.Clubs: RefreshClubsPage(newDay); break;
                case HudPage.Report: RefreshReportPage(newDay); break;
                case HudPage.Reports: RefreshReportsPage(newDay); break;
                case HudPage.Log: RefreshLogPage(newDay); break;
                case HudPage.Rooms: RefreshRoomsPage(newDay); break;
            }
        }

        void ResetHookedPages()
        {
            _inboxFocus = _studentFocus = _staffFocus = _roomFocus = -1;
            ResetInboxPage();
            ResetStudentsPage();
            ResetStaffPage();
            ResetMoneyPage();
            ResetRulesPage();
            ResetAdminPage();
            ResetAdmissionsPage();
            ResetClubsPage();
            ResetReportPage();
            ResetReportsPage();
            ResetLogPage();
            ResetRoomsPage();
        }

        static (string title, string icon) HookedPageTitle(HudPage page) => page switch
        {
            HudPage.Inbox => ("Inbox", "envelope"),
            HudPage.Students => ("Students", "people"),
            HudPage.Staff => ("Staff room", "staff"),
            HudPage.Money => ("Money", "coin"),
            HudPage.Rules => ("School rules", "sliders"),
            HudPage.Admin => ("Administration", "staff"),
            HudPage.Admissions => ("Admissions", "letters"),
            HudPage.Clubs => ("Clubs & teams", "trophy"),
            HudPage.Report => ("School report", "clipboard"),
            HudPage.Reports => ("Year reports", "book"),
            HudPage.Log => ("School log", "eye"),
            HudPage.Rooms => ("Rooms", "house"),
            _ => ("", "envelope"),
        };

        /// <summary>Opens a page without toggling it closed, rebuilding it if it's already open (for focus).</summary>
        void ShowPageAt(HudPage page)
        {
            if (_page != page) OpenPage(page);
            else ShowPage(page);
        }

        /// <summary>Runs a player command, toasts its result and refreshes everything that shows it.</summary>
        void RunCommand(Func<CommandResult> command)
        {
            if (_sim == null || command == null) return;
            var result = command();
            if (!string.IsNullOrEmpty(result.Message)) ShowToast(result.Message, result.Ok ? "check" : "alert", result.Ok ? null : "bad");
            _pageDay = -1;          // the open page rebuilds on its next refresh
            _nextRefresh = 0f;
        }

        /// <summary>Each page's own stylesheet, from Resources/Hud/Pages.</summary>
        void LoadPageStyles()
        {
            foreach (var sheet in Resources.LoadAll<StyleSheet>("Hud/Pages"))
                if (!_root.styleSheets.Contains(sheet)) _root.styleSheets.Add(sheet);
        }
    }
}
