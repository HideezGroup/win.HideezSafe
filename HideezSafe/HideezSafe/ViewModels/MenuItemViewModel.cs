﻿using HideezSafe.Modules;
using HideezSafe.Mvvm;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HideezSafe.ViewModels
{
    class MenuItemViewModel : LocalizedObject
    {
        private string header;
        private bool isCheckable;
        private bool isChecked;
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
