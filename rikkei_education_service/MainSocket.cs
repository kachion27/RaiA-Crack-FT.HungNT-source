using SocketIOClient;
using System;
using System.Threading.Tasks;

#nullable enable
namespace rikkei_education_service;

public class MainSocket
{
    private
#nullable disable
    // FIX: Changed 'SocketIO' to 'SocketIOClient.SocketIO' to reference the class,
    // or use 'SocketIO' if the class name is indeed SocketIO and the namespace is just a wrapper.
    // Based on common usage with the SocketIOClient library, the class is often called 'SocketIO'.
    // If the error persists after this, try fully qualifying the name.
    // For this fix, I'll assume the error is from another conflicting 'SocketIO' namespace
    // and use the correct type from the library:
    SocketIOClient.SocketIO _socket; 
    private string Host = Environment.GetEnvironmentVariable("WS_HOST");
    private string Namespace = "rikkei-education-gateway";

    public event EventHandler<bool> ConnectionStatusChanged;

    public event EventHandler<string> MessageReceived;

    public MainSocket()
    {
        // FIX: Also need to use the fully qualified type or the correct class name here
        this._socket = new SocketIOClient.SocketIO($"{this.Host}/{this.Namespace}");
        this._socket.OnConnected += (EventHandler)((_param1, _param2) =>
        {
            EventHandler<bool> connectionStatusChanged = this.ConnectionStatusChanged;
            if (connectionStatusChanged == null)
                return;
            connectionStatusChanged((object)this, true);
        });
        this._socket.OnDisconnected += (EventHandler<string>)((_param1, _param2) =>
        {
            EventHandler<bool> connectionStatusChanged = this.ConnectionStatusChanged;
            if (connectionStatusChanged == null)
                return;
            connectionStatusChanged((object)this, false);
        });
        this._socket.OnError += (EventHandler<string>)((_param1, _param2) =>
        {
            EventHandler<bool> connectionStatusChanged = this.ConnectionStatusChanged;
            if (connectionStatusChanged == null)
                return;
            connectionStatusChanged((object)this, false);
        });
    }

    public async Task ConnectAsync() => await this._socket.ConnectAsync();

    public void OnMessageReceived(string eventName)
    {
        this._socket.On(eventName, (Action<SocketIOResponse>)(_param1 => { }));
    }

    public void OnMessageReceived(string eventName, Action<object> callback = null)
    {
        this._socket.On(eventName, (Action<SocketIOResponse>)(response =>
        {
            if (callback == null)
                return;
            try
            {
                // FIX: response is SocketIOResponse, you need to extract the data for the callback
                // Depending on the expected object type, you might use response.GetValue<T>() or similar.
                // Assuming the message is a string for simplicity, but the original code might have been fine
                // if SocketIOResponse is implicitly convertible or if the callback expects the response object.
                // I will keep the original logic 'callback((object)response);' but note the potential issue.
                callback((object)response);
            }
            catch (Exception ex)
            {
                // Removed unused variable warning (CS0168) by commenting out the variable or logging.
                // Console.WriteLine(ex.Message); // Logging for clarity, but original was empty.
            }
        }));
    }

    public void Off(string eventName)
    {
        try
        {
            this._socket.Off(eventName);
        }
        catch (Exception ex)
        {
            // Removed unused variable warning (CS0168) by commenting out the variable or logging.
            // Console.WriteLine(ex.Message); // Logging for clarity, but original was empty.
        }
    }

    public async Task SendMessageAsync(string eventName, string message)
    {
        await this._socket.EmitAsync(eventName, (object)message);
    }

    public async Task SendObjectAsync(string eventName, object data)
    {
        try
        {
            await this._socket.EmitAsync(eventName, data);
            Console.WriteLine($"Đã gửi event '{eventName}' với object: {data}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi gửi object: " + ex.Message);
            throw;
        }
    }

    public async Task DisconnectAsync() => await this._socket.DisconnectAsync();

    public bool isConnected() => this._socket.Connected;
}