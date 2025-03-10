using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TicTacToeServer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var server = new TicTacToeServer();
            await server.StartAsync();
        }
    }

    public class TicTacToeServer
    {
        private TcpListener _listener;
        private List<TcpClient> _clients = new List<TcpClient>();
        private char[] _gameBoard = new char[9];
        private int _currentPlayer = 0; // 0 - pierwszy gracz (X), 1 - drugi gracz (O)
        private bool _gameInProgress = false;
        private readonly int _port = 5000;

        public TicTacToeServer()
        {
            // Inicjalizacja pustej planszy
            for (int i = 0; i < 9; i++)
            {
                _gameBoard[i] = ' ';
            }
        }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"Serwer uruchomiony na porcie {_port}.");

            try
            {
                while (true)
                {
                    if (_clients.Count < 2)
                    {
                        Console.WriteLine("Oczekiwanie na graczy...");
                        var client = await _listener.AcceptTcpClientAsync();
                        _clients.Add(client);

                        int playerNumber = _clients.Count;
                        Console.WriteLine($"Gracz {playerNumber} połączony.");

                        // Poinformuj gracza o jego numerze
                        var stream = client.GetStream();
                        var playerInfo = $"PLAYER:{playerNumber}";
                        var buffer = Encoding.ASCII.GetBytes(playerInfo);
                        await stream.WriteAsync(buffer, 0, buffer.Length);

                        // Jeśli to drugi gracz, rozpocznij grę
                        if (_clients.Count == 2)
                        {
                            _gameInProgress = true;
                            ResetGame();
                            await BroadcastGameStateAsync();
                        }

                        // Obsługa komunikacji z klientem w osobnym wątku
                        _ = HandleClientAsync(client, playerNumber - 1);
                    }
                    else
                    {
                        // Poczekaj na zwolnienie miejsca
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd serwera: {ex.Message}");
            }
            finally
            {
                _listener.Stop();
            }
        }

        private async Task HandleClientAsync(TcpClient client, int playerIndex)
        {
            try
            {
                var buffer = new byte[1024];
                var stream = client.GetStream();

                while (client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    var message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    await ProcessMessageAsync(message, playerIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas obsługi klienta {playerIndex + 1}: {ex.Message}");
            }
            finally
            {
                // Usunięcie klienta i reset gry
                _clients.Remove(client);
                client.Close();
                Console.WriteLine($"Gracz {playerIndex + 1} rozłączony.");

                if (_gameInProgress)
                {
                    _gameInProgress = false;
                    await BroadcastMessageAsync("OPPONENT_DISCONNECTED");
                }
            }
        }

        private async Task ProcessMessageAsync(string message, int playerIndex)
        {
            if (!_gameInProgress || _clients.Count != 2)
            {
                await SendToClientAsync(playerIndex, "WAIT:Oczekiwanie na drugiego gracza.");
                return;
            }

            if (_currentPlayer != playerIndex)
            {
                await SendToClientAsync(playerIndex, "NOT_YOUR_TURN:To nie jest Twoja kolej.");
                return;
            }

            if (message.StartsWith("MOVE:"))
            {
                var moveStr = message.Substring(5);
                if (int.TryParse(moveStr, out int move) && move >= 0 && move < 9)
                {
                    if (_gameBoard[move] == ' ')
                    {
                        // Wykonanie ruchu
                        _gameBoard[move] = _currentPlayer == 0 ? 'X' : 'O';

                        // Sprawdzenie wygranej
                        var gameState = CheckGameState();

                        // Zmiana gracza jeśli gra trwa
                        if (gameState == "CONTINUE")
                        {
                            _currentPlayer = 1 - _currentPlayer; // Zmiana gracza (0->1, 1->0)
                        }
                        else
                        {
                            _gameInProgress = false;
                        }

                        // Wysłanie stanu gry do obu graczy
                        await BroadcastGameStateAsync();
                    }
                    else
                    {
                        await SendToClientAsync(playerIndex, "INVALID_MOVE:To pole jest już zajęte.");
                    }
                }
                else
                {
                    await SendToClientAsync(playerIndex, "INVALID_MOVE:Nieprawidłowy ruch.");
                }
            }
        }

        private async Task SendToClientAsync(int playerIndex, string message)
        {
            if (playerIndex >= 0 && playerIndex < _clients.Count)
            {
                var client = _clients[playerIndex];
                if (client.Connected)
                {
                    var buffer = Encoding.ASCII.GetBytes(message);
                    await client.GetStream().WriteAsync(buffer, 0, buffer.Length);
                }
            }
        }

        private async Task BroadcastMessageAsync(string message)
        {
            foreach (var client in _clients)
            {
                if (client.Connected)
                {
                    var buffer = Encoding.ASCII.GetBytes(message);
                    await client.GetStream().WriteAsync(buffer, 0, buffer.Length);
                }
            }
        }

        private async Task BroadcastGameStateAsync()
        {
            var gameState = CheckGameState();
            var boardString = new String(_gameBoard);
            var message = $"GAME_STATE:{boardString}|{_currentPlayer}|{gameState}";

            await BroadcastMessageAsync(message);
        }

        private string CheckGameState()
        {
            // Sprawdzenie wygranej w poziomie
            for (int i = 0; i < 9; i += 3)
            {
                if (_gameBoard[i] != ' ' && _gameBoard[i] == _gameBoard[i + 1] && _gameBoard[i] == _gameBoard[i + 2])
                {
                    return $"WIN:{(_gameBoard[i] == 'X' ? 0 : 1)}";
                }
            }

            // Sprawdzenie wygranej w pionie
            for (int i = 0; i < 3; i++)
            {
                if (_gameBoard[i] != ' ' && _gameBoard[i] == _gameBoard[i + 3] && _gameBoard[i] == _gameBoard[i + 6])
                {
                    return $"WIN:{(_gameBoard[i] == 'X' ? 0 : 1)}";
                }
            }

            // Sprawdzenie wygranej na ukos
            if (_gameBoard[0] != ' ' && _gameBoard[0] == _gameBoard[4] && _gameBoard[0] == _gameBoard[8])
            {
                return $"WIN:{(_gameBoard[0] == 'X' ? 0 : 1)}";
            }

            if (_gameBoard[2] != ' ' && _gameBoard[2] == _gameBoard[4] && _gameBoard[2] == _gameBoard[6])
            {
                return $"WIN:{(_gameBoard[2] == 'X' ? 0 : 1)}";
            }

            // Sprawdzenie remisu
            bool isBoardFull = true;
            for (int i = 0; i < 9; i++)
            {
                if (_gameBoard[i] == ' ')
                {
                    isBoardFull = false;
                    break;
                }
            }

            if (isBoardFull)
            {
                return "DRAW";
            }

            // Gra trwa dalej
            return "CONTINUE";
        }

        private void ResetGame()
        {
            for (int i = 0; i < 9; i++)
            {
                _gameBoard[i] = ' ';
            }
            _currentPlayer = 0;
        }
    }
}