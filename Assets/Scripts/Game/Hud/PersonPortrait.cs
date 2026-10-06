using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// A student's or staff member's face for the HUD pages: the same look as their figure on the
    /// campus, with an expression from their current mood. Size it with USS (it keeps a 32:40 shape).
    /// </summary>
    public sealed class PersonPortrait : InkPicture
    {
        long _shown = long.MinValue;

        public PersonPortrait()
        {
            AddToClassList("person-portrait");
        }

        public void ShowStudent(Student s)
        {
            if (s == null) { ClearFace(); return; }
            Show(PersonLook.ForStudent(s), Expressions.For(s));
        }

        public void ShowStaff(StaffMember m)
        {
            if (m == null) { ClearFace(); return; }
            Show(PersonLook.ForStaff(m), Expressions.For(m));
        }

        public void Show(PersonLook look, Expression face)
        {
            long key = PeopleArt.Key(look, face);
            if (key == _shown && Drawing != null) return;
            _shown = key;
            Drawing = PeopleArt.Portrait(look, face);
        }

        public void ClearFace()
        {
            _shown = long.MinValue;
            Drawing = null;
        }
    }
}
