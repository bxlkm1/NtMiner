﻿using NTMiner.Core;
using NTMiner.Vms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace NTMiner {
    public partial class AppContext {
        public class MinerGroupViewModels : ViewModelBase {
            public static readonly MinerGroupViewModels Instance = new MinerGroupViewModels();
            private readonly Dictionary<Guid, MinerGroupViewModel> _dicById = new Dictionary<Guid, MinerGroupViewModel>();

            public ICommand Add { get; private set; }

            private MinerGroupViewModels() {
#if DEBUG
                NTStopwatch.Start();
#endif
                if (WpfUtil.IsInDesignMode) {
                    return;
                }
                foreach (var item in NTMinerRoot.Instance.MinerGroupSet.AsEnumerable()) {
                    _dicById.Add(item.GetId(), new MinerGroupViewModel(item));
                }
                this.Add = new DelegateCommand(() => {
                    new MinerGroupViewModel(Guid.NewGuid()).Edit.Execute(FormType.Add);
                });
                AddEventPath<MinerGroupAddedEvent>("添加矿机分组后刷新VM内存", LogEnum.DevConsole,
                    action: message => {
                        if (!_dicById.ContainsKey(message.Target.GetId())) {
                            _dicById.Add(message.Target.GetId(), new MinerGroupViewModel(message.Target));
                            OnPropertyChanged(nameof(List));
                            OnPropertyChanged(nameof(MinerGroupItems));
                            AppContext.Instance.MinerClientsWindowVm.OnPropertyChanged(nameof(MinerClientsWindowViewModel.SelectedMinerGroup));
                        }
                    }, location: this.GetType());
                AddEventPath<MinerGroupUpdatedEvent>("更新矿机分组后刷新VM内存", LogEnum.DevConsole,
                    action: message => {
                        _dicById[message.Target.GetId()].Update(message.Target);
                    }, location: this.GetType());
                AddEventPath<MinerGroupRemovedEvent>("删除矿机分组后刷新VM内存", LogEnum.DevConsole,
                    action: message => {
                        _dicById.Remove(message.Target.GetId());
                        OnPropertyChanged(nameof(List));
                        OnPropertyChanged(nameof(MinerGroupItems));
                        AppContext.Instance.MinerClientsWindowVm.OnPropertyChanged(nameof(MinerClientsWindowViewModel.SelectedMinerGroup));
                    }, location: this.GetType());
#if DEBUG
                var elapsedMilliseconds = NTStopwatch.Stop();
                if (elapsedMilliseconds.ElapsedMilliseconds > NTStopwatch.ElapsedMilliseconds) {
                    Write.DevTimeSpan($"耗时{elapsedMilliseconds} {this.GetType().Name}.ctor");
                }
#endif
            }

            public List<MinerGroupViewModel> List {
                get {
                    return _dicById.Values.ToList();
                }
            }

            public bool TryGetMineWorkVm(Guid id, out MinerGroupViewModel minerGroupVm) {
                return _dicById.TryGetValue(id, out minerGroupVm);
            }

            private IEnumerable<MinerGroupViewModel> GetMinerGroupItems() {
                yield return MinerGroupViewModel.PleaseSelect;
                foreach (var item in List) {
                    yield return item;
                }
            }
            public List<MinerGroupViewModel> MinerGroupItems {
                get {
                    return GetMinerGroupItems().ToList();
                }
            }
        }
    }
}
