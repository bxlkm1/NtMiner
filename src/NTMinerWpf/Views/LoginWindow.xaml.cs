﻿using NTMiner.Views.Ucs;
using NTMiner.Vms;
using System;
using System.Windows;

namespace NTMiner.Views {
    public partial class LoginWindow : Window {
        public static void Login(Action onLoginSuccess) {
            if (!IsLogined()) {
                UIThread.Execute(() => () => {
                    var topWindow = WpfUtil.GetTopWindow();
                    LoginWindow window = new LoginWindow(onLoginSuccess);
                    if (topWindow != null && topWindow.GetType() != typeof(NotiCenterWindow)) {
                        window.Owner = topWindow;
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                    window.ShowSoftDialog();
                    window.PasswordFocus();
                });
            }
            else {
                onLoginSuccess?.Invoke();
            }
        }

        private static bool IsLogined() {
            return !string.IsNullOrEmpty(VirtualRoot.RpcUser.LoginName) && !string.IsNullOrEmpty(VirtualRoot.RpcUser.PasswordSha1);
        }

        private LoginWindowViewModel Vm {
            get {
                return (LoginWindowViewModel)this.DataContext;
            }
        }

        private readonly Action _onLoginSuccess;
        private LoginWindow(Action onLoginSuccess) {
            _onLoginSuccess = onLoginSuccess;
            InitializeComponent();
            this.TbUcName.Text = nameof(LoginWindow);
            // 1个是通知窗口，1个是本窗口
            NotiCenterWindow.Instance.Bind(this, isNoOtherWindow: Application.Current.Windows.Count <= 2);
            PasswordFocus();
        }

        public void PasswordFocus() {
            this.PbPassword.Focus();
        }

        private void MetroWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) {
                this.DragMove();
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            bool isNoOwnerWindow = this.Owner == null || this.Owner.GetType() == typeof(NotiCenterWindow);
            if (isNoOwnerWindow && !IsLogined() && Application.Current.ShutdownMode != ShutdownMode.OnMainWindowClose) {
                Application.Current.Shutdown();
            }
        }

        private void BtnLogin_OnClick(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(Vm.ServerHost)) {
                Vm.ShowMessage("服务器地址不能为空");
                return;
            }
            string passwordSha1 = HashUtil.Sha1(Vm.Password);
            NTMinerRegistry.SetControlCenterHost(Vm.ServerHost);
            var list = NTMinerRegistry.GetControlCenterHosts();
            if (!list.Contains(Vm.ServerHost)) {
                list.Insert(0, Vm.ServerHost);
            }
            NTMinerRegistry.SetControlCenterHosts(list);
            // 内网免登录
            if (Net.IpUtil.IsInnerIp(Vm.ServerHost)) {
                VirtualRoot.SetRpcUser(new User.RpcUser("localhost", "localhost"));
                this.Close();
                // 回调可能弹窗，弹窗可能有父窗口，父窗口是顶层窗口，如果在this.Close()之前回调
                // 则会导致弹窗的父窗口是本窗口，而本窗口随后立即关闭导致作为子窗口的弹窗也会被关闭。
                _onLoginSuccess?.Invoke();
                return;
            }
            RpcRoot.Server.ControlCenterService.LoginAsync(Vm.LoginName, passwordSha1, (response, exception) => {
                UIThread.Execute(() => () => {
                    if (response == null) {
                        Vm.ShowMessage("服务器忙");
                        return;
                    }
                    if (response.IsSuccess()) {
                        this.Close();
                        // 回调可能弹窗，弹窗可能有父窗口，父窗口是顶层窗口，如果在this.Close()之前回调
                        // 则会导致弹窗的父窗口是本窗口，而本窗口随后立即关闭导致作为子窗口的弹窗也会被关闭。
                        _onLoginSuccess?.Invoke();
                    }
                    else if (Vm.LoginName == "admin" && response.StateCode == 404) {
                        Vm.IsPasswordAgainVisible = Visibility.Visible;
                        Vm.ShowMessage(response.ReadMessage(exception));
                        this.PbPasswordAgain.Focus();
                    }
                    else {
                        Vm.IsPasswordAgainVisible = Visibility.Collapsed;
                        Vm.ShowMessage(response.ReadMessage(exception));
                    }
                });
            });
        }

        private void OpenServerHostsPopup() {
            var popup = PopupServerHosts;
            popup.IsOpen = true;
            var selected = Vm.ServerHost;
            var vm = new ServerHostSelectViewModel(selected, onOk: selectedResult => {
                if (selectedResult != null) {
                    if (Vm.ServerHost != selectedResult.IpOrHost) {
                        Vm.ServerHost = selectedResult.IpOrHost;
                    }
                    popup.IsOpen = false;
                }
            }) {
                HideView = new DelegateCommand(() => {
                    popup.IsOpen = false;
                })
            };
            popup.Child = new ServerHostSelect(vm);
        }

        private void ButtonServerHost_Click(object sender, RoutedEventArgs e) {
            OpenServerHostsPopup();
            e.Handled = true;
        }
    }
}
