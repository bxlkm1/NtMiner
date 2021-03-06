﻿using NTMiner.Core.Daemon;
using NTMiner.Core.MinerClient;

namespace NTMiner.Controllers {
    public interface IMinerClientController {
        bool ShowMainWindow();
        ResponseBase CloseNTMiner(SignRequest request);
        ResponseBase StartMine(WorkRequest request);
        ResponseBase StopMine(SignRequest request);
        ResponseBase SetMinerProfileProperty(SetMinerProfilePropertyRequest request);
        SpeedData GetSpeed();
        void RefreshAutoBootStart();
        void OverClock();
    }
}
