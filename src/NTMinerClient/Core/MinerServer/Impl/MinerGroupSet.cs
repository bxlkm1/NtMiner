﻿using NTMiner.Core.MinerServer;
using System;
using System.Collections.Generic;

namespace NTMiner.Core.MinerServer.Impl {
    public class MinerGroupSet : IMinerGroupSet {
        private readonly Dictionary<Guid, MinerGroupData> _dicById = new Dictionary<Guid, MinerGroupData>();

        public MinerGroupSet() {
            VirtualRoot.AddCmdPath<AddMinerGroupCommand>(action: (message) => {
                InitOnece();
                if (message == null || message.Input == null || message.Input.GetId() == Guid.Empty) {
                    throw new ArgumentNullException();
                }
                if (string.IsNullOrEmpty(message.Input.Name)) {
                    throw new ValidationException("minerGroup name can't be null or empty");
                }
                if (_dicById.ContainsKey(message.Input.GetId())) {
                    return;
                }
                MinerGroupData entity = new MinerGroupData().Update(message.Input);
                RpcRoot.Server.MinerGroupService.AddOrUpdateMinerGroupAsync(entity, (response, exception) => {
                    if (response.IsSuccess()) {
                        _dicById.Add(entity.Id, entity);
                        VirtualRoot.RaiseEvent(new MinerGroupAddedEvent(message.MessageId, entity));
                    }
                    else {
                        VirtualRoot.Out.ShowError(response.ReadMessage(exception), autoHideSeconds: 4);
                    }
                });
            }, location: this.GetType());
            VirtualRoot.AddCmdPath<UpdateMinerGroupCommand>(action: (message) => {
                InitOnece();
                if (message == null || message.Input == null || message.Input.GetId() == Guid.Empty) {
                    throw new ArgumentNullException();
                }
                if (string.IsNullOrEmpty(message.Input.Name)) {
                    throw new ValidationException("minerGroup name can't be null or empty");
                }
                if (!_dicById.ContainsKey(message.Input.GetId())) {
                    return;
                }
                MinerGroupData entity = _dicById[message.Input.GetId()];
                MinerGroupData oldValue = new MinerGroupData().Update(entity);
                entity.Update(message.Input);
                RpcRoot.Server.MinerGroupService.AddOrUpdateMinerGroupAsync(entity, (response, exception) => {
                    if (!response.IsSuccess()) {
                        entity.Update(oldValue);
                        VirtualRoot.RaiseEvent(new MinerGroupUpdatedEvent(message.MessageId, entity));
                        VirtualRoot.Out.ShowError(response.ReadMessage(exception), autoHideSeconds: 4);
                    }
                });
                VirtualRoot.RaiseEvent(new MinerGroupUpdatedEvent(message.MessageId, entity));
            }, location: this.GetType());
            VirtualRoot.AddCmdPath<RemoveMinerGroupCommand>(action: (message) => {
                InitOnece();
                if (message == null || message.EntityId == Guid.Empty) {
                    throw new ArgumentNullException();
                }
                if (!_dicById.ContainsKey(message.EntityId)) {
                    return;
                }
                MinerGroupData entity = _dicById[message.EntityId];
                RpcRoot.Server.MinerGroupService.RemoveMinerGroupAsync(entity.Id, (response, exception) => {
                    if (response.IsSuccess()) {
                        _dicById.Remove(entity.Id);
                        VirtualRoot.RaiseEvent(new MinerGroupRemovedEvent(message.MessageId, entity));
                    }
                    else {
                        VirtualRoot.Out.ShowError(response.ReadMessage(exception), autoHideSeconds: 4);
                    }
                });
            }, location: this.GetType());
        }

        private bool _isInited = false;
        private readonly object _locker = new object();

        private void InitOnece() {
            if (_isInited) {
                return;
            }
            Init();
        }

        private void Init() {
            if (!_isInited) {
                lock (_locker) {
                    if (!_isInited) {
                        var result = RpcRoot.Server.MinerGroupService.GetMinerGroups();
                        foreach (var item in result) {
                            if (!_dicById.ContainsKey(item.GetId())) {
                                _dicById.Add(item.GetId(), item);
                            }
                        }
                        _isInited = true;
                    }
                }
            }
        }

        public bool TryGetMinerGroup(Guid id, out IMinerGroup group) {
            InitOnece();
            var r = _dicById.TryGetValue(id, out MinerGroupData g);
            group = g;
            return r;
        }

        public IEnumerable<IMinerGroup> AsEnumerable() {
            InitOnece();
            return _dicById.Values;
        }
    }
}
