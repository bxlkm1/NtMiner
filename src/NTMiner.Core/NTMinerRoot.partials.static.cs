﻿using Microsoft.Win32;
using NTMiner.JsonDb;
using NTMiner.MinerServer;
using NTMiner.Repositories;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NTMiner {
    public partial class NTMinerRoot {
        public const int SpeedHistoryLengthByMinute = 10;
        public const int GpuAllId = -1;
        public static readonly bool IsAutoStart = (NTMinerRegistry.GetIsAutoStart() || CommandLineArgs.IsAutoStart);
        private static readonly NTMinerRoot S_Instance = new NTMinerRoot();
        public static readonly INTMinerRoot Instance = S_Instance;
        public static readonly Version CurrentVersion;
        public static readonly string CurrentVersionTag;

        public static string ServerVersion;
        private static bool _isJsonServer;
        public static bool IsJsonServer {
            get { return _isJsonServer; }
            private set { _isJsonServer = value; }
        }

        private static bool _isJsonLocal;
        public static bool IsJsonLocal {
            get { return _isJsonLocal; }
            private set { _isJsonLocal = value; }
        }

        public static Action RefreshArgsAssembly { get; private set; } = () => { };
        public static void SetRefreshArgsAssembly(Action action) {
            RefreshArgsAssembly = action;
        }
        public static bool IsUiVisible;
        public static DateTime MainWindowRendedOn = DateTime.MinValue;

        public static bool IsUseDevConsole = false;
        public static int OSVirtualMemoryMb;
        public static string UserKernelCommandLine;

        public static bool IsAutoStartCanceled = false;

        public static bool IsKernelBrand {
            get {
                return KernelBrandId != Guid.Empty;
            }
        }

        private static Guid? kernelBrandId = null;
        public static Guid KernelBrandId {
            get {
                if (!kernelBrandId.HasValue) {
                    kernelBrandId = VirtualRoot.GetBrandId(VirtualRoot.AppFileFullName, Consts.KernelBrandId);
                }
                return kernelBrandId.Value;
            }
        }

        public static bool IsPoolBrand {
            get {
                return PoolBrandId != Guid.Empty;
            }
        }

        private static Guid? poolBrandId = null;
        public static Guid PoolBrandId {
            get {
                if (!poolBrandId.HasValue) {
                    poolBrandId = VirtualRoot.GetBrandId(VirtualRoot.AppFileFullName, Consts.PoolBrandId);
                }
                return poolBrandId.Value;
            }
        }

        public static bool IsBrandSpecified {
            get {
                return KernelBrandId != Guid.Empty || PoolBrandId != Guid.Empty;
            }
        }

        static NTMinerRoot() {
            Assembly mainAssembly = Assembly.GetEntryAssembly();
            CurrentVersion = mainAssembly.GetName().Version;
            ServerVersion = CurrentVersion.ToString();
            CurrentVersionTag = ((AssemblyDescriptionAttribute)mainAssembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), inherit: false).First()).Description;
        }

        private static LocalJsonDb _localJson;
        public static ILocalJsonDb LocalJson {
            get {
                LocalJsonInit();
                return _localJson;
            }
        }

        public static void ReInitLocalJson() {
            _localJsonInited = false;
            LocalJsonInit();
        }

        private static readonly object _localJsonlocker = new object();
        private static bool _localJsonInited = false;
        // 从磁盘读取local.json反序列化为LocalJson对象
        private static void LocalJsonInit() {
            if (!_localJsonInited) {
                lock (_localJsonlocker) {
                    if (!_localJsonInited) {
                        string localJson = SpecialPath.ReadLocalJsonFile();
                        if (!string.IsNullOrEmpty(localJson)) {
                            try {
                                LocalJsonDb data = VirtualRoot.JsonSerializer.Deserialize<LocalJsonDb>(localJson);
                                _localJson = data ?? new LocalJsonDb();
                            }
                            catch (Exception e) {
                                Logger.ErrorDebugLine(e);
                            }
                        }
                        else {
                            _localJson = new LocalJsonDb();
                        }
                        // 这里的逻辑是，当用户在主界面填写矿工名时，矿工名会被交换到注册表从而当用户使用群控但没有填写群控矿工名时作为缺省矿工名
                        // 但是旧版本的挖矿端并没有把矿工名交换到注册表去所以当注册表中没有矿工名时需读取local.litedb中的矿工名
                        if (string.IsNullOrEmpty(_localJson.MinerProfile.MinerName)) {
                            _localJson.MinerProfile.MinerName = GetMinerName();
                            if (string.IsNullOrEmpty(_localJson.MinerProfile.MinerName)) {
                                var repository = new LiteDbReadWriteRepository<Profile.MinerProfileData>(SpecialPath.LocalDbFileFullName);
                                Profile.MinerProfileData data = repository.GetByKey(Profile.MinerProfileData.DefaultId);
                                if (data != null) {
                                    _localJson.MinerProfile.MinerName = data.MinerName;
                                }
                            }
                        }
                        _localJsonInited = true;
                    }
                }
            }
        }

        private static ServerJsonDb _serverJson;
        public static IServerJsonDb ServerJson {
            get {
                ServerJsonInit();
                return _serverJson;
            }
        }

        public static void ReInitServerJson() {
            _serverJsonInited = false;
        }

        #region ServerJsonInit
        private static readonly object _serverJsonlocker = new object();
        private static bool _serverJsonInited = false;
        // 从磁盘读取server.json反序列化为ServerJson对象
        private static void ServerJsonInit() {
            if (!_serverJsonInited) {
                lock (_serverJsonlocker) {
                    if (!_serverJsonInited) {
                        string serverJson = SpecialPath.ReadServerJsonFile();
                        if (!string.IsNullOrEmpty(serverJson)) {
                            try {
                                ServerJsonDb data = VirtualRoot.JsonSerializer.Deserialize<ServerJsonDb>(serverJson);
                                _serverJson = data;
                                if (KernelBrandId != Guid.Empty) {
                                    var kernelToRemoves = data.Kernels.Where(a => a.BrandId != KernelBrandId).ToArray();
                                    foreach (var item in kernelToRemoves) {
                                        data.Kernels.Remove(item);
                                    }
                                    var coinKernelToRemoves = data.CoinKernels.Where(a => kernelToRemoves.Any(b => b.Id == a.KernelId)).ToArray();
                                    foreach (var item in coinKernelToRemoves) {
                                        data.CoinKernels.Remove(item);
                                    }
                                    var poolKernelToRemoves = data.PoolKernels.Where(a => kernelToRemoves.Any(b => b.Id == a.KernelId)).ToArray();
                                    foreach (var item in poolKernelToRemoves) {
                                        data.PoolKernels.Remove(item);
                                    }
                                }
                                if (PoolBrandId != Guid.Empty) {
                                    var poolToRemoves = data.Pools.Where(a => a.BrandId != PoolBrandId && data.Pools.Any(b => b.CoinId == a.CoinId && b.BrandId == poolBrandId)).ToArray();
                                    foreach (var item in poolToRemoves) {
                                        data.Pools.Remove(item);
                                    }
                                    var poolKernelToRemoves = data.PoolKernels.Where(a => poolToRemoves.Any(b => b.Id == a.PoolId)).ToArray();
                                    foreach (var item in poolKernelToRemoves) {
                                        data.PoolKernels.Remove(item);
                                    }
                                }
                            }
                            catch (Exception e) {
                                Logger.ErrorDebugLine(e);
                            }
                        }
                        else {
                            _serverJson = new ServerJsonDb();
                        }
                        _serverJsonInited = true;
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// 将当前的系统状态导出到给定的json文件
        /// </summary>
        /// <returns></returns>
        public static void ExportServerVersionJson(string jsonFileFullName) {
            ServerJsonDb serverJsonObj = new ServerJsonDb(Instance);
            serverJsonObj.CutJsonSize();
            string json = VirtualRoot.JsonSerializer.Serialize(serverJsonObj);
            serverJsonObj.UnCut();
            File.WriteAllText(jsonFileFullName, json);
        }

        public static void ExportWorkJson(MineWorkData mineWorkData, out string localJson, out string serverJson) {
            localJson = string.Empty;
            serverJson = string.Empty;
            try {
                LocalJsonDb localJsonObj = new LocalJsonDb(Instance, mineWorkData);
                ServerJsonDb serverJsonObj = new ServerJsonDb(Instance, localJsonObj);
                localJson = VirtualRoot.JsonSerializer.Serialize(localJsonObj);
                serverJson = VirtualRoot.JsonSerializer.Serialize(serverJsonObj);
                mineWorkData.ServerJsonSha1 = HashUtil.Sha1(serverJson);
            }
            catch (Exception e) {
                Logger.ErrorDebugLine(e);
            }
        }

        /// <summary>
        /// 创建组合仓储，组合仓储由ServerDb和ProfileDb层序组成。
        /// 如果是开发者则访问ServerDb且只访问GlobalDb，否则将ServerDb和ProfileDb并起来访问且不能修改删除GlobalDb。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IRepository<T> CreateCompositeRepository<T>() where T : class, ILevelEntity<Guid> {
            return new HierarchicalRepository<T>(CreateServerRepository<T>(), CreateLocalRepository<T>());
        }

        public static IRepository<T> CreateLocalRepository<T>() where T : class, IDbEntity<Guid> {
            if (!IsJsonLocal) {
                return new LiteDbReadWriteRepository<T>(SpecialPath.LocalDbFileFullName);
            }
            else {
                return new JsonReadOnlyRepository<T>(LocalJson);
            }
        }

        public static IRepository<T> CreateServerRepository<T>() where T : class, IDbEntity<Guid> {
            if (!IsJsonServer) {
                return new LiteDbReadWriteRepository<T>(SpecialPath.ServerDbFileFullName);
            }
            else {
                return new JsonReadOnlyRepository<T>(ServerJson);
            }
        }

        // 矿工名中不可以包含的字符
        private static readonly char[] InvalidChars = { '.', ' ', '-', '_' };
        public static string GetThisPcName() {
            string value = Environment.MachineName.ToLower();
            value = new string(value.ToCharArray().Where(a => !InvalidChars.Contains(a)).ToArray());
            return value;
        }

        #region DiskSpace
        private static DateTime _diskSpaceOn = DateTime.MinValue;
        private static string _diskSpace = string.Empty;
        public static string DiskSpace {
            get {
                if (_diskSpaceOn.AddMinutes(20) < DateTime.Now) {
                    _diskSpaceOn = DateTime.Now;
                    StringBuilder sb = new StringBuilder();
                    int len = sb.Length;
                    foreach (var item in DriveInfo.GetDrives().Where(a => a.DriveType == DriveType.Fixed)) {
                        if (len != sb.Length) {
                            sb.Append(";");
                        }
                        // item.Name like C:\
                        sb.Append(item.Name).Append((item.AvailableFreeSpace / (double)(1024 * 1024 * 1024)).ToString("f1")).Append(" Gb");
                    }
                    _diskSpace = sb.ToString();
                }

                return _diskSpace;
            }
        }
        #endregion

        #region MinerName 非群控模式时将矿工名交换到注册表从而作为群控模式时未指定矿工名的缺省矿工名
        public static string GetMinerName() {
            object value = Windows.WinRegistry.GetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "MinerName");
            return (value ?? string.Empty).ToString();
        }

        public static void SetMinerName(string value) {
            Windows.WinRegistry.SetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "MinerName", value);
        }
        #endregion

        #region IsShowNotifyIcon
        public static bool GetIsShowNotifyIcon() {
            object value = Windows.WinRegistry.GetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "IsShowNotifyIcon");
            return value == null || value.ToString() == "True";
        }

        public static void SetIsShowNotifyIcon(bool value) {
            Windows.WinRegistry.SetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "IsShowNotifyIcon", value);
        }
        #endregion

        #region IsCloseMeanExit
        public static bool GetIsCloseMeanExit() {
            object value = Windows.WinRegistry.GetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "IsCloseMeanExit");
            return value != null && value.ToString() == "True";
        }

        public static void SetIsCloseMeanExit(bool value) {
            Windows.WinRegistry.SetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "IsCloseMeanExit", value);
        }
        #endregion

        #region IsShowCommandLine
        public static bool GetIsShowCommandLine() {
            object isAutoBootValue = Windows.WinRegistry.GetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "IsShowCommandLine");
            return isAutoBootValue != null && isAutoBootValue.ToString() == "True";
        }

        public static void SetIsShowCommandLine(bool value) {
            Windows.WinRegistry.SetValue(Registry.Users, NTMinerRegistry.NTMinerRegistrySubKey, "IsShowCommandLine", value);
        }
        #endregion

        #region GetIsRemoteDesktopEnabled
        public static bool GetIsRemoteDesktopEnabled() {
            return (int)Windows.WinRegistry.GetValue(Registry.LocalMachine, "SYSTEM\\CurrentControlSet\\Control\\Terminal Server", "fDenyTSConnections") == 0;
        }
        #endregion
    }
}
