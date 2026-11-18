using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorArcade.Shared.Multiplayer;
using BlazorArcade.Shared.Scores;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace BlazorArcade.Services
{
    /// <summary>Where BlazorArcade.Server lives (ArcadeBackend:BaseUrl). Empty means online features are hidden.</summary>
    public sealed class ArcadeBackendOptions
    {
        public Uri? BaseUrl { get; }
        public bool IsConfigured => BaseUrl != null;

        public ArcadeBackendOptions(string? baseUrl)
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) BaseUrl = uri;
        }
    }

    /// <summary>The player's nickname and the private token that lets them reclaim a seat after a dropped connection.</summary>
    public sealed class PlayerProfile
    {
        private const string NameKey = "arcadePlayerName";
        private const string TokenKey = "arcadePlayerToken";
        private readonly IJSRuntime _js;

        public PlayerProfile(IJSRuntime js) => _js = js;

        public async Task<string> GetNameAsync() => await Get(NameKey) ?? "";

        public Task SetNameAsync(string name) => Set(NameKey, name);

        /// <summary>
        /// Per tab (sessionStorage), so two tabs in one browser are two players, while a refresh
        /// still gets you back into your seat.
        /// </summary>
        public async Task<string> GetTokenAsync()
        {
            var token = await Get("sessionStorage", TokenKey);
            if (string.IsNullOrEmpty(token))
            {
                token = Guid.NewGuid().ToString("N");
                await Set("sessionStorage", TokenKey, token);
            }
            return token;
        }

        private Task<string?> Get(string key) => Get("localStorage", key);

        private Task Set(string key, string value) => Set("localStorage", key, value);

        private async Task<string?> Get(string storage, string key)
        {
            try { return await _js.InvokeAsync<string?>($"{storage}.getItem", key); }
            catch (JSException) { return null; }
        }

        private async Task Set(string storage, string key, string value)
        {
            try { await _js.InvokeVoidAsync($"{storage}.setItem", key, value); }
            catch (JSException) { /* storage unavailable: fine, it just won't be remembered */ }
        }
    }

    /// <summary>High-score API client. Failures come back as null: the arcade must keep working offline.</summary>
    public sealed class ScoreboardClient
    {
        private readonly HttpClient? _http;

        public ScoreboardClient(ArcadeBackendOptions options)
        {
            if (options.IsConfigured) _http = new HttpClient { BaseAddress = options.BaseUrl, Timeout = TimeSpan.FromSeconds(10) };
        }

        public bool IsEnabled => _http != null;

        public async Task<IReadOnlyList<ScoreEntry>?> GetTopAsync(string game, int take = 10)
        {
            if (_http == null) return null;
            try { return await _http.GetFromJsonAsync<List<ScoreEntry>>($"api/scores/{game}?take={take}"); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return null; }
        }

        public async Task<SubmitScoreResponse?> SubmitAsync(string game, string player, int score)
        {
            if (_http == null) return null;
            try
            {
                var response = await _http.PostAsJsonAsync($"api/scores/{game}", new SubmitScoreRequest(player, score));
                return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SubmitScoreResponse>() : null;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return null; }
        }
    }

    /// <summary>
    /// One player's connection to the match hub. Owns a single room at a time and keeps track
    /// of which seat is ours, including across rematches (which swap seats) and reconnects.
    /// </summary>
    public sealed class MatchClient : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private string _name = "", _token = "";

        public RoomState? Room { get; private set; }
        public int Seat { get; private set; } = -1;
        public string? Error { get; private set; }
        public bool IsConnected => _connection.State == HubConnectionState.Connected;

        /// <summary>Raised whenever <see cref="Room"/>, <see cref="Seat"/>, <see cref="Error"/> or the connection state changes.</summary>
        public event Action? Changed;

        public MatchClient(ArcadeBackendOptions options)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(options.BaseUrl!, MatchHubRoutes.Path.TrimStart('/')))
                .WithAutomaticReconnect()
                .Build();

            _connection.On<RoomState>(MatchHubMethods.RoomUpdated, OnRoomUpdated);
            _connection.Reconnecting += _ => { Changed?.Invoke(); return Task.CompletedTask; };
            _connection.Closed += _ => { Changed?.Invoke(); return Task.CompletedTask; };
            // A reconnect gets a new connection id; rejoining with our token reclaims the seat.
            _connection.Reconnected += async _ =>
            {
                if (Room != null) Accept(await _connection.InvokeAsync<JoinResult>(MatchHubMethods.JoinRoom, Room.Code, _name, _token));
            };
        }

        public Task<bool> CreateAsync(string game, string name, string token) =>
            Enter(MatchHubMethods.CreateRoom, game, name, token);

        public Task<bool> JoinAsync(string code, string name, string token) =>
            Enter(MatchHubMethods.JoinRoom, code.Trim().ToUpperInvariant(), name, token);

        /// <returns>An error message when the server refused the move.</returns>
        public async Task<string?> SendMoveAsync(string move)
        {
            if (Room == null) return "Not in a room.";
            try { return await _connection.InvokeAsync<string?>(MatchHubMethods.MakeMove, Room.Code, move); }
            catch (Exception) { return "Connection lost."; }
        }

        public async Task RequestRematchAsync()
        {
            if (Room != null) await _connection.InvokeAsync(MatchHubMethods.RequestRematch, Room.Code);
        }

        public async Task LeaveAsync()
        {
            if (Room == null) return;
            var code = Room.Code;
            Room = null;
            Seat = -1;
            Changed?.Invoke();
            if (IsConnected) await _connection.InvokeAsync(MatchHubMethods.LeaveRoom, code);
        }

        private async Task<bool> Enter(string method, string gameOrCode, string name, string token)
        {
            _name = name;
            _token = token;
            Error = null;
            try
            {
                if (_connection.State == HubConnectionState.Disconnected) await _connection.StartAsync();
                return Accept(await _connection.InvokeAsync<JoinResult>(method, gameOrCode, name, token));
            }
            catch (Exception)
            {
                Error = "Can't reach the game server right now.";
                Changed?.Invoke();
                return false;
            }
        }

        private bool Accept(JoinResult result)
        {
            if (result.Ok)
            {
                Seat = result.Seat;
                Room = result.Room;
                Error = null;
            }
            else
            {
                Error = result.Error;
            }
            Changed?.Invoke();
            return result.Ok;
        }

        private void OnRoomUpdated(RoomState state)
        {
            if (Room != null && state.Code == Room.Code && state.GameNumber > Room.GameNumber && (state.GameNumber - Room.GameNumber) % 2 == 1)
                Seat = 1 - Seat; // rematches swap seats so the other player moves first
            Room = state;
            Changed?.Invoke();
        }

        public async ValueTask DisposeAsync()
        {
            try { await LeaveAsync(); } catch (Exception) { /* closing anyway */ }
            await _connection.DisposeAsync();
        }
    }
}
