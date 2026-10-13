namespace ComunioAdminTools.UI;

// Registered once per WebAssembly/MAUI client to connect native navigation to Razor.
public sealed class ResultsDialogController
{
    public bool IsOpen { get; private set; }
    public event Action? CloseRequested;

    public void Open() => IsOpen = true;

    public bool TryClose()
    {
        if (!IsOpen) return false;
        IsOpen = false;
        CloseRequested?.Invoke();
        return true;
    }
}
