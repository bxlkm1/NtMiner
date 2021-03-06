﻿using NTMiner.Core.MinerClient;

namespace NTMiner.Core.Gpus {
    public interface IGpu : IGpuStaticData, IOverClockInput {
        int Temperature { get; }
        uint FanSpeed { get; set; }
        uint PowerUsage { get; }
    }
}
