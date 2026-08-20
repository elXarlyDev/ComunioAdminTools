namespace ComunioAdminTools;

using ComunioAdminTools.Helpers;
using Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
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

    private async void ShowTeamsLastPoints(object sender, EventArgs e)
    {
        try
        {
            // Mostrar overlay de carga y desactivar botones
            LoadingOverlay.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            SetPointsCashButton.IsEnabled = false;
            RevertPointsCashButton.IsEnabled = false;
            ShowTeamsLastPointsButton.IsEnabled = false;

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
            var responseMessage = string.Join(Environment.NewLine, standings.Teams.Select(team => $"{team.TeamInfo.User.Name}: {team.LastPoints} pts"));

            await DisplayAlertAsync("Últims punts", responseMessage, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            // Ocultar overlay y reactivar botones
            LoadingOverlay.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            SetPointsCashButton.IsEnabled = true;
            RevertPointsCashButton.IsEnabled = true;
            ShowTeamsLastPointsButton.IsEnabled = true;
        }
    }

    private async Task ManagePunts(object sender, EventArgs e, bool isRevertingPoints)
    {
        try
        {
            // Mostrar overlay de carga y desactivar botones
            LoadingOverlay.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            SetPointsCashButton.IsEnabled = false;
            RevertPointsCashButton.IsEnabled = false;
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

            var responseMessage = string.Join(Environment.NewLine, postCashResults);

            await DisplayAlertAsync("Resultat", responseMessage, "OK");
            
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            // Ocultar overlay y reactivar botones
            LoadingOverlay.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            SetPointsCashButton.IsEnabled = true;
            RevertPointsCashButton.IsEnabled = true;
        }
    }

    private async Task<List<string>> ManagePostCash(StandingsModel standings, bool isRevertingPoints, HttpClient httpClient, SessionDataModel sessionData)
    {
        var postCashResults = new List<string>();

        foreach (var team in standings.Teams)
        {
            var canPostCash = int.TryParse(team.LastPoints, out var lastPoints) && lastPoints > 0;

            if (canPostCash)
            {
                var requestModel = new PostCashRequestModel
                {
                    Amount = lastPoints * 100000 * (isRevertingPoints ? -1 : 1),
                    Reason = isRevertingPoints ? "CORREGIR ERROR PUNTS" : "PUNTS"
                };

                await httpClient.PostWithoutResponseAsync($"https://api.comunio.es/communities/{sessionData.Community.Id}/users/{team.TeamInfo.User.Id}/penalties", requestModel);

                postCashResults.Add($"'{team.TeamInfo.User.Name}':  Diners {(isRevertingPoints ? "llevats" : "posats")}: {requestModel.Amount} ({team.LastPoints} pts) ");
            }
        }

        return postCashResults;
    }
}