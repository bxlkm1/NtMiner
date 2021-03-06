﻿using Microsoft.Win32;
using NTMiner.Core;
using NTMiner.Views.Ucs;
using NTMiner.Vms;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NTMiner.Views {
    public partial class MainWindow : Window, IMaskWindow {
        #region SafeNativeMethods
        private static class SafeNativeMethods {
            #region enum struct class
            [StructLayout(LayoutKind.Sequential)]
            public struct POINT {
                public int X;
                public int Y;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public class MONITORINFO {
                public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                public RECT rcMonitor = new RECT();
                public RECT rcWork = new RECT();
                public int dwFlags = 0;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT {
                public int Left, Top, Right, Bottom;
            }
            #endregion

            [DllImport(DllName.User32Dll)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetCursorPos(out POINT lpPoint);
        }
        #endregion

        private bool mRestoreIfMove = false;

        private MainWindowViewModel Vm {
            get {
                return (MainWindowViewModel)this.DataContext;
            }
        }

        private HwndSource hwndSource;
        private readonly GridLength _leftDrawerGripWidth;
        private readonly Brush _btnLeftDrawerGripBrush;
        public MainWindow() {
            if (WpfUtil.IsInDesignMode) {
                return;
            }

            this.MinHeight = 430;
            this.MinWidth = 640;
            this.Width = AppStatic.MainWindowWidth;
            this.Height = AppStatic.MainWindowHeight;
#if DEBUG
            NTStopwatch.Start();
#endif
            ConsoleWindow.Instance.Show();
            ConsoleWindow.Instance.MouseDown += (sender, e) => {
                MoveConsoleWindow();
            };
            ConsoleWindow.Instance.Hide();
            this.Owner = ConsoleWindow.Instance;
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            this.Loaded += (sender, e) => {
                MoveConsoleWindow();
                hwndSource = PresentationSource.FromVisual((Visual)sender) as HwndSource;
                hwndSource.AddHook(new HwndSourceHook(Win32Proc.WindowProc));
            };
            InitializeComponent();
            _leftDrawerGripWidth = LeftDrawerGrip.Width;
            _btnLeftDrawerGripBrush = BtnLeftDrawerGrip.Background;
            NTMinerRoot.RefreshArgsAssembly.Invoke();
            // 下面几行是为了看见设计视图
            this.ResizeCursors.Visibility = Visibility.Visible;
            this.HideLeftDrawerGrid();
            // 上面几行是为了看见设计视图

            DateTime lastGetServerMessageOn = DateTime.MinValue;
            // 切换了主界面上的Tab时
            this.MainArea.SelectionChanged += (sender, e) => {
                // 延迟创建，以加快主界面的启动
                #region
                var selectedItem = MainArea.SelectedItem;
                if (selectedItem == TabItemSpeedTable) {
                    if (SpeedTableContainer.Child == null) {
                        SpeedTableContainer.Child = GetSpeedTable();
                    }
                }
                else if (selectedItem == TabItemMessage) {
                    if (MessagesContainer.Child == null) {
                        MessagesContainer.Child = new Messages();
                    }
                }
                else if (selectedItem == TabItemToolbox) {
                    if (ToolboxContainer.Child == null) {
                        ToolboxContainer.Child = new Toolbox();
                    }
                }
                else if (selectedItem == TabItemMinerProfileOption) {
                    if (MinerProfileOptionContainer.Child == null) {
                        MinerProfileOptionContainer.Child = new MinerProfileOption();
                    }
                }
                VirtualRoot.SetIsServerMessagesVisible(selectedItem == TabItemMessage);
                if (selectedItem == TabItemMessage) {
                    if (lastGetServerMessageOn.AddSeconds(10) < DateTime.Now) {
                        lastGetServerMessageOn = DateTime.Now;
                        VirtualRoot.Execute(new LoadNewServerMessageCommand());
                    }
                }
                #endregion
            };
            this.IsVisibleChanged += (sender, e) => {
                #region
                if (this.IsVisible) {
                    NTMinerRoot.IsUiVisible = true;
                }
                else {
                    NTMinerRoot.IsUiVisible = false;
                }
                MoveConsoleWindow();
                #endregion
            };
            this.ConsoleRectangle.IsVisibleChanged += (sender, e) => {
                MoveConsoleWindow();
            };
            this.StateChanged += (s, e) => {
                #region
                if (Vm.MinerProfile.IsShowInTaskbar) {
                    ShowInTaskbar = true;
                }
                else {
                    if (WindowState == WindowState.Minimized) {
                        ShowInTaskbar = false;
                    }
                    else {
                        ShowInTaskbar = true;
                    }
                }
                if (WindowState == WindowState.Maximized) {
                    ResizeCursors.Visibility = Visibility.Collapsed;
                }
                else {
                    ResizeCursors.Visibility = Visibility.Visible;
                }
                MoveConsoleWindow();
                #endregion
            };
            this.ConsoleRectangle.SizeChanged += (s, e) => {
                MoveConsoleWindow();
            };
            this.SizeChanged += (s, e) => {
                #region
                if (this.Width < 860) {
                    this.CloseLeftDrawer();
                }
                else {
                    this.OpenLeftDrawer();
                }
                if (!this.ConsoleRectangle.IsVisible) {
                    if (e.WidthChanged) {
                        ConsoleWindow.Instance.Width = e.NewSize.Width;
                    }
                    if (e.HeightChanged) {
                        ConsoleWindow.Instance.Height = e.NewSize.Height;
                    }
                }
                #endregion
            };
            NotiCenterWindow.Instance.Bind(this, ownerIsTopMost: true);
            this.LocationChanged += (sender, e) => {
                MoveConsoleWindow();
            };
            VirtualRoot.AddCmdPath<CloseMainWindowCommand>(action: message => {
                UIThread.Execute(() => () => {
                    if (message.IsAutoNoUi) {
                        SwitchToNoUi();
                    }
                    else {
                        this.Close();
                    }
                });
            }, location: this.GetType());
            this.AddEventPath<PoolDelayPickedEvent>("从内核输出中提取了矿池延时时展示到界面", LogEnum.DevConsole,
                action: message => {
                    UIThread.Execute(() => () => {
                        if (message.IsDual) {
                            Vm.StateBarVm.DualPoolDelayText = message.PoolDelayText;
                        }
                        else {
                            Vm.StateBarVm.PoolDelayText = message.PoolDelayText;
                        }
                    });
                }, location: this.GetType());
            this.AddEventPath<MineStartedEvent>("开始挖矿后将清空矿池延时", LogEnum.DevConsole,
                action: message => {
                    UIThread.Execute(() => () => {
                        Vm.StateBarVm.PoolDelayText = string.Empty;
                        Vm.StateBarVm.DualPoolDelayText = string.Empty;
                    });
                }, location: this.GetType());
            this.AddEventPath<MineStopedEvent>("停止挖矿后将清空矿池延时", LogEnum.DevConsole,
                action: message => {
                    UIThread.Execute(() => () => {
                        Vm.StateBarVm.PoolDelayText = string.Empty;
                        Vm.StateBarVm.DualPoolDelayText = string.Empty;
                    });
                }, location: this.GetType());
            this.AddEventPath<Per1MinuteEvent>("挖矿中时自动切换为无界面模式", LogEnum.DevConsole,
                action: message => {
                    if (NTMinerRoot.IsUiVisible && NTMinerRoot.Instance.MinerProfile.IsAutoNoUi && NTMinerRoot.Instance.IsMining) {
                        if (NTMinerRoot.MainWindowRendedOn.AddMinutes(NTMinerRoot.Instance.MinerProfile.AutoNoUiMinutes) < message.BornOn) {
                            VirtualRoot.ThisLocalInfo(nameof(MainWindow), $"挖矿中界面展示{NTMinerRoot.Instance.MinerProfile.AutoNoUiMinutes}分钟后自动切换为无界面模式，可在选项页调整配置");
                            VirtualRoot.Execute(new CloseMainWindowCommand(isAutoNoUi: true));
                        }
                    }
                }, location: this.GetType());
            this.AddEventPath<CpuPackageStateChangedEvent>("CPU包状态变更后刷新Vm内存", LogEnum.None,
                action: message => {
                    UpdateCpuView();
                }, location: this.GetType());
#if DEBUG
            var elapsedMilliseconds = NTStopwatch.Stop();
            if (elapsedMilliseconds.ElapsedMilliseconds > NTStopwatch.ElapsedMilliseconds) {
                Write.DevTimeSpan($"耗时{elapsedMilliseconds} {this.GetType().Name}.ctor");
            }
#endif
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e) {
            MoveConsoleWindow();
        }

        #region 改变下面的控制台窗口的尺寸和位置
        private void MoveConsoleWindow() {
            if (!this.IsLoaded) {
                return;
            }
            ConsoleWindow consoleWindow = ConsoleWindow.Instance;
            if (!this.IsVisible || this.WindowState == WindowState.Minimized) {
                consoleWindow.Hide();
                return;
            }
            if (!consoleWindow.IsVisible) {
                consoleWindow.Show();
            }
            if (consoleWindow.WindowState != this.WindowState) {
                consoleWindow.WindowState = this.WindowState;
            }
            // -2 -1是因为主窗口有圆角，但下层的控制台窗口不能透明所以不能圆角，把下层的控制台窗口的宽高缩小一点点从而避免看见下层控制台窗口的棱角
            if (consoleWindow.Width != this.Width - 2) {
                consoleWindow.Width = this.Width - 2;
            }
            if (consoleWindow.Height != this.Height - 2) {
                consoleWindow.Height = this.Height - 2;
            }
            if (this.WindowState == WindowState.Normal) {
                if (consoleWindow.Left != this.Left + 1) {
                    consoleWindow.Left = this.Left + 1;
                }
                if (consoleWindow.Top != this.Top + 1) {
                    consoleWindow.Top = this.Top + 1;
                }
            }
            if (ConsoleRectangle != null && ConsoleRectangle.IsVisible) {
                Point point = ConsoleRectangle.TransformToAncestor(this).Transform(new Point(0, 0));
                consoleWindow.MoveWindow(marginLeft: (int)point.X, marginTop: (int)point.Y, height: (int)ConsoleRectangle.ActualHeight);
            }
        }
        #endregion

        #region 更新状态栏展示的CPU使用率和温度
        private int _cpuPerformance = 0;
        private int _cpuTemperature = 0;
        private int _cpuPower = 0;
        private void UpdateCpuView() {
            int performance = NTMinerRoot.Instance.CpuPackage.Performance;
            int temperature = NTMinerRoot.Instance.CpuPackage.Temperature;
            int cpuPower = NTMinerRoot.Instance.CpuPackage.Power;
            if (temperature < 0) {
                temperature = 0;
            }
            if (_cpuPerformance != performance) {
                _cpuPerformance = performance;
                Vm.StateBarVm.CpuPerformanceText = performance.ToString() + "%";
            }
            if (_cpuPower != cpuPower) {
                _cpuPower = cpuPower;
                Vm.StateBarVm.CpuPowerText = cpuPower.ToString() + "W";
            }
            if (_cpuTemperature != temperature) {
                _cpuTemperature = temperature;
                Vm.StateBarVm.CpuTemperatureText = temperature.ToString() + " ℃";
            }
        }
        #endregion

        #region 显示或隐藏半透明遮罩层
        // 因为挖矿端主界面是透明的，遮罩方法和普通窗口不同，如果按照通用的方法遮罩的话会导致能透过窗口看见windows桌面或者下面的窗口。
        public void ShowMask() {
            MaskLayer.Visibility = Visibility.Visible;
        }

        public void HideMask() {
            MaskLayer.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region 主界面左侧的抽屉
        // 点击pin按钮
        public void BtnLeftDrawerPin_Click(object sender, RoutedEventArgs e) {
            if (LeftDrawerGrip.Width != _leftDrawerGripWidth) {
                CloseLeftDrawer();
            }
            else {
                OpenLeftDrawer();
            }
        }

        // 点击x按钮
        private void BtnLeftDrawerClose_Click(object sender, RoutedEventArgs e) {
            CloseLeftDrawer();
        }

        private void BtnLeftDrawerGrip_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 2) {
                if (leftDrawer.Visibility == Visibility.Collapsed) {
                    leftDrawer.Visibility = Visibility.Visible;
                }
                else {
                    leftDrawer.Visibility = Visibility.Collapsed;
                }
            }
            else {
                OpenLeftDrawer();
            }
        }

        private void BtnLeftDrawerGrip_MouseEnter(object sender, MouseEventArgs e) {
            ((Control)sender).Background = WpfUtil.GreenBrush;
        }

        private void BtnLeftDrawerGrip_MouseLeave(object sender, MouseEventArgs e) {
            ((Control)sender).Background = _btnLeftDrawerGripBrush;
        }

        // 打开左侧抽屉
        private void CloseLeftDrawer() {
            if (leftDrawer.Visibility == Visibility.Collapsed) {
                return;
            }
            leftDrawer.Visibility = Visibility.Collapsed;
            this.ShowLeftDrawerGrid();
            PinRotateTransform.Angle = 90;

            mainLayer.ColumnDefinitions.Remove(MinerProfileColumn);
            MainArea.SetValue(Grid.ColumnProperty, mainLayer.ColumnDefinitions.Count - 1);
        }

        private void ShowLeftDrawerGrid() {
            LeftDrawerGrip.Width = _leftDrawerGripWidth;
        }

        // 关闭左侧抽屉
        private void OpenLeftDrawer() {
            if (LeftDrawerGrip.Width != _leftDrawerGripWidth) {
                return;
            }
            leftDrawer.Visibility = Visibility.Visible;
            this.HideLeftDrawerGrid();
            PinRotateTransform.Angle = 0;

            if (!mainLayer.ColumnDefinitions.Contains(MinerProfileColumn)) {
                mainLayer.ColumnDefinitions.Insert(0, MinerProfileColumn);
            }
            MainArea.SetValue(Grid.ColumnProperty, mainLayer.ColumnDefinitions.Count - 1);
        }

        private void HideLeftDrawerGrid() {
            LeftDrawerGrip.Width = new GridLength(0);
        }
        #endregion

        protected override void OnClosing(CancelEventArgs e) {
            if (NTMinerRoot.Instance.MinerProfile.IsCloseMeanExit) {
                AppStatic.AppExit.Execute(null);
            }
            else {
                e.Cancel = true;
                SwitchToNoUi();
            }
        }

        private void SwitchToNoUi() {
            AppContext.Disable();
            this.Hide();
            VirtualRoot.Out.ShowSuccess("已切换为无界面模式运行");
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                this.DragMove();
            }
        }

        private void ScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            WpfUtil.ScrollViewer_PreviewMouseDown(sender, e);
        }

        private void BtnOverClockVisible_Click(object sender, RoutedEventArgs e) {
            var speedTableUc = this.GetSpeedTable();
            if (MainArea.SelectedItem == TabItemSpeedTable) {
                speedTableUc.ShowOrHideOverClock(isShow: speedTableUc.IsOverClockVisible == Visibility.Collapsed);
            }
            else {
                speedTableUc.ShowOrHideOverClock(isShow: true);
            }
            MainArea.SelectedItem = TabItemSpeedTable;
            IconOverClockEyeClosed.Visibility = speedTableUc.IsOverClockVisible == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private SpeedTable _speedTable;
        private SpeedTable GetSpeedTable() {
            if (_speedTable == null) {
                _speedTable = new SpeedTable();
            }
            return _speedTable;
        }

        private void SwitchWindowState() {
            switch (WindowState) {
                case WindowState.Normal:
                    Microsoft.Windows.Shell.SystemCommands.MaximizeWindow(this);
                    break;
                case WindowState.Maximized:
                    Microsoft.Windows.Shell.SystemCommands.RestoreWindow(this);
                    break;
            }
        }

        #region 拖动窗口头部
        private void RctHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                SwitchWindowState();
                return;
            }

            else if (WindowState == WindowState.Maximized) {
                mRestoreIfMove = true;
                return;
            }

            DragMove();
        }

        private void RctHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            mRestoreIfMove = false;
        }

        private void RctHeader_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (mRestoreIfMove && e.LeftButton == MouseButtonState.Pressed) {
                mRestoreIfMove = false;

                double percentHorizontal = e.GetPosition(this).X / ActualWidth;
                double targetHorizontal = RestoreBounds.Width * percentHorizontal;

                double percentVertical = e.GetPosition(this).Y / ActualHeight;
                double targetVertical = RestoreBounds.Height * percentVertical;

                WindowState = WindowState.Normal;

                SafeNativeMethods.GetCursorPos(out SafeNativeMethods.POINT lMousePosition);

                Left = lMousePosition.X - targetHorizontal;
                Top = lMousePosition.Y - targetVertical;

                DragMove();
            }
        }
        #endregion
    }
}
