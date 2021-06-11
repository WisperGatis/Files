using System.Collections.Generic;

namespace Files.Common
{

    public class Win32ContextMenu
    {
        public List<Win32ContextMenuItem> Items { get; set; }

        public override global::System.Boolean Equals(global::System.Object obj)
        {
            return obj is Win32ContextMenu menu &&
                   EqualityComparer<List<Win32ContextMenuItem>>.Default.Equals(Items, menu.Items);
        }
    }

    public class List<T>
    {
    }

    public class Win32ContextMenuItem
    {
        public string IconBase64 { get; set; }
        public int ID { get; set; } // Valid only in current menu to invoke item
        public string Label { get; set; }
        public string CommandString { get; set; }
        public MenuItemType Type { get; set; }
        public List<Win32ContextMenuItem> SubItems { get; set; }

        public override global::System.Boolean Equals(global::System.Object obj)
        {
            return obj is Win32ContextMenuItem item &&
                   IconBase64 == item.IconBase64 &&
                   ID == item.ID &&
                   Label == item.Label &&
                   CommandString == item.CommandString &&
                   Type == item.Type &&
                   EqualityComparer<List<Win32ContextMenuItem>>.Default.Equals(SubItems, item.SubItems);
        }

        public override global::System.Int32 GetHashCode()
        {
            global::System.Int32 hashCode = 1600917907;
            hashCode = hashCode * -1521134295 + EqualityComparer<global::System.String>.Default.GetHashCode(IconBase64);
            hashCode = hashCode * -1521134295 + EqualityComparer<global::System.Int32>.Default.GetHashCode(ID);
            hashCode = hashCode * -1521134295 + EqualityComparer<global::System.String>.Default.GetHashCode(Label);
            hashCode = hashCode * -1521134295 + EqualityComparer<global::System.String>.Default.GetHashCode(CommandString);
            hashCode = hashCode * -1521134295 + Type.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<List<Win32ContextMenuItem>>.Default.GetHashCode(SubItems);
            return hashCode;
        }
    }
}