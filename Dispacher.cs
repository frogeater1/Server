using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Google.Protobuf;
using OnlineGame;

namespace Arknights;

public static partial class Dispacher
{
    public static Dictionary<ProtoIdx, MessageParser> parsers = new()
    {
        { ProtoIdx.LogicUpdate, LogicUpdate.Parser },
        { ProtoIdx.RpcMsg, RpcMsg.Parser },
        { ProtoIdx.KeepAlive, KeepAlive.Parser },
        { ProtoIdx.create_room_c2s, create_room_c2s.Parser },
        { ProtoIdx.join_room_c2s, join_room_c2s.Parser },
    };

    public static void ReceiveRoomMsg(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        while (true)
        {
            IMessage msg = Receive(stream);
            Console.WriteLine(msg.ToString());
            switch (msg)
            {
                case create_room_c2s data:
                    RoomManager.CreateRoom(data.Name, client);
                    return;
                case join_room_c2s data:
                    RoomManager.JoinRoom(data.Name, client);
                    return;
                default:
                    Console.WriteLine("ReceiveRoomMsg收到错误的消息: " + msg.GetType() + msg);
                    break;
            }
        }
    }
}