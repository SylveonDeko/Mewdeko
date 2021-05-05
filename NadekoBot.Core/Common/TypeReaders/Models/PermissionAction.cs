namespace NadekoBot.Common.TypeReaders.Models
{
    public class PermissionAction
    {
        public static PermissionAction Enable => new PermissionAction(true);
        public static PermissionAction Disable => new PermissionAction(false);

        public bool Value { get; }

        public PermissionAction(bool value)
        {
            this.Value = value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return this.Value == ((PermissionAction)obj).Value;
        }

        public override int GetHashCode() => Value.GetHashCode();
    }
}
