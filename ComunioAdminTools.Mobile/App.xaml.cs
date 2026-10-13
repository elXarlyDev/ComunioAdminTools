namespace ComunioAdminTools.Mobile
{
    public partial class App : Application
    {
        private readonly ComunioAdminTools.UI.ResultsDialogController dialogs;

        public App(ComunioAdminTools.UI.ResultsDialogController dialogs)
        {
            this.dialogs = dialogs;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage(dialogs)) { Title = "ComunioAdminTools" };
        }
    }
}
