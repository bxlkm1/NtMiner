﻿using LiteDB;
using System;
using System.Text;

namespace NTMiner.Core.Profile {
    public class CoinKernelProfileData : ICoinKernelProfile, IDbEntity<Guid>, IGetSignData {
        public CoinKernelProfileData() { }

        public static CoinKernelProfileData CreateDefaultData(Guid coinKernelId, double dualCoinWeight) {
            return new CoinKernelProfileData() {
                CoinKernelId = coinKernelId,
                IsDualCoinEnabled = false,
                IsAutoDualWeight = true,
                DualCoinId = Guid.Empty,
                DualCoinWeight = dualCoinWeight,
                CustomArgs = string.Empty,
                TouchedArgs = string.Empty
            };
        }

        public Guid GetId() {
            return this.CoinKernelId;
        }

        [BsonId]
        public Guid CoinKernelId { get; set; }

        public bool IsDualCoinEnabled { get; set; }

        public Guid DualCoinId { get; set; }

        public double DualCoinWeight { get; set; }

        public bool IsAutoDualWeight { get; set; }

        public string CustomArgs { get; set; }

        public string TouchedArgs { get; set; }

        public override string ToString() {
            return this.BuildSign().ToString();
        }

        public StringBuilder GetSignData() {
            return this.BuildSign();
        }
    }
}
