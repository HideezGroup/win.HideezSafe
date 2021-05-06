/*The Original source code of this control and it's style in Generic.Xaml is by
 * Paul Middlemiss - http://www.silverlightshow.net/items/Create-a-Custom-Control-Inheriting-from-TextBox.aspx
 * who licensed and released this control and it's style under the Creative Commons license
 * visit http://creativecommons.org/licenses/by/3.0/us for more information
 * Feel free to use this control and style with proper attribution.
 * The rest of the code and modification is by Bond - http://www.codeproject.com/Members/bonded, 
 * and well, licensed under CODE PROJECT OPEN LICENSE where you downloaded this project. :)
 *  */
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using System.Windows.Interop;

namespace HideezClient.Modules.HotkeyManager.BondTech.HotKeyManagement
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:BondTech.HotKeyManagement.WPF._4"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:BondTech.HotKeyManagement.WPF._4;assembly=BondTech.HotKeyManagement.WPF._4"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:HotKeyControl/>
    ///
    /// </summary>
    [DefaultProperty("ForceModifiers"), DefaultEvent("HotKeyIsSet")]
    public class HotKeyControl : TextBox
    {
        #region **Properties.
        HwndSource hwndSource;
        HwndSourceHook hook;

        /// <summary>Immediate text update dependency property
        /// </summary>
        public static readonly DependencyProperty IsUpdateImmediateProperty = DependencyProperty.Register(
            "IsUpdateImmediate",
            typeof(bool),
            typeof(HotKeyControl),
            new PropertyMetadata(false));


        /// <summary>Identifies the HotKey control ForceModifiers dependency property.
        /// </summary>
        public static readonly DependencyProperty ForceModifiersProperty = DependencyProperty.Register(
            "ForceModifiers",
            typeof(Boolean),
            typeof(HotKeyControl),
            new PropertyMetadata(true));

        /// <summary>Identifies the HasError dependency property.
        /// </summary>
        public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
            "HasError",
            typeof(Boolean),
            typeof(HotKeyControl),
            new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets a value indicating whether bindings on the Text property updates
        /// as soon as the text change. This is a dependency property.
        /// </summary>
        /// <value>If true then TextChanges fires whenever the text changes, else only on LostFocus</value>
        [Category("Watermark")]
        [Description("Gets or sets a value indicating whether the binding source is updated immediately as text changes, or on LostFocus")]
        public bool IsUpdateImmediate
        {
            get { return (bool)GetValue(IsUpdateImmediateProperty); }
            set { SetValue(IsUpdateImmediateProperty, value); }
        }

        /// <summary>Gets or sets a value specifying that the user should be forced to enter modifiers. This is a dependency property.
        /// </summary>
        [Bindable(true), EditorBrowsable(EditorBrowsableState.Always), Browsable(true)]
        [Description("Gets or sets a value specifying that the user be forced to enter modifiers.")]
        public bool ForceModifiers
        {
            get { return (bool)GetValue(ForceModifiersProperty); }
            set { SetValue(ForceModifiersProperty, value); }
        }

        /// <summary>Returns the key set by the user.
        /// </summary>
        [Browsable(false)]
        public Keys UserKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.Text) && this.Text != Keys.None.ToString())
                {
                    return (Keys)HotKeyShared.ParseShortcut(this.Text).GetValue(1);
                }
                return Keys.None;
            }
        }

        /// <summary>Returns the Modifier set by the user.
        /// </summary>
        [Browsable(false)]
        public ModifierKeys UserModifier
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.Text) && this.Text != Keys.None.ToString())
                {
                    return (ModifierKeys)HotKeyShared.ParseShortcut(this.Text).GetValue(0);
                }
                return ModifierKeys.None;
            }
        }
        
        /// <summary> Gets or sets a value indicating that the error notification should be shown to the user. This is a dependency property.
        /// </summary>
        [Bindable(true), EditorBrowsable(EditorBrowsableState.Always), Browsable(true)]
        public bool HasError
        {
            get { return (bool)GetValue(HasErrorProperty); }
            set { SetValue(HasErrorProperty, value); }
        }
        #endregion

        #region **Events
        public static readonly RoutedEvent HotKeyIsSetEvent = EventManager.RegisterRoutedEvent(
            "HotKeyIsSet", RoutingStrategy.Bubble, typeof(HotKeyIsSetEventHandler), typeof(HotKeyControl));

        [Category("Behaviour")]
        public event HotKeyIsSetEventHandler HotKeyIsSet
        {
            add { AddHandler(HotKeyIsSetEvent, value); }
            remove { RemoveHandler(HotKeyIsSetEvent, value); }
        }
        #endregion

        #region **Constructor.
        static HotKeyControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HotKeyControl), new FrameworkPropertyMetadata(typeof(HotKeyControl)));
        }

        public HotKeyControl()
        {
            this.GotFocus += this.TextBoxGotFocus;
            this.LostFocus += this.TextBoxLostFocus;
            this.TextChanged += this.TextBoxTextChanged;
            this.PreviewKeyDown += this.TextBoxKeyDown;
            this.MouseLeave += this.TextBoxMouseOut;

            this.hook = new HwndSourceHook(WndProc);
            this.ContextMenu = null; //Disable shortcuts.
            this.IsReadOnly = true;
            this.AllowDrop = false;
        }
        #endregion

        #region **Helpers
        /// <summary>When overridden in a derived class, is invoked whenever application code or internal processes (such as a rebuilding layout pass) call <see cref="M:System.Windows.Controls.Control.ApplyTemplate"/>.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var but = this.GetTemplateChild("PART_ClearText") as Button; // Enable text clearing button while TextBox is set to ReadOnly
            but.IsEnabled = true;
        }

        private void TextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!this.IsUpdateImmediate)
            {
                return;
            }

            BindingExpression binding = this.GetBindingExpression(TextBox.TextProperty);
            if (null != binding)
            {
                binding.UpdateSource();
            }
        }

        private void TextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            this.hwndSource = (HwndSource)HwndSource.FromVisual(this); // new WindowInteropHelper(window).Handle // If the InPtr is needed.
            this.hwndSource.AddHook(hook);
        }

        private void TextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            this.hwndSource.RemoveHook(hook);
            ClearErrorMessage();
        }

        private void TextBoxKeyDown(object sender, KeyEventArgs e)
        {
            ClearErrorMessage();

            Microsoft.VisualBasic.Devices.Keyboard UserKeyBoard = new Microsoft.VisualBasic.Devices.Keyboard();
            bool AltPressed = UserKeyBoard.AltKeyDown;
            bool ControlPressed = UserKeyBoard.CtrlKeyDown;
            bool ShiftPressed = UserKeyBoard.ShiftKeyDown;

            ModifierKeys LocalModifier = ModifierKeys.None;
            if (AltPressed) { LocalModifier = ModifierKeys.Alt; }
            if (ControlPressed) { LocalModifier |= ModifierKeys.Control; }
            if (ShiftPressed) { LocalModifier |= ModifierKeys.Shift; }

            switch (e.Key)
            {
                case Key.Back:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Back) : "";
                    e.Handled = true;
                    break;

                case Key.Space:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Space) : "";
                    e.Handled = true;
                    break;

                case Key.Delete:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Delete) : "";
                    e.Handled = true;
                    break;

                case Key.Home:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Home) : "";
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.PageUp) : "";
                    e.Handled = true;
                    break;

                case Key.Next:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Next) : "";
                    e.Handled = true;
                    break;

                case Key.End:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.End) : "";
                    break;

                case Key.Up:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Up) : "";
                    break;

                case Key.Down:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Down) : "";
                    break;

                case Key.Right:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Right) : "";
                    break;

                case Key.Left:
                    this.Text = CheckModifier(LocalModifier) ? HotKeyShared.CombineShortcut(LocalModifier, Keys.Left) : "";
                    break;
            }
        }

        private void TextBoxMouseOut(object sender, MouseEventArgs e)
        {
            ClearErrorMessage();
        }

        private bool CheckModifier(ModifierKeys modifier)
        {
            if (modifier == ModifierKeys.None && ForceModifiers)
            {
                ShowErrorMessage();
                return false;
            }

            return true;
        }

        private void ShowErrorMessage()
        {
            var tltp = this.ToolTip as ToolTip;
            tltp.IsOpen = true;
            HasError = true;
        }

        private void ClearErrorMessage()
        {
            var tltp = this.ToolTip as ToolTip;
            if (tltp != null)
                tltp.IsOpen = false;
            HasError = false;
        }
        #endregion

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                Keys KeyPressed = (Keys)wParam;

                Microsoft.VisualBasic.Devices.Keyboard UserKeyBoard = new Microsoft.VisualBasic.Devices.Keyboard();
                bool AltPressed = UserKeyBoard.AltKeyDown;
                bool ControlPressed = UserKeyBoard.CtrlKeyDown;
                bool ShiftPressed = UserKeyBoard.ShiftKeyDown;

                ModifierKeys LocalModifier = ModifierKeys.None;
                if (AltPressed) { LocalModifier = ModifierKeys.Alt; }
                if (ControlPressed) { LocalModifier |= ModifierKeys.Control; }
                if (ShiftPressed) { LocalModifier |= ModifierKeys.Shift; }

                switch ((KeyboardMessages)msg)
                {
                    case KeyboardMessages.WmSyskeydown:
                    case KeyboardMessages.WmKeydown:
                        switch (KeyPressed)
                        {
                            case Keys.Control:
                            case Keys.ControlKey:
                            case Keys.LControlKey:
                            case Keys.RControlKey:
                            case Keys.Shift:
                            case Keys.ShiftKey:
                            case Keys.LShiftKey:
                            case Keys.RShiftKey:
                            case Keys.Alt:
                            case Keys.Menu:
                            case Keys.LMenu:
                            case Keys.RMenu:
                            case Keys.LWin:
                                return IntPtr.Zero;

                            //case Keys.Back:
                            //    this.Text = Keys.None.ToString();
                            //    return IntPtr.Zero;
                        }

                        if (LocalModifier != ModifierKeys.None)
                        {
                            this.Text = HotKeyShared.CombineShortcut(LocalModifier, KeyPressed);
                        }
                        else
                        {
                            if (ForceModifiers)
                            {
                                ShowErrorMessage();
                            }
                            else
                            {
                                this.Text = KeyPressed.ToString();
                            }
                        }
                        return IntPtr.Zero;

                    case KeyboardMessages.WmSyskeyup:
                    case KeyboardMessages.WmKeyup:
                        if (!String.IsNullOrWhiteSpace(Text.Trim()) || this.Text != Keys.None.ToString())
                        {
                            if (HotKeyIsSetEvent != null)
                            {
                                var e = new HotKeyIsSetEventArgs(HotKeyIsSetEvent, UserKey, UserModifier);
                                base.RaiseEvent(e);
                                if (e.Cancel)
                                {
                                    this.Text = "";
                                }
                            }
                        }
                        return IntPtr.Zero;
                }
            }
            catch (OverflowException) { }

            return IntPtr.Zero;
        }
    }
}