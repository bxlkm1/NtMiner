﻿using NTMiner.User;
using NTMiner.Vms;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace NTMiner {
    public partial class AppContext {
        public class UserViewModels : ViewModelBase {
            public static readonly UserViewModels Instance = new UserViewModels();
            private readonly Dictionary<string, UserViewModel> _dicByLoginName = new Dictionary<string, UserViewModel>();

            public ICommand Add { get; private set; }

            private UserViewModels() {
#if DEBUG
                NTStopwatch.Start();
#endif
                if (WpfUtil.IsInDesignMode) {
                    return;
                }
                this.Add = new DelegateCommand(() => {
                    if (!VirtualRoot.IsMinerStudio) {
                        return;
                    }
                    new UserViewModel().Edit.Execute(FormType.Add);
                });
                AddEventPath<UserAddedEvent>("添加了用户后", LogEnum.DevConsole,
                    action: message => {
                        if (!_dicByLoginName.ContainsKey(message.Target.LoginName)) {
                            _dicByLoginName.Add(message.Target.LoginName, new UserViewModel(message.Target));
                            OnPropertyChanged(nameof(List));
                        }
                    }, location: this.GetType());
                AddEventPath<UserUpdatedEvent>("更新了用户后", LogEnum.DevConsole,
                    action: message => {
                        if (_dicByLoginName.TryGetValue(message.Target.LoginName, out UserViewModel vm)) {
                            vm.Update(message.Target);
                        }
                    }, location: this.GetType());
                AddEventPath<UserRemovedEvent>("移除了用户后", LogEnum.DevConsole,
                    action: message => {
                        _dicByLoginName.Remove(message.Target.LoginName);
                        OnPropertyChanged(nameof(List));
                    }, location: this.GetType());
                foreach (var item in NTMinerRoot.Instance.UserSet.AsEnumerable()) {
                    _dicByLoginName.Add(item.LoginName, new UserViewModel(item));
                }
#if DEBUG
                var elapsedMilliseconds = NTStopwatch.Stop();
                if (elapsedMilliseconds.ElapsedMilliseconds > NTStopwatch.ElapsedMilliseconds) {
                    Write.DevTimeSpan($"耗时{elapsedMilliseconds} {this.GetType().Name}.ctor");
                }
#endif
            }

            public List<UserViewModel> List {
                get {
                    return _dicByLoginName.Values.ToList();
                }
            }
        }
    }
}
