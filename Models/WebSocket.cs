using System.Net.WebSockets;
using DotnetBackend.Services;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotnetBackend.Models;

public class WebSocketHandler
{
    private readonly ExtractService _extractService;
    private readonly List<WebSocket> _clients = new List<WebSocket>();

    public WebSocketHandler(ExtractService extractService)
    {
        _extractService = extractService;
    }

    public async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        _clients.Add(webSocket);

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var extracts = await _extractService.GetLast50ExtractsAsync();

                var message = Newtonsoft.Json.JsonConvert.SerializeObject(new { @event = "new_extracts", extracts });
                var bytes = System.Text.Encoding.UTF8.GetBytes(message);

                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

                await Task.Delay(5000);
            }
        }
        finally
        {
            _clients.Remove(webSocket);
        }
    }

    public async Task SendNewClientAsync(Client newClient)
    {
        var message = Newtonsoft.Json.JsonConvert.SerializeObject(new { @event = "new_client", client = newClient });
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);

        Console.WriteLine($"Enviando novo cliente: {newClient.Id}, {newClient.Name}"); // Verifique este log

        foreach (var client in _clients)
        {
            if (client.State == WebSocketState.Open)
            {
                // Adicione um log aqui para verificar o que está sendo enviado
                Console.WriteLine($"Enviando mensagem para o WebSocket: {message}");
                await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}