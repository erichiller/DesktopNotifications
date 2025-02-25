﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tmds.DBus;

namespace DesktopNotifications.FreeDesktop
{
    public class FreeDesktopNotificationManager : INotificationManager, IDisposable
    {
        private readonly FreeDesktopApplicationContext _appContext;
        private const    string                        NotificationsService = "org.freedesktop.Notifications";

        private static readonly ObjectPath                     NotificationsPath = new ObjectPath("/org/freedesktop/Notifications");
        private readonly        Dictionary<uint, Notification> _activeNotifications;
        private                 Connection?                    _connection;
        private                 IDisposable?                   _notificationActionSubscription;
        private                 IDisposable?                   _notificationCloseSubscription;

        private IFreeDesktopNotificationsProxy? _proxy;
        // KILL - added by EDH
        public IFreeDesktopNotificationsProxy Proxy => _proxy;
        // KILL - added by EDH
        public FreeDesktopApplicationContext AppContext => _appContext;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appContext"></param>
        public FreeDesktopNotificationManager(FreeDesktopApplicationContext? appContext = null) {
            _appContext          = appContext ?? FreeDesktopApplicationContext.FromCurrentProcess();
            _activeNotifications = new Dictionary<uint, Notification>();
        }

        public void Dispose() {
            _notificationActionSubscription?.Dispose();
            _notificationCloseSubscription?.Dispose();
        }

        public event EventHandler<NotificationActivatedEventArgs>? NotificationActivated;
        public event EventHandler<NotificationDismissedEventArgs>? NotificationDismissed;

        public string? LaunchActionId { get; }

        public async Task Initialize() {
            _connection = Connection.Session;

            await _connection.ConnectAsync();

            _proxy = _connection.CreateProxy<IFreeDesktopNotificationsProxy>(
                NotificationsService,
                NotificationsPath
            );

            _notificationActionSubscription = await _proxy.WatchActionInvokedAsync(
                OnNotificationActionInvoked,
                OnNotificationActionInvokedError
            );
            _notificationCloseSubscription = await _proxy.WatchNotificationClosedAsync(
                OnNotificationClosed,
                OnNotificationClosedError
            );
        }

        public Task ShowNotification(Notification notification, DateTimeOffset? expirationTime = null) => ShowNotification(notification, null, expirationTime); 

        public async Task ShowNotification(Notification notification, string? soundName = null, DateTimeOffset? expirationTime = null) {
            if (_connection == null || _proxy == null) {
                throw new InvalidOperationException("Not connected. Call Initialize() first.");
            }

            if (expirationTime < DateTimeOffset.Now) {
                throw new ArgumentException("Expiration time can not be earlier than now", nameof(expirationTime));
            }

            var duration = expirationTime - DateTimeOffset.Now;
            var actions  = GenerateActions(notification);

            var hints =
                new Dictionary<string, object> {
                    { "urgency", 1 },
                };
            if ( ! String.IsNullOrWhiteSpace(soundName)) {
                hints.Add("sound-name", soundName);
            }

            var id = await _proxy.NotifyAsync(
                // _appContext.Name, // KILL -- edh
                "example.avalonia",
                0,
                _appContext.AppIcon ?? string.Empty,
                notification.Title  ?? throw new ArgumentException("Notification is missing a title", nameof(notification)),
                notification.Body   ?? throw new ArgumentException("Notification is missing a body", nameof(notification)),
                actions.ToArray(),
                hints,
                duration?.Milliseconds ?? 0
            ).ConfigureAwait(false);

            _activeNotifications[id] = notification;
        }

        public async Task ScheduleNotification(
            Notification    notification,
            DateTimeOffset  deliveryTime,
            DateTimeOffset? expirationTime = null
        ) {
            if (deliveryTime < DateTimeOffset.Now || deliveryTime > expirationTime) {
                throw new ArgumentException("Delivery time can not be earlier than now or later than the expiration time", nameof(deliveryTime));
            }

            //Note: We could consider spawning some daemon that sends the notification at the specified time.
            //For now we only allow to schedule notifications while the application is running.
            await Task.Delay(deliveryTime - DateTimeOffset.Now);

            await ShowNotification(notification, expirationTime);
        }

        private static IEnumerable<string> GenerateActions(Notification notification) {
            foreach (var (title, actionId) in notification.Buttons) {
                yield return actionId;
                yield return title;
            }
        }

        private void OnNotificationClosedError(Exception obj) {
            throw obj;
        }

        private static NotificationDismissReason GetReason(uint reason) {
            return reason switch {
                       1 => NotificationDismissReason.Expired,
                       2 => NotificationDismissReason.User,
                       3 => NotificationDismissReason.Application,
                       _ => throw new ArgumentOutOfRangeException(nameof(reason))
                   };
        }

        private void OnNotificationClosed((uint id, uint reason) @event) {
            if (_activeNotifications.ContainsKey(@event.id)) {
                var notification = _activeNotifications[@event.id];
                _activeNotifications.Remove(@event.id);

                // //TODO: Not sure why but it calls this event twice sometimes
                // //In this case the notification has already been removed from the dict.
                // if (notification == null)
                // {
                //     return;
                // }

                var dismissReason = GetReason(@event.reason);

                Console.WriteLine($"Notification key {@event.id} has dismiss reason: {dismissReason}");
                NotificationDismissed?.Invoke(this,
                                              new NotificationDismissedEventArgs(notification, dismissReason));
            }
            else {
                Console.WriteLine($"Notification key {@event.id} was not found in _activeNotifications");
            }
        }

        private void OnNotificationActionInvokedError(Exception obj) {
            throw obj;
        }

        private void OnNotificationActionInvoked((uint id, string actionKey) @event) {
            var notification = _activeNotifications[@event.id];

            NotificationActivated?.Invoke(this,
                                          new NotificationActivatedEventArgs(notification, @event.actionKey));
        }
    }
}