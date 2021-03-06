﻿using NTMiner.Controllers;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace NTMiner.Services.Client {
    public class MinerStudioService {
        public static readonly MinerStudioService Instance = new MinerStudioService();

        private readonly string _controllerName = RpcRoot.GetControllerName<IMinerStudioController>();
        private MinerStudioService() {
        }

        /// <summary>
        /// 本机网络调用
        /// </summary>
        /// <param name="clientPort"></param>
        /// <param name="callback"></param>
        public void ShowMainWindowAsync(int clientPort, Action<bool, Exception> callback) {
            Task.Factory.StartNew(() => {
                try {
                    using (HttpClient client = RpcRoot.CreateHttpClient()) {
                        Task<HttpResponseMessage> getHttpResponse = client.PostAsync($"http://localhost:{clientPort.ToString()}/api/{_controllerName}/{nameof(IMinerStudioController.ShowMainWindow)}", null);
                        bool response = getHttpResponse.Result.Content.ReadAsAsync<bool>().Result;
                        callback?.Invoke(response, null);
                    }
                }
                catch (Exception e) {
                    callback?.Invoke(false, e);
                }
            });
        }

        /// <summary>
        /// 本机同步网络调用
        /// </summary>
        public void CloseMinerStudio() {
            string location = NTMinerRegistry.GetLocation();
            if (string.IsNullOrEmpty(location) || !File.Exists(location)) {
                return;
            }
            string processName = Path.GetFileNameWithoutExtension(location);
            if (Process.GetProcessesByName(processName).Length == 0) {
                return;
            }
            bool isClosed = false;
            try {
                using (HttpClient client = RpcRoot.CreateHttpClient()) {
                    Task<HttpResponseMessage> getHttpResponse = client.PostAsJsonAsync($"http://localhost:{NTKeyword.MinerStudioPort.ToString()}/api/{_controllerName}/{nameof(IMinerStudioController.CloseMinerStudio)}", new SignRequest { });
                    ResponseBase response = getHttpResponse.Result.Content.ReadAsAsync<ResponseBase>().Result;
                    isClosed = response.IsSuccess();
                }
            }
            catch (Exception e) {
                Logger.ErrorDebugLine(e);
            }
            if (!isClosed) {
                try {
                    Windows.TaskKill.Kill(processName, waitForExit: true);
                }
                catch (Exception e) {
                    Logger.ErrorDebugLine(e);
                }
            }
        }
    }
}
