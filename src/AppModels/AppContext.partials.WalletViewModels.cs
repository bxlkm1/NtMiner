﻿using NTMiner.Core;
using NTMiner.Vms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTMiner {
    public partial class AppContext {
        public class WalletViewModels : ViewModelBase {
            public static readonly WalletViewModels Instance = new WalletViewModels();
            private readonly Dictionary<Guid, WalletViewModel> _dicById = new Dictionary<Guid, WalletViewModel>();
            private WalletViewModels() {
#if DEBUG
                NTStopwatch.Start();
#endif
                VirtualRoot.AddEventPath<LocalContextReInitedEvent>("LocalContext刷新后刷新钱包Vm内存", LogEnum.None,
                    action: message=> {
                        _dicById.Clear();
                        Init();
                    }, location: this.GetType());
                AddEventPath<WalletAddedEvent>("添加了钱包后调整VM内存", LogEnum.DevConsole,
                    action: (message) => {
                        _dicById.Add(message.Target.GetId(), new WalletViewModel(message.Target));
                        OnPropertyChanged(nameof(WalletList));
                        if (AppContext.Instance.CoinVms.TryGetCoinVm(message.Target.CoinId, out CoinViewModel coin)) {
                            coin.OnPropertyChanged(nameof(CoinViewModel.Wallets));
                            coin.OnPropertyChanged(nameof(CoinViewModel.WalletItems));
                            coin.CoinKernel?.CoinKernelProfile?.SelectedDualCoin?.OnPropertyChanged(nameof(CoinViewModel.Wallets));
                        }
                    }, location: this.GetType());
                AddEventPath<WalletRemovedEvent>("删除了钱包后调整VM内存", LogEnum.DevConsole,
                    action: (message) => {
                        _dicById.Remove(message.Target.GetId());
                        OnPropertyChanged(nameof(WalletList));
                        if (AppContext.Instance.CoinVms.TryGetCoinVm(message.Target.CoinId, out CoinViewModel coin)) {
                            coin.OnPropertyChanged(nameof(CoinViewModel.Wallets));
                            coin.OnPropertyChanged(nameof(CoinViewModel.WalletItems));
                            coin.CoinProfile?.OnPropertyChanged(nameof(CoinProfileViewModel.SelectedWallet));
                            coin.CoinKernel?.CoinKernelProfile?.SelectedDualCoin?.OnPropertyChanged(nameof(CoinViewModel.Wallets));
                        }
                    }, location: this.GetType());
                AddEventPath<WalletUpdatedEvent>("更新了钱包后调整VM内存", LogEnum.DevConsole,
                    action: (message) => {
                        _dicById[message.Target.GetId()].Update(message.Target);
                    }, location: this.GetType());
                Init();
#if DEBUG
                var elapsedMilliseconds = NTStopwatch.Stop();
                if (elapsedMilliseconds.ElapsedMilliseconds > NTStopwatch.ElapsedMilliseconds) {
                    Write.DevTimeSpan($"耗时{elapsedMilliseconds} {this.GetType().Name}.ctor");
                }
#endif
            }

            private void Init() {
                foreach (var item in NTMinerRoot.Instance.MinerProfile.GetWallets()) {
                    _dicById.Add(item.GetId(), new WalletViewModel(item));
                }
            }

            public List<WalletViewModel> WalletList {
                get {
                    return _dicById.Values.ToList();
                }
            }

            public WalletViewModel GetUpOne(Guid coinId, int sortNumber) {
                return WalletList.Where(a => a.CoinId == coinId).GetUpOne(sortNumber);
            }

            public WalletViewModel GetNextOne(Guid coinId, int sortNumber) {
                return WalletList.Where(a => a.CoinId == coinId).GetNextOne(sortNumber);
            }
        }
    }
}
