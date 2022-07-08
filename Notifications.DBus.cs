using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Tmds.DBus;

[assembly: InternalsVisibleTo(Tmds.DBus.Connection.DynamicAssemblyName)]
namespace Notifications.DBus
{
    [DBusInterface("org.freedesktop.Notifications")]
    interface INotifications : IDBusObject
    {
        Task<uint> NotifyAsync(string Arg0, uint Arg1, string Arg2, string Arg3, string Arg4, string[] Arg5, IDictionary<string, object> Arg6, int Arg7);
        Task CloseNotificationAsync(uint Arg0);
        Task<string[]> GetCapabilitiesAsync();
        Task<(string arg0, string arg1, string arg2, string arg3)> GetServerInformationAsync();
        Task<IDisposable> WatchNotificationClosedAsync(Action<(uint arg0, uint arg1)> handler, Action<Exception> onError = null);
        Task<IDisposable> WatchActionInvokedAsync(Action<(uint arg0, string arg1)> handler, Action<Exception> onError = null);
    }
}