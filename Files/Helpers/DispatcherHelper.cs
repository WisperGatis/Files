using System;
using System.Runtime.CompilerServices;
using Windows.UI.Core;

namespace Files.Helpers
{
    internal static class DispatcherHelper

        public struct DispatcherPriorityAwaitable
        {
            private readonly CoreDispatcher dispatcher;
            private readonly CoreDispatcherPriority priority;

            internal DispatcherPriorityAwaitable(CoreDispatcher dispatcher, CoreDispatcherPriority priority)
            {
                this.dispatcher = dispatcher;
                this.priority = priority;
            }

            public DispatcherPriorityAwaiter GetAwaiter()
            {
                return new DispatcherPriorityAwaiter(this.dispatcher, this.priority);
            }
        }

        public struct DispatcherPriorityAwaiter : INotifyCompletion
        {
            private readonly CoreDispatcher dispatcher;
            private readonly CoreDispatcherPriority priority;

            public bool IsCompleted => false;

            internal DispatcherPriorityAwaiter(CoreDispatcher dispatcher, CoreDispatcherPriority priority)
            {
                this.dispatcher = dispatcher;
                this.priority = priority;
            }

            public void GetResult()
            {
            }

            public async void OnCompleted(Action continuation)
            {
                await this.dispatcher.RunAsync(this.priority, new DispatchedHandler(continuation));
            }
        }

        public static DispatcherPriorityAwaitable YieldAsync(this CoreDispatcher dispatcher, CoreDispatcherPriority priority = CoreDispatcherPriority.Low)
        {
            return new DispatcherPriorityAwaitable(dispatcher, priority);
        }
    }
}