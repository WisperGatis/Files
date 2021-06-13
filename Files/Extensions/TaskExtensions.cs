using System.Threading.Tasks;

namespace Files.Extensions
{
    internal static class TaskExtensions
    {
#pragma warning disable RCS1175 
#pragma warning disable IDE0060 

        internal static void Forget(this Task task)
        {
            // do nothing, just forget about the task
        }

#pragma warning restore IDE0060 
#pragma warning restore RCS1175
    }
}