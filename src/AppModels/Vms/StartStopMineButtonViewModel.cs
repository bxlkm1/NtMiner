﻿using NTMiner.Hub;
using System.Windows.Input;

namespace NTMiner.Vms {
    public class StartStopMineButtonViewModel : ViewModelBase {
        public static readonly StartStopMineButtonViewModel Instance = new StartStopMineButtonViewModel();

        public ICommand StartMine { get; private set; }
        public ICommand StopMine { get; private set; }

        private StartStopMineButtonViewModel() {
            if (WpfUtil.IsInDesignMode) {
                return;
            }
#if DEBUG
            NTStopwatch.Start();
#endif
            this.StartMine = new DelegateCommand(() => {
                VirtualRoot.ThisLocalInfo(nameof(StartStopMineButtonViewModel), $"手动开始挖矿", toConsole: true);
                NTMinerRoot.Instance.StartMine();
            });
            this.StopMine = new DelegateCommand(() => {
                VirtualRoot.ThisLocalInfo(nameof(StartStopMineButtonViewModel), $"手动停止挖矿", toConsole: true);
                if (!NTMinerRoot.Instance.IsMining) {
                    this.MinerProfile.IsMining = false;
                }
                NTMinerRoot.IsAutoStartCanceled = true;
                NTMinerRoot.Instance.StopMineAsync(StopMineReason.LocalUserAction, () => {
                    if (!NTMinerRoot.Instance.IsMining) {
                        this.MinerProfile.IsMining = false;
                    }
                });
            });
#if DEBUG
            var elapsedMilliseconds = NTStopwatch.Stop();
            if (elapsedMilliseconds.ElapsedMilliseconds > NTStopwatch.ElapsedMilliseconds) {
                Write.DevTimeSpan($"耗时{elapsedMilliseconds} {this.GetType().Name}.ctor");
            }
#endif
        }

        public void AutoStart() {
            bool IsAutoStart = (MinerProfile.IsAutoStart || CommandLineArgs.IsAutoStart);
            if (IsAutoStart && !this.MinerProfile.IsMining) {
                this.MinerProfile.IsMining = true;
                IMessagePathId pathId = null;
                pathId = VirtualRoot.AddViaTimesLimitPath<Per1SecondEvent>("挖矿倒计时", LogEnum.None,
                    action: message => {
                        if (NTMinerRoot.IsAutoStartCanceled) {
                            BtnStopText = $"尚未开始";
                        }
                        else {
                            BtnStopText = $"倒计时{pathId.ViaTimesLimit.ToString()}";
                        }
                        if (pathId.ViaTimesLimit == 0) {
                            if (!NTMinerRoot.IsAutoStartCanceled) {
                                VirtualRoot.ThisLocalInfo(nameof(StartStopMineButtonViewModel), $"自动开始挖矿", toConsole: true);
                                NTMinerRoot.Instance.StartMine();
                            }
                        }
                    }, location: this.GetType(), viaTimesLimit: MinerProfile.AutoStartDelaySeconds);
            }
        }

        private string _btnStopText = "正在挖矿";
        public string BtnStopText {
            get => _btnStopText;
            set {
                _btnStopText = value;
                OnPropertyChanged(nameof(BtnStopText));
            }
        }

        public MinerProfileViewModel MinerProfile {
            get {
                return AppContext.Instance.MinerProfileVm;
            }
        }
    }
}
