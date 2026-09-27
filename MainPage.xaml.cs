namespace ComunioAdminTools;

using ComunioAdminTools.Helpers;
using ComunioAdminTools.Controls;
using Models;
using System.Net.Http;
using System.Net.Http.Headers;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnDinersTabClicked(object? sender, EventArgs e) => SelectTab(true);

    private void OnConsultesTabClicked(object? sender, EventArgs e) => SelectTab(false);

    private void SelectTab(bool dinersSelected)
    {
        DinersPanel.IsVisible = dinersSelected;
        ConsultesPanel.IsVisible = !dinersSelected;
        DinersTabButton.BackgroundColor = Color.FromArgb(dinersSelected ? "#DCEBE2" : "#FFFFFF");
        ConsultesTabButton.BackgroundColor = Color.FromArgb(dinersSelected ? "#FFFFFF" : "#DCEBE2");
        SemanticProperties.SetDescription(DinersTabButton, dinersSelected ? "Diners, pestanya seleccionada" : "Diners, pestanya no seleccionada");
        SemanticProperties.SetDescription(ConsultesTabButton, dinersSelected ? "Consultes, pestanya no seleccionada" : "Consultes, pestanya seleccionada");
    }

    private async void OnButtonPressed(object sender, EventArgs e)
    {
        if (sender is VisualElement ve)
        {
            try { await ve.ScaleToAsync(0.98, 80, Easing.CubicOut); } catch { }
        }
    }

    private async void OnButtonReleased(object sender, EventArgs e)
    {
        if (sender is VisualElement ve)
        {
            try { await ve.ScaleToAsync(1.0, 80, Easing.CubicIn); } catch { }
        }
    }

    private async void OnPosarPuntsClicked(object sender, EventArgs e)
    {
        await ManagePunts(sender, e, isRevertingPoints: false);
    }

    private async void OnRevertirPuntsClicked(object sender, EventArgs e)
    {
        await ManagePunts(sender, e, isRevertingPoints: true);
    }

    private async void OnCheckSquadExcessClicked(object sender, EventArgs e)
    {
        try
        {
            SetLoading(true);
            using var httpClient = new HttpClient();
            var tokenResponse = await httpClient.PostAsync<LoginModel, TokenResponse>("https://api.comunio.es/login", new LoginModel
            {
                Username = UsernameEntry.Text ?? string.Empty,
                Password = PasswordEntry.Text ?? string.Empty
            });

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
            var sessionData = await httpClient.GetAsync<SessionDataModel>("https://api.comunio.es");

            if (!sessionData.User.IsLeader && !sessionData.User.IsAdmin)
            {
                await DisplayAlertAsync("Error", "No tienes permisos para realizar esta acción.", "OK");
                return;
            }

            var standings = await httpClient.GetAsync<StandingsModel>($"https://api.comunio.es/communities/{sessionData.Community.Id}/standings?period=total&wpe=true");

            var results = new List<ExcesoPlantillaModel>();

            foreach (var team in standings.Teams)
            {
                var squad = await httpClient.GetAsync<SquadModel>($"https://api.comunio.es/users/{team.TeamInfo.User.Id}/squad?eid=live");

                if (squad.Players.Count() > 25)
                {
                    results.Add(new ExcesoPlantillaModel
                    {
                        UserName = team.TeamInfo.User.Name,
                        PlayerCount = squad.Players.Count()
                    });
                }
            }

            await ShowResultsAsync("Excés de plantilla", results, new ExcesoPlantilla(), ExcesoPlantilla.ItemsSourceProperty);
            
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.IsVisible = isLoading;
        LoadingIndicator.IsRunning = isLoading;
        SetPointsCashButton.IsEnabled = !isLoading;
        RevertPointsCashButton.IsEnabled = !isLoading;
        ShowTeamsLastPointsButton.IsEnabled = !isLoading;
        CheckSquadExcessButton.IsEnabled = !isLoading;
    }

    private async void ShowTeamsLastPoints(object sender, EventArgs e)
    {
        try
        {
            // Mostrar overlay de carga y desactivar botones
            SetLoading(true);

            using var httpClient = new HttpClient();

            var tokenResponse = await httpClient.PostAsync<LoginModel, TokenResponse>("https://api.comunio.es/login", new LoginModel
            {
                Username = UsernameEntry.Text ?? string.Empty,
                Password = PasswordEntry.Text ?? string.Empty
            });

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            var sessionData = await httpClient.GetAsync<SessionDataModel>("https://api.comunio.es");

            if (!sessionData.User.IsLeader && !sessionData.User.IsAdmin)
            {
                await DisplayAlertAsync("Error", "No tienes permisos para realizar esta acción.", "OK");
                return;
            }

            var standings = await httpClient.GetAsync<StandingsModel>($"https://api.comunio.es/communities/{sessionData.Community.Id}/standings?period=total&wpe=true");
            var results = standings.Teams.Select(team => new PuntosJornadaModel
            {
                UserName = team.TeamInfo.User.Name,
                Points = int.TryParse(team.LastPoints, out var points) ? points : 0
            })
            .OrderByDescending(item => item.Points)
            .ToList();

            await ShowResultsAsync("Punts de la última jornada", results, new PuntosJornadaView(), PuntosJornadaView.ItemsSourceProperty);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message + Environment.NewLine + ex.StackTrace, "OK");
        }
        finally
        {
            // Ocultar overlay y reactivar botones
            SetLoading(false);
        }
    }

    private async Task ManagePunts(object sender, EventArgs e, bool isRevertingPoints)
    {
        try
        {
            // Mostrar overlay de carga y desactivar botones
            SetLoading(true);
            using var httpClient = new HttpClient();

            var tokenResponse = await httpClient.PostAsync<LoginModel, TokenResponse>("https://api.comunio.es/login", new LoginModel
            {
                Username = UsernameEntry.Text ?? string.Empty,
                Password = PasswordEntry.Text ?? string.Empty
            });

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            var sessionData = await httpClient.GetAsync<SessionDataModel>("https://api.comunio.es");

            if (!sessionData.User.IsLeader && !sessionData.User.IsAdmin)
            {
                await DisplayAlertAsync("Error", "No tienes permisos para realizar esta acción.", "OK");
                return;
            }

            var standings = await httpClient.GetAsync<StandingsModel>($"https://api.comunio.es/communities/{sessionData.Community.Id}/standings?period=total&wpe=true");
            var postCashResults = await ManagePostCash(standings, isRevertingPoints, httpClient, sessionData);

            await ShowDineroJornadaAsync(isRevertingPoints ? "Diners retirats" : "Diners abonats", postCashResults);
            
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            // Ocultar overlay y reactivar botones
            SetLoading(false);
        }
    }

    private Task ShowDineroJornadaAsync(string title, IEnumerable<DineroJornadaModel> dataSource)
        => ShowResultsAsync(title, dataSource, new DineroJornadaResultView(), DineroJornadaResultView.ItemsSourceProperty);

    private async Task ShowResultsAsync(string title, System.Collections.IEnumerable dataSource, View resultsView, BindableProperty itemsSourceProperty)
    {
        LoadingOverlay.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        MainContent.IsEnabled = false;
        try
        {
            await ActionResultsPopup.ShowAsync(title, dataSource, resultsView, itemsSourceProperty);
        }
        finally
        {
            MainContent.IsEnabled = true;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (!ActionResultsPopup.IsVisible)
            return base.OnBackButtonPressed();

        ActionResultsPopup.Close();
        return true;
    }

    protected override void OnDisappearing()
    {
        ActionResultsPopup.Close();
        base.OnDisappearing();
    }

    private async Task<List<DineroJornadaModel>> ManagePostCash(StandingsModel standings, bool isRevertingPoints, HttpClient httpClient, SessionDataModel sessionData)
    {
        var postCashResults = new List<DineroJornadaModel>();

        foreach (var team in standings.Teams)
        {
            var canPostCash = int.TryParse(team.LastPoints, out var lastPoints);
            var cashAmount = 0;

            while (canPostCash && lastPoints > 0)
            {
                var requestModel = new PostCashRequestModel
                {
                    Amount = CalculatePointsCash(Math.Min(lastPoints, 100), isRevertingPoints),
                    Reason = isRevertingPoints ? "CORREGIR ERROR PUNTS" : "PUNTS"
                };

                await httpClient.PostWithoutResponseAsync($"https://api.comunio.es/communities/{sessionData.Community.Id}/users/{team.TeamInfo.User.Id}/penalties", requestModel);

                lastPoints -= 100;
                cashAmount += requestModel.Amount;
            }

            postCashResults.Add(new DineroJornadaModel { UserName = team.TeamInfo.User.Name, Amount = cashAmount });
        }

        return postCashResults.OrderByDescending(item => item.Amount).ToList();
    }

    private static int CalculatePointsCash(int points, bool isRevertingPoints)
    {
        var cashPerPoint = isRevertingPoints ? -100000 : 100000;
        return points > 0 ? points * cashPerPoint : 0;
    }
}
