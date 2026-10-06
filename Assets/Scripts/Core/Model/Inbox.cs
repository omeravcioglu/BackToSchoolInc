using System.Collections.Generic;

namespace SchoolSim
{
    public sealed class MessageOption
    {
        public string Id;
        public string Label;
        public string Preview;             // visible consequences
        public string Hint;                // hidden consequence, revealed at higher director levels
        public float Cost;                 // cash needed to choose it
        public Feature? RequiresFeature;
        public string DisabledReason;      // set when the option cannot be chosen at all
    }

    /// <summary>
    /// A message in the director's inbox. Messages created by an event template carry its id plus a
    /// small payload, and the template resolves the chosen option. That keeps messages plain data.
    /// </summary>
    public sealed class InboxMessage
    {
        public int Id;
        public string TemplateId;
        public SenderType Sender;
        public string SenderName;
        public string Subject;
        public string Body;
        public MessagePriority Priority = MessagePriority.Normal;
        public int ReceivedDay;
        public int ExpiresDay = -1;
        public readonly List<MessageOption> Options = new List<MessageOption>();

        public bool Resolved;
        public string ChosenOptionId;
        public string ResolutionText;
        public string HandledBy;           // administrator name, or null if the director answered
        public bool Read;

        // The assistant's view: which option they would pick and why.
        public string AdvisorOptionId;
        public string AdvisorNote;

        // Payload interpreted by the template.
        public int StudentId = -1;
        public int StudentId2 = -1;
        public int StaffId = -1;
        public int RoomId = -1;
        public int OtherId = -1;
        public float Amount;
        public float Amount2;
        public string Tag;

        public bool NeedsDecision => !Resolved && Options.Count > 0;

        public MessageOption AddOption(string id, string label, string preview, string hint = null, float cost = 0f)
        {
            var o = new MessageOption { Id = id, Label = label, Preview = preview, Hint = hint, Cost = cost };
            Options.Add(o);
            return o;
        }

        public MessageOption FindOption(string id)
        {
            foreach (var o in Options) if (o.Id == id) return o;
            return null;
        }
    }

    public enum PromiseKind
    {
        FoodQualityAtLeast,
        FoodTierAtLeast,
        DormQualityAtLeast,
        RoomTypeExists,
        InternetLevelAtLeast,
        SalaryReview,
        StaffRoleCountAtLeast,
        ClubExists,
        SafetyAtLeast,
    }

    /// <summary>A commitment the director made in a reply. Checked daily until kept or broken.</summary>
    public sealed class Promise
    {
        public int Id;
        public string Text;
        public PromiseKind Kind;
        public SenderType MadeTo;
        public int MadeDay;
        public int DueDay;
        public float Target;
        public int TargetId = -1;          // staff id, room type, club type... depending on kind
        public float TrustReward = 4f;
        public float TrustPenalty = 10f;
        public int StudentId = -1;         // family that received the promise, if any

        public bool Resolved;
        public bool Kept;
    }

    /// <summary>A delayed consequence ("the cover-up leaks in 3 weeks").</summary>
    public sealed class ScheduledEvent
    {
        public int Day;
        public string TemplateId;
        public string Tag;
        public int StudentId = -1;
        public int StaffId = -1;
        public int OtherId = -1;
        public float Amount;
    }

    public sealed class LogEntry
    {
        public int Day;
        public string Text;
        public bool Important;
    }
}
