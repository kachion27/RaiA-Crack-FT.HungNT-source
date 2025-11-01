using System;
using System.Threading.Tasks;

namespace rikkei_education_service
{
    public interface IMainSocket
    {
        event EventHandler<bool> ConnectionStatusChanged;
        event EventHandler<string> MessageReceived;

        Task ConnectAsync();
        Task DisconnectAsync();
        bool isConnected();
        void Off(string eventName);
        void OnMessageReceived(string eventName);
        void OnMessageReceived(string eventName, Action<object> callback = null);
        Task SendMessageAsync(string eventName, string message);
        Task SendObjectAsync(string eventName, object data);
    }
}