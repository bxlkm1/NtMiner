﻿using System;
using System.Collections.Generic;
using WebSocketSharp;

namespace NTMiner {
    class Program {
        static void Main() {
            using (var ws = new WebSocket("ws://localhost:8088/")) {
                ws.OnOpen += (sender, e) => {
                    Write.UserWarn($"WebSocket Open");
                    ws.Send("Hi!");
                };
                ws.OnMessage += (sender, e) => {
                    if (string.IsNullOrEmpty(e.Data) || e.Data[0] != '{' || e.Data[e.Data.Length - 1] != '}') {
                        return;
                    }
                    WsMessage message = VirtualRoot.JsonSerializer.Deserialize<WsMessage>(e.Data);
                    if (message == null) {
                        return;
                    }
                    switch (message.GetType()) {
                        case WsMessageType.GetSpeed:
                            ws.SendAsync(new WsMessage().SetType(WsMessageType.Speed)
                                .SetData(new Dictionary<string, object> {
                                        {"str", "hello" },
                                        {"num", 111 },
                                        {"date", DateTime.Now }
                                    }).ToJson(), completed: null);
                            break;
                        default:
                            Write.UserInfo(e.Data);
                            break;
                    }
                };
                ws.OnError += (sender, e) => {
                    Write.UserError(e.Message);
                };
                ws.OnClose += (sender, e) => {
                    Write.UserWarn($"WebSocket Close {e.Code} {e.Reason}");
                };
                ws.Log.Level = LogLevel.Trace;
                ws.Connect();
                Windows.ConsoleHandler.Register(ws.Close);
                Console.WriteLine("\nType 'exit' to exit.\n");
                while (true) {
                    Console.Write("> ");
                    var action = Console.ReadLine();
                    if (action == "exit") {
                        break;
                    }

                    if (!ws.IsAlive) {
                        ws.Connect();
                    }
                }
            }
        }
    }
}
