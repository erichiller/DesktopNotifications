using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DesktopNotifications;

namespace Example.Avalonia
{
    public class MainWindow : Window
    {
        private readonly TextBox   _bodyTextBox;
        private readonly ListBox   _eventsListBox;
        private readonly ListBox   _capabilitiesListBox;
        private readonly TextBox   _titleTextBox;
        private readonly TextBox   _soundNameTextBox;
        private readonly TextBlock _serverInfoTextBlock;
        // private readonly INotificationManager _notificationManager;
        private readonly DesktopNotifications.FreeDesktop.FreeDesktopNotificationManager _notificationManager;

        public MainWindow() {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            _titleTextBox              = this.FindControl<TextBox>("TitleTextBox");
            _bodyTextBox               = this.FindControl<TextBox>("BodyTextBox");
            _soundNameTextBox          = this.FindControl<TextBox>("SoundNameTextBox");
            _serverInfoTextBlock       = this.FindControl<TextBlock>("ServerInfoTextBox");
            _eventsListBox             = this.FindControl<ListBox>("EventsListBox");
            _capabilitiesListBox       = this.FindControl<ListBox>("CapabilitiesListBox");
            _capabilitiesListBox.Items = new ObservableCollection<string>();
            _eventsListBox.Items       = new ObservableCollection<string>();

            _notificationManager = AvaloniaLocator.Current.GetService<INotificationManager>() as DesktopNotifications.FreeDesktop.FreeDesktopNotificationManager ??
                                   throw new InvalidOperationException("Missing notification manager");
            _notificationManager.NotificationActivated += OnNotificationActivated;
            _notificationManager.NotificationDismissed += OnNotificationDismissed;

            if (_notificationManager.LaunchActionId != null) {
                ((IList<string>)_eventsListBox.Items).Add($"Launch action: {_notificationManager.LaunchActionId}");
            }

            Task.Run(_notificationManager.Proxy.GetServerInformationAsync)
                .ContinueWith(async f => {
                    Console.WriteLine("GetServerInformationAsync");
                    var result = await f;
                    Console.WriteLine($@"
            name:           {result.name}
            spec_version:   {result.spec_version}
            vendor:         {result.vendor}
            version:        {result.version}
            ");
                    ;
                    Dispatcher.UIThread.Post(() => {
                        var fdManager = _notificationManager as DesktopNotifications.FreeDesktop.FreeDesktopNotificationManager ?? throw new Exception();

                        _serverInfoTextBlock.Text = $@"Client (Self):
            name:           {fdManager.AppContext.Name}
            icon:           {fdManager.AppContext.AppIcon}
            " + "\n\n" + $@"Server:
            name:           {result.name}
            spec_version:   {result.spec_version}
            vendor:         {result.vendor}
            version:        {result.version}
            ";
                    });
                });


            Task.Run(
                (_notificationManager as DesktopNotifications.FreeDesktop.FreeDesktopNotificationManager
                 ?? throw new Exception()
                )!.Proxy.GetCapabilitiesAsync
            ).ContinueWith(async f => {
                var foo = await f ?? throw new Exception();
                Dispatcher.UIThread.Post(() => {
                    foreach (var item in foo) {
                        (_capabilitiesListBox.Items as ObservableCollection<string>).Add(item);
                    }
                });
            });
        }

        private void OnNotificationDismissed(object? sender, NotificationDismissedEventArgs e) {
            ((IList<string>)_eventsListBox.Items).Add($"Notification dismissed: {e.Reason}");
        }

        private void OnNotificationActivated(object? sender, NotificationActivatedEventArgs e) {
            ((IList<string>)_eventsListBox.Items).Add($"Notification activated: {e.ActionId}");
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }

        public void Show_OnClick(object? sender, RoutedEventArgs e) {
            Debug.Assert(_notificationManager != null);

            _notificationManager.ShowNotification(new Notification {
                Title = _titleTextBox.Text ?? _titleTextBox.Watermark,
                Body  = _bodyTextBox.Text  ?? _bodyTextBox.Watermark,
                Buttons =
                {
                ("This is awesome!",
                "awesome")
            }
            }, soundName: _soundNameTextBox.Text );
        }

        private void Schedule_OnClick(object? sender, RoutedEventArgs e) {
            _notificationManager.ScheduleNotification(new Notification {
                Title = _titleTextBox.Text ?? _titleTextBox.Watermark,
                Body  = _bodyTextBox.Text  ?? _bodyTextBox.Watermark
            }, DateTimeOffset.Now + TimeSpan.FromSeconds(5));
        }
    }
}