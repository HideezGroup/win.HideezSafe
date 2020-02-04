﻿using Hideez.ARM;
using MvvmExtensions.Commands;
using MvvmExtensions.PropertyChangedMonitoring;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TemplateFactory
{
    class MainWindowViewModel : PropertyChangedImplementation
    {
        bool refreshing = false;

        public MainWindowViewModel() : base()
        {
            RefreshAppList();
        }

        public ObservableCollection<ExpandedAppInfo> Apps { get; } = new ObservableCollection<ExpandedAppInfo>();

        public bool Refreshing
        {
            get
            {
                return refreshing;
            }
            set
            {
                if (refreshing != value)
                {
                    refreshing = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public ICommand RefreshAppListCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) =>
                    {
                        RefreshAppList();
                    }
                };
            }
        }

        async void RefreshAppList()
        {
            try
            {
                Refreshing = true;

                await Task.Run(() =>
                {
                    var apps = AppInfoFactory.GetVisibleAppsInfo();

                    var expandedApps = apps.Select(a => new ExpandedAppInfo(a)).ToList();

                    var urls = new List<ExpandedAppInfo>();

                    foreach (var app in expandedApps)
                    {
                        if (!string.IsNullOrWhiteSpace(app.AppInfo.Domain))
                        {
                            urls.Add(new ExpandedAppInfo(app.AppInfo.Copy()));
                            app.AppInfo.Domain = string.Empty;
                        }
                    }

                    var uniqueApps = expandedApps.GroupBy(x => x.AppInfo.Description).Select(a => a.First()).ToList();

                    uniqueApps.AddRange(urls);

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Apps.Clear();
                        foreach (var app in uniqueApps)
                            Apps.Add(app);
                    });
                });
            }
            finally
            {
                Refreshing = false;
            }
        }
    }
}
