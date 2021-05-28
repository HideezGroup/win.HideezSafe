using HideezClient.Modules;
using MahApps.Metro.Controls;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HideezClient.Controls
{
    public abstract class NotificationBase : UserControl
    {
        readonly object lockObj = new object();
        readonly Storyboard ScaleInAnimation;
        readonly Storyboard ScaleOutAnimation;
        private int _closing = 0;
        private DispatcherTimer timer;


        protected NotificationBase(NotificationOptions options)
        {
            Options = options;
            
            ScaleInAnimation = (Storyboard)FindResource("ShowNotificationAnimation");
            ScaleOutAnimation = (Storyboard)FindResource("HideNotificationAnimation");

            Loaded += OnLoaded;
        }

        public event EventHandler Closed;

        public NotificationOptions Options { get; }

        public NotificationPosition Position
        {
            get
            {
                return Options.Position;
            }
        }

        public void ResetCloseTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Start();
            }
        }

        public void Close()
        {
            if (Interlocked.CompareExchange(ref _closing, 1, 0) == 0)
            {
                Options.TaskCompletionSource?.TrySetResult(false);

                var errorInStoryboard = false;
                try
                {
                    // TODO: Find out, why 'Cannot resolve all property references' occurs for RenderTransform.ScaleX
                    // Maybe, its somehow related to lock screen or resolution change when leaving suspended mode
                    // Maybe there was an attempt to close/animate it before its style was set.
                    Application.Current.Invoke(() => {
                        ScaleInAnimation.Stop(this);
                        ScaleOutAnimation.Begin(this, true);
                    });
                }
                catch (Exception) 
                {
                    // Some kind of error occured while starting animation
                    errorInStoryboard = true;
                }

                if (!errorInStoryboard)
                {
                    var delayDuration = ((Duration)FindResource("AnimationHideTime")).TimeSpan;
                    Application.Current.BeginInvoke(async () =>
                    {
                        // Wait for animation to finish
                        await Task.Delay(delayDuration);
                        Closed?.Invoke(this, EventArgs.Empty);
                    });
                }
                else
                {
                    Closed?.Invoke(this, EventArgs.Empty);
                }

            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            ScaleInAnimation.Begin(this, true);

            if (Options.SetFocus)
            {
                Focusable = true;
                Focus();
            }

            StartTimer(Options.CloseTimeout);
        }

        protected void StartTimer(TimeSpan time)
        {
            if (time != TimeSpan.Zero && timer == null)
            {
                timer = new DispatcherTimer
                {
                    Interval = time,
                };

                timer.Tick += Timer_Tick;
                timer.Start();
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            timer?.Stop();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            timer?.Start();
        }

        private void Timer_Tick(object s, EventArgs e)
        {
            Options.TaskCompletionSource?.TrySetResult(false);
            timer.Tick -= Timer_Tick;
            timer.Stop();
            Close();
        }
    }
}
