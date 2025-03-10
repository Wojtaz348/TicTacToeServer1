using System.Net.Sockets;
using System.Text;

namespace TicTacToeClient;

public partial class MainPage : ContentPage
{
    private TcpClient _client;
    private NetworkStream _stream;
    private bool _isConnected = false;
    private int _playerNumber = -1;
    private int _currentPlayer = 0;
    private Button[] _boardButtons;
    private CancellationTokenSource _cts;

    public MainPage()
    {
        InitializeComponent();
        _boardButtons = new Button[9]
        {
            Button0, Button1, Button2,
            Button3, Button4, Button5,
            Button6, Button7, Button8
        };

        // Przypisanie obsługi kliknięcia dla każdego przycisku planszy
        for (int i = 0; i < 9; i++)
        {
            int index = i; // Wymagane do prawidłowego przechwycenia indeksu w lambda
            _boardButtons[i].Clicked += async (sender, e) => await OnBoardButtonClicked(index);
        }
    }

    private async void OnConnectClicked(object sender, EventArgs e)
    {
        if (_isConnected)
        {
            await DisconnectAsync();
            ConnectButton.Text = "Połącz";
            StatusLabel.Text = "Rozłączono";
            return;
        }

        string serverIp = ServerIpEntry.Text;
        if (string.IsNullOrEmpty(serverIp))
        {
            await DisplayAlert("Błąd", "Podaj adres IP serwera", "OK");
            return;
        }

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(serverIp, 5000);
            _stream = _client.GetStream();
            _isConnected = true;

            _cts = new CancellationTokenSource();
            _ = ReceiveMessagesAsync(_cts.Token);

            ConnectButton.Text = "Rozłącz";
            StatusLabel.Text = "Połączono";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd połączenia", ex.Message, "OK");
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            byte[] buffer = new byte[1024];

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                if (_client.Available > 0 || _stream.DataAvailable)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        await ProcessServerMessageAsync(message);
                    }
                }

                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Obsługa anulowania
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Błąd odbierania danych", ex.Message, "OK");
                await DisconnectAsync();
            });
        }
    }

    private async Task ProcessServerMessageAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (message.StartsWith("PLAYER:"))
            {
                _playerNumber = int.Parse(message.Substring(7));
                PlayerLabel.Text = $"Grasz jako: {(_playerNumber == 1 ? "X" : "O")}";
                StatusLabel.Text = "Oczekiwanie na drugiego gracza...";
            }
            else if (message.StartsWith("GAME_STATE:"))
            {
                var parts = message.Substring(11).Split('|');
                if (parts.Length == 3)
                {
                    var board = parts[0];
                    _currentPlayer = int.Parse(parts[1]);
                    var gameState = parts[2];

                    UpdateBoard(board);
                    UpdateGameStatus(gameState);
                }
            }
            else if (message.StartsWith("INVALID_MOVE:"))
            {
                await DisplayAlert("Błędny ruch", message.Substring(13), "OK");
            }
            else if (message.StartsWith("NOT_YOUR_TURN:"))
            {
                StatusLabel.Text = "To nie jest Twoja kolej!";
            }
            else if (message.StartsWith("WAIT:"))
            {
                StatusLabel.Text = message.Substring(5);
            }
            else if (message == "OPPONENT_DISCONNECTED")
            {
                await DisplayAlert("Informacja", "Przeciwnik rozłączył się. Gra zostanie zresetowana.", "OK");
                ResetBoard();
                StatusLabel.Text = "Oczekiwanie na drugiego gracza...";
            }
        });
    }

    private void UpdateBoard(string boardState)
    {
        if (boardState.Length != 9) return;

        for (int i = 0; i < 9; i++)
        {
            _boardButtons[i].Text = boardState[i] == ' ' ? "" : boardState[i].ToString();
            _boardButtons[i].IsEnabled = boardState[i] == ' ';
        }
    }

    private void UpdateGameStatus(string gameState)
    {
        if (gameState == "CONTINUE")
        {
            bool isMyTurn = (_playerNumber - 1) == _currentPlayer;
            StatusLabel.Text = isMyTurn ? "Twoja kolej" : "Kolej przeciwnika";
        }
        else if (gameState.StartsWith("WIN:"))
        {
            int winner = int.Parse(gameState.Substring(4));
            if (winner == (_playerNumber - 1))
            {
                StatusLabel.Text = "Wygrałeś!";
            }
            else
            {
                StatusLabel.Text = "Przegrałeś!";
            }
            DisableBoardButtons();
        }
        else if (gameState == "DRAW")
        {
            StatusLabel.Text = "Remis!";
            DisableBoardButtons();
        }
    }

    private void DisableBoardButtons()
    {
        foreach (var button in _boardButtons)
        {
            button.IsEnabled = false;
        }
    }

    private void ResetBoard()
    {
        foreach (var button in _boardButtons)
        {
            button.Text = "";
            button.IsEnabled = true;
        }
    }

    private async Task OnBoardButtonClicked(int index)
    {
        if (!_isConnected) return;

        try
        {
            string message = $"MOVE:{index}";
            byte[] buffer = Encoding.ASCII.GetBytes(message);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie można wysłać ruchu: {ex.Message}", "OK");
        }
    }

    private async Task DisconnectAsync()
    {
        _isConnected = false;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_stream != null)
        {
            _stream.Close();
            _stream = null;
        }

        if (_client != null)
        {
            _client.Close();
            _client = null;
        }

        ResetBoard();
        PlayerLabel.Text = "Gracz: -";
        StatusLabel.Text = "Niepołączony";
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await DisconnectAsync();
    }
}