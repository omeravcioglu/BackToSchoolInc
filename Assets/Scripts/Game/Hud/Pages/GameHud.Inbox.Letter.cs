using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The inbox's open letter: who wrote it (with a face or badge on the stamp), the subject in
    /// marker, the handwriting on lined paper, the deadline, and a "good to know" card about the
    /// people it's about.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Lined letter paper: a faint rule under each line of the handwriting inside it.</summary>
        sealed class IbxRuled : VisualElement
        {
            static readonly Color Rule = InkIcons.Hex(0xD6E6F7);
            public float Spacing, First;

            public IbxRuled()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                float w = layout.width, h = layout.height;
                if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h) || Spacing < 6f) return;
                var p = ctx.painter2D;
                p.strokeColor = Rule;
                p.lineWidth = 1.5f;
                p.BeginPath();
                for (float y = First; y < h; y += Spacing)
                {
                    p.MoveTo(new Vector2(0f, y));
                    p.LineTo(new Vector2(w, y));
                }
                p.Stroke();
            }
        }

        InkBox _ibxKnow;
        ScrollView _ibxLetterScroll;
        VisualElement _ibxLetterEmpty, _ibxKnowList;
        Label _ibxFromName, _ibxFromDetail, _ibxSent, _ibxDue, _ibxStatus, _ibxStampWord, _ibxSubject, _ibxBody, _ibxSign, _ibxLateNote;
        PersonPortrait _ibxFace;
        Portrait _ibxHelperFace;
        InkIcon _ibxSenderIcon;
        IbxRuled _ibxRuled;
        int _ibxLetterSig;

        void IbxBuildLetter()
        {
            var letter = Ink("ibx-letter");
            letter.Add(Box("ibx-margin"));
            letter.Add(Ink("ibx-hole", "ibx-hole--a"));
            letter.Add(Ink("ibx-hole", "ibx-hole--b"));
            letter.Add(Ink("ibx-hole", "ibx-hole--c"));

            var page = Box("ibx-letter-page");
            var head = Box("ibx-head");
            var from = Box("ibx-from");
            from.Add(Text("from", "font-note", "ibx-from-label"));
            _ibxFromName = Text("", "font-heavy", "ibx-from-name");
            from.Add(_ibxFromName);
            _ibxFromDetail = Text("", "font-semi", "ibx-from-detail");
            from.Add(_ibxFromDetail);
            var dates = Box("ibx-dates");
            _ibxSent = Text("", "font-semi", "ibx-from-detail");
            dates.Add(_ibxSent);
            _ibxDue = Text("", "font-heavy", "ibx-due");
            dates.Add(_ibxDue);
            from.Add(dates);
            _ibxStatus = Text("", "font-heavy", "ibx-status");
            from.Add(_ibxStatus);
            head.Add(from);

            var stamp = Ink("ibx-stamp-box");
            _ibxFace = new PersonPortrait();
            _ibxFace.AddToClassList("ibx-stamp-face");
            stamp.Add(_ibxFace);
            _ibxHelperFace = new Portrait();
            _ibxHelperFace.AddToClassList("ibx-stamp-face");
            stamp.Add(_ibxHelperFace);
            _ibxSenderIcon = Icon("envelope", "ibx-stamp-icon");
            stamp.Add(_ibxSenderIcon);
            _ibxStampWord = Text("", "font-note", "ibx-stamp-word");
            stamp.Add(_ibxStampWord);
            head.Add(stamp);
            page.Add(head);

            var subject = Box("ibx-subject-box");
            subject.Add(Box("ibx-subject-mark"));
            _ibxSubject = Text("", "font-hand", "ibx-subject");
            subject.Add(_ibxSubject);
            page.Add(subject);

            _ibxRuled = new IbxRuled();
            _ibxRuled.AddToClassList("ibx-ruled");
            _ibxBody = Text("", "font-note", "ibx-body");
            _ibxBody.RegisterCallback<GeometryChangedEvent>(_ => IbxMeasureRules());
            _ibxRuled.Add(_ibxBody);
            _ibxSign = Text("", "font-note", "ibx-sign");
            _ibxRuled.Add(_ibxSign);
            page.Add(_ibxRuled);

            _ibxLateNote = Text("", "font-note", "ibx-late-note");
            page.Add(_ibxLateNote);

            _ibxKnow = Ink("ibx-know");
            _ibxKnow.Add(Text("good to know", "font-note", "ibx-know-title"));
            _ibxKnowList = Box();
            _ibxKnow.Add(_ibxKnowList);
            page.Add(_ibxKnow);

            _ibxLetterScroll = new ScrollView(ScrollViewMode.Vertical);
            _ibxLetterScroll.AddToClassList("ibx-letter-scroll");
            _ibxLetterScroll.Add(page);
            letter.Add(_ibxLetterScroll);

            _ibxLetterEmpty = Box("ibx-letter-empty");
            _ibxLetterEmpty.Add(Icon("envelope", "ibx-empty-icon"));
            _ibxLetterEmpty.Add(Text("Your desk is clear", "font-hand", "ibx-empty-title"));
            _ibxLetterEmpty.Add(Text("New letters land here: families, staff and the board all write in.", "font-note", "empty-note"));
            letter.Add(_ibxLetterEmpty);

            letter.Add(TapeStrip("ibx-letter-tape", "tape-sky"));
            _pageHost.Add(letter);
        }

        /// <summary>Lines the paper up with the handwriting: one rule per line of text, just under its baseline.</summary>
        void IbxMeasureRules()
        {
            const VisualElement.MeasureMode free = VisualElement.MeasureMode.Undefined;
            float size = _ibxBody.resolvedStyle.fontSize;
            float one = _ibxBody.MeasureTextSize("Ag", 0f, free, 0f, free).y;
            float five = _ibxBody.MeasureTextSize("Ag\nAg\nAg\nAg\nAg", 0f, free, 0f, free).y;
            float spacing = (five - one) / 4f;
            if (float.IsNaN(spacing) || spacing < size * 0.8f) spacing = size * 1.26f;   // Caveat's line is 1.26 em
            float first = spacing * 0.76f + 2f;                                          // and its baseline sits 76% down
            if (Mathf.Abs(spacing - _ibxRuled.Spacing) < 0.01f && Mathf.Abs(first - _ibxRuled.First) < 0.01f) return;
            _ibxRuled.Spacing = spacing;
            _ibxRuled.First = first;
            _ibxRuled.MarkDirtyRepaint();
        }

        void IbxRefreshLetter(InboxMessage m, bool newDay)
        {
            int sig = m == null ? 1 : m.Id * 4 + (m.Resolved ? 2 : 0) + 3;
            if (!newDay && sig == _ibxLetterSig) return;
            _ibxLetterSig = sig;
            Show(_ibxLetterScroll, m != null);
            Show(_ibxLetterEmpty, m == null);
            if (m == null) return;

            var student = S.FindStudent(m.StudentId);
            var staff = S.FindStaff(m.StaffId);
            string name = string.IsNullOrEmpty(m.SenderName) ? IbxKind(m.Sender) : m.SenderName;
            SetText(_ibxFromName, name);
            string detail = IbxDetail(m, name, student, staff);
            SetText(_ibxFromDetail, detail);
            Show(_ibxFromDetail, detail.Length > 0);
            IbxStamp(m, student, staff);

            bool due = m.NeedsDecision && m.ExpiresDay >= 0;
            SetText(_ibxSent, "sent " + IbxDate(m.ReceivedDay) + (due ? " · answer by " + IbxAnswerBy(m) : ""));
            Show(_ibxDue, due);
            Show(_ibxLateNote, due);
            if (due)
            {
                SetText(_ibxDue, IbxLeftText(m));
                _ibxDue.EnableInClassList("ibx-due--soon", IbxDaysLeft(m) <= 3 || m.Priority >= MessagePriority.Urgent);
                SetText(_ibxLateNote, $"no reply by {IbxAnswerBy(m)} has consequences too");
            }

            bool late = m.Resolved && m.ChosenOptionId == IbxExpired, urgent = m.NeedsDecision && m.Priority >= MessagePriority.Urgent;
            string status = m.Resolved ? (late ? "NO REPLY" : m.HandledBy != null ? "HANDLED" : "ANSWERED")
                : urgent ? "URGENT"
                : m.NeedsDecision && m.Priority == MessagePriority.Important ? "IMPORTANT"
                : null;
            Show(_ibxStatus, status != null);
            SetText(_ibxStatus, status);
            _ibxStatus.EnableInClassList("ibx-status--bad", late || urgent);
            _ibxStatus.EnableInClassList("ibx-status--done", m.Resolved && !late);

            SetHandText(_ibxSubject, m.Subject);
            SetText(_ibxBody, m.Body);
            SetText(_ibxSign, "– " + name);
            IbxFillKnow(m, student, staff);
        }

        /// <summary>The line under the sender's name: whose parent, which teacher, or what kind of sender.</summary>
        static string IbxDetail(InboxMessage m, string name, Student student, StaffMember staff)
        {
            switch (m.Sender)
            {
                case SenderType.Parent:
                    return student != null ? $"parent of {student.FullName} · grade {student.Grade}" : "parent";
                case SenderType.Student:
                    return student == null ? "student"
                        : name == student.FullName ? $"student · grade {student.Grade}"
                        : $"on behalf of the students, via {student.FullName}";
                case SenderType.Teacher:
                case SenderType.Staff:
                    if (staff != null && name == staff.Name) return staff.Title;
                    break;
                case SenderType.Government:
                    return "";
            }
            string kind = IbxKind(m.Sender);
            return name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0 ? "" : kind;
        }

        /// <summary>The stamp in the corner: the face of whoever the letter is from or about, or the sender's badge.</summary>
        void IbxStamp(InboxMessage m, Student student, StaffMember staff)
        {
            bool staffFace = staff != null && (m.Sender == SenderType.Teacher || m.Sender == SenderType.Staff || student == null);
            bool studentFace = !staffFace && student != null;
            var helper = !staffFace && !studentFace && m.Sender == SenderType.Assistant ? S.Assistant : null;
            Show(_ibxFace, staffFace || studentFace);
            Show(_ibxHelperFace, helper != null);
            Show(_ibxSenderIcon, !staffFace && !studentFace && helper == null);
            string word;
            if (staffFace)
            {
                _ibxFace.ShowStaff(staff);
                word = IbxFirstName(staff.Name);
            }
            else if (studentFace)
            {
                _ibxFace.ShowStudent(student);
                word = student.FirstName;
            }
            else if (helper != null)
            {
                _ibxHelperFace.Show(helper.Style, helper.Look);
                word = helper.FirstName;
            }
            else
            {
                _ibxSenderIcon.Icon = IbxSenderIcon(m.Sender);
                word = IbxStampWord(m.Sender);
            }
            SetText(_ibxStampWord, word);
        }

        static string IbxStampWord(SenderType t) => t switch
        {
            SenderType.Parent => "home",
            SenderType.Student => "dorms",
            SenderType.Teacher => "class",
            SenderType.Staff => "staff",
            SenderType.Admissions => "admissions",
            SenderType.Finance => "bank",
            SenderType.Sports => "sports",
            SenderType.Inspector => "inspector",
            SenderType.Sponsor => "sponsor",
            SenderType.Government => "board",
            SenderType.RivalSchool => "rivals",
            SenderType.StudentAffairs => "campus",
            SenderType.Facilities => "repairs",
            SenderType.Alumni => "alumni",
            SenderType.Assistant => "assistant",
            SenderType.Press => "press",
            _ => "post",
        };

        static string IbxFirstName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        // --- Good to know -------------------------------------------------------------------------

        void IbxFillKnow(InboxMessage m, Student student, StaffMember staff)
        {
            _ibxKnowList.Clear();
            if (student != null) _ibxKnowList.Add(IbxPerson(student));
            var second = m.StudentId2 != m.StudentId ? S.FindStudent(m.StudentId2) : null;
            if (second != null) _ibxKnowList.Add(IbxPerson(second));
            if (staff != null) _ibxKnowList.Add(IbxPerson(staff));
            Show(_ibxKnow, _ibxKnowList.childCount > 0);
        }

        VisualElement IbxPerson(Student s)
        {
            var row = IbxPersonRow(out var face, s.FullName, $"grade {s.Grade}");
            face.ShowStudent(s);
            row.Add(IbxMini("mood", s.Happiness));
            row.Add(IbxMini("family", s.ParentMood));
            row.Add(IbxMini("grades", s.Grades));
            if (_sim.IsRevealed(Reveal.Students))
            {
                int id = s.Id;
                IbxLink(row, () =>
                {
                    _studentFocus = id;
                    ShowPageAt(HudPage.Students);
                });
            }
            return row;
        }

        VisualElement IbxPerson(StaffMember s)
        {
            var row = IbxPersonRow(out var face, s.Name, s.Title);
            face.ShowStaff(s);
            row.Add(IbxMini("morale", s.Morale));
            row.Add(IbxMini("skill", s.Skill));
            row.Add(IbxMini("pay", MoneyShort(s.Salary), false));
            if (_sim.IsRevealed(Reveal.Staff))
            {
                int id = s.Id;
                IbxLink(row, () =>
                {
                    _staffFocus = id;
                    ShowPageAt(HudPage.Staff);
                });
            }
            return row;
        }

        static VisualElement IbxPersonRow(out PersonPortrait face, string name, string sub)
        {
            var row = Box("ibx-person");
            face = new PersonPortrait();
            face.AddToClassList("ibx-person-face");
            row.Add(face);
            var text = Box("ibx-person-text");
            text.Add(Text(name, "font-heavy", "ibx-person-name"));
            text.Add(Text(sub, "font-semi", "ibx-person-sub"));
            row.Add(text);
            return row;
        }

        static VisualElement IbxMini(string label, float value) => IbxMini(label, Mathf.RoundToInt(value).ToString(), value < 35f);

        static VisualElement IbxMini(string label, string value, bool low)
        {
            var mini = Box("ibx-mini");
            mini.Add(Text(label, "font-note", "ibx-mini-label"));
            var number = Text(value, "font-hand", "ibx-mini-value");
            number.EnableInClassList("ibx-mini-value--low", low);
            mini.Add(number);
            return mini;
        }

        /// <summary>Makes a person's row open their file.</summary>
        static void IbxLink(VisualElement row, Action open)
        {
            row.AddToClassList("ibx-person--link");
            row.Add(Icon("next", "icon-17", "icon--blue", "ibx-person-go"));
            OnClick(row, open);
        }
    }
}
