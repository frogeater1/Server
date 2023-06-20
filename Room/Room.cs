using System.Collections.Concurrent;
using System.Net.Sockets;
using Google.Protobuf;
using OnlineGame;

namespace Arknights;

public class Room
{
    public string name;

    public TcpClient[] clients = new TcpClient[2];
    public Player[] players = new Player[2];

    public ConcurrentQueue<RpcMsg> waitingMsgs = new();

    public Room(create_room_c2s msg, TcpClient client1)
    {
        name = msg.Name;
        clients[0] = client1;
        players[0] = msg.Player;
    }

    public void Join(join_room_c2s msg, TcpClient client2)
    {
        clients[1] = client2;
        players[1] = msg.Player;
    }

    public void GameStart()
    {
        Console.WriteLine("GameStart!");
        Task.Run(() => ReceiveMsg(0));
        Task.Run(() => ReceiveMsg(1));
        var t = new System.Timers.Timer();
        t.Interval = 1000f / 60;
        t.Elapsed += (sender, args) =>
        {
            RpcMsg[] rpc_msgs;
            lock (waitingMsgs)
            {
                rpc_msgs = waitingMsgs.ToArray();
                waitingMsgs.Clear();
            }

            var logic_update = new LogicUpdate
            {
                Rpcs = { rpc_msgs }
            };
            if (clients[0] is { Connected: true })
            {
                Dispacher.Send(clients[0].GetStream(), logic_update);
            }

            if (clients[1] is { Connected: true })
            {
                Dispacher.Send(clients[1].GetStream(), logic_update);
            }
        };
        t.Enabled = true;
    }

    public void ReceiveMsg(int idx)
    {
        var stream = clients[idx].GetStream();
        while (true)
        {
            var msg = Dispacher.Receive(stream);
            Console.WriteLine(msg);
            if (msg is RpcMsg rpcMsg)
            {
                waitingMsgs.Enqueue(rpcMsg);
            }
            else
            {
                Console.WriteLine("收到错误的消息: " + msg);
            }
        }
    }
}