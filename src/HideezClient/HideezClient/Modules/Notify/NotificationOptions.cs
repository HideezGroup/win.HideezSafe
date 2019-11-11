﻿using System;
using System.Threading.Tasks;
using System.Windows;

namespace HideezClient.Modules
{
    public class NotificationOptions
    {
        public NotificationPosition Position { get; set; } = NotificationPosition.Normal;
        /// <summary>
        /// Close notification when window notificatiuon container deactivate
        /// </summary>
        public bool CloseWhenDeactivate { get; set; }
        public bool SetFocus { get; set; }
        public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(7);
        public TaskCompletionSource<bool> TaskCompletionSource { get; set; }
    }
}
