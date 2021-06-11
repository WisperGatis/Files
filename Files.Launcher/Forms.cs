using System.Collections.Specialized;

namespace System.Windows
{
    internal class Forms
    {
        public static object Clipboard { get; internal set; }
        public static object Application { get; internal set; }

        internal class DataObject
        {
            public DataObject()
            {
            }

            internal void SetFileDropList(StringCollection fileList)
            {
                throw new NotImplementedException();
            }
        }
    }
}