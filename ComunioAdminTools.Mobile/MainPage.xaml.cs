namespace ComunioAdminTools.Mobile
{
    public partial class MainPage : ContentPage
    {
        private readonly ComunioAdminTools.UI.ResultsDialogController dialogs;

        public MainPage(ComunioAdminTools.UI.ResultsDialogController dialogs)
        {
            this.dialogs = dialogs;
            InitializeComponent();
        }

        protected override bool OnBackButtonPressed()
            => dialogs.TryClose() || base.OnBackButtonPressed();

        protected override void OnDisappearing()
        {
            dialogs.TryClose();
            base.OnDisappearing();
        }
    }
}
