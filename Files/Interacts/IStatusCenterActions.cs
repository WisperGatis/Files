using Files.Enums;
using Files.ViewModels;
using System;
using System.Threading;

namespace Files.Interacts
{
    public interface IStatusCenterActions
    {
        event EventHandler<PostedStatusBanner> ProgressBannerPosted;

        float MedianOperationProgressValue { get; }

        int OngoingOperationsCount { get; }

        bool AnyOperationsOngoing { get; }

        void UpdateMedianProgress();

        PostedStatusBanner PostBanner(string title, string message, float initialProgress, ReturnResult status, FileOperationType operation);

        PostedStatusBanner PostActionBanner(string title, string message, string primaryButtonText, string cancelButtonText, Action primaryAction);
        
        PostedStatusBanner PostOperationBanner(string title, string message, float initialProgress, ReturnResult status, FileOperationType operation, CancellationTokenSource cancellationTokenSource);

        bool CloseBanner(StatusBanner banner);

        void UpdateBanner(StatusBanner banner);
    }
}