namespace CDPIUI.ViewModels.BlockCheck2;

public sealed partial class BlockCheck2MainPageViewModel
{
    public string ReviewSourcesText
    {
        get => _reviewPresentation.ReviewSourcesText;
        private set => _reviewPresentation.ReviewSourcesText = value;
    }

    public string ReviewConnectionsText
    {
        get => _reviewPresentation.ReviewConnectionsText;
        private set => _reviewPresentation.ReviewConnectionsText = value;
    }

    public string ReviewModeText
    {
        get => _reviewPresentation.ReviewModeText;
        private set => _reviewPresentation.ReviewModeText = value;
    }

    public string ReviewRequestsText
    {
        get => _reviewPresentation.ReviewRequestsText;
        private set => _reviewPresentation.ReviewRequestsText = value;
    }

    public string ReviewTimeoutsText
    {
        get => _reviewPresentation.ReviewTimeoutsText;
        private set => _reviewPresentation.ReviewTimeoutsText = value;
    }

    public string ReviewMaximumTimeText
    {
        get => _reviewPresentation.ReviewMaximumTimeText;
        private set => _reviewPresentation.ReviewMaximumTimeText = value;
    }

    private void UpdateReviewSummary(BlockCheck2PreparationPreview preview) =>
        _reviewPresentation.UpdateReviewSummary(preview);
}
