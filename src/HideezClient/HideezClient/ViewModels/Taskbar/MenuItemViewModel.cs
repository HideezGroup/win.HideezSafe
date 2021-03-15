﻿using HideezClient.Modules;
using HideezMiddleware.Localize;
using HideezClient.Mvvm;
using NLog.LayoutRenderers;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using HideezClient.Modules.Localize;

namespace HideezClient.ViewModels
{
    public class MenuItemViewModel : LocalizedObject
    {
        private string header;
        private string description;
        private bool isCheckable;
        private bool isChecked;
        private bool isVisible = true;
        private ICommand menuItemCommand;
        private object commandParameter;
        private ObservableCollection<MenuItemViewModel> menuItems;

        /// <summary>
        /// Key for localize text menu.
        /// </summary>
        [Localization]
        public string Header
        {
            get { return L(header); }
            set
            {
                Set(ref header, value);
            }
        }

        [Localization]
        public string Description
        {
            get { return description != null ? L(description) : null; }
            set
            {
                Set(ref description, value);
            }
        }

        public bool IsCheckable
        {
            get { return isCheckable; }
            set
            {
                Set(ref isCheckable, value);
            }
        }

        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                Set(ref isChecked, value);
            }
        }

        public bool IsVisible
        {
            get { return isVisible; }
            set { Set(ref isVisible, value); }
        }

        public bool HasMenuItems 
        {
            get => MenuItems != null && MenuItems.Count > 0; 
        }

        public ICommand Command
        {
            get { return menuItemCommand; }
            set
            {
                Set(ref menuItemCommand, value);
            }
        }

        public object CommandParameter
        {
            get { return commandParameter; }
            set
            {
                Set(ref commandParameter, value);
            }
        }

        /// <summary>
        /// Inner menu items
        /// </summary>
        public ObservableCollection<MenuItemViewModel> MenuItems
        {
            get { return menuItems; }
            set { Set(ref menuItems, value); }
        }
    }
}
