using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Google.Protobuf;
using OnlineGame;

namespace Arknights;

public static class RoomManager
{
    public static ConcurrentDictionary<string, Room> rooms = new();

    public static void CreateRoom(create_room_c2s msg, TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        if (rooms.ContainsKey(msg.Name))
        {
            Dispacher.Send(stream, new create_room_s2c { ResCode = ResCode.DuplicateName });
            return;
        }

        var room = new Room(msg, client);
        try
        {
            ResCode res = rooms.TryAdd(msg.Name, room) ? ResCode.Success : ResCode.DuplicateName;
            Console.WriteLine(new StackTrace());
            Dispacher.Send(stream, new create_room_s2c { ResCode = res });
        }
        catch (Exception e)
        {
            Console.WriteLine(e.StackTrace);
            throw;
        }
    }

    public static void JoinRoom(join_room_c2s msg, TcpClient client2)
    {
        NetworkStream stream2 = client2.GetStream();
        if (!rooms.TryGetValue(msg.Name, out Room? room))
        {
            Dispacher.Send(stream2, new join_room_s2c { ResCode = ResCode.CantFindRoom });
            return;
        }

        lock (room)
        {
            //多线程
            if (room.clients[1] != null)
            {
                Dispacher.Send(stream2, new join_room_s2c { ResCode = ResCode.RoomIsFull });
                return;
            }

            //发keepAlive确认
            try
            {
                Dispacher.Send(room.clients[0].GetStream(), new KeepAlive { Data = 1, });
                Dispacher.Send(stream2, new KeepAlive { Data = 1, });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return;
            }

            //收keepAlive确认
            IMessage? msg1 = null;
            IMessage? msg2 = null;
            var t1 = Task.Run(() => { msg1 = Dispacher.Receive(room.clients[0].GetStream()); });
            var t2 = Task.Run(() => { msg2 = Dispacher.Receive(stream2); });
            Console.WriteLine("start wait alive" + DateTime.Now);
            Task.WaitAll(new[] { t1, t2 }, 5000);
            Console.WriteLine("end wait alive" + DateTime.Now);
            if (!t1.IsCompleted || msg1 is not KeepAlive { Data: 1 })
            {
                rooms.TryRemove(msg.Name, out _);
                Dispacher.Send(stream2, new join_room_s2c { ResCode = ResCode.CantFindRoom });
                return;
            }

            if (!t2.IsCompleted || msg2 is not KeepAlive { Data: 1 })
            {
                return;
            }

            room.Join(msg, client2);
            //发匹配成功消息
            try
            {
                Dispacher.Send(room.clients[0].GetStream(), new join_room_s2c
                {
                    ResCode = ResCode.Success,
                    Player = room.players[1],
                });
                Dispacher.Send(stream2, new join_room_s2c
                {
                    ResCode = ResCode.Success,
                    Player = room.players[0],
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw;
            }


            //收进入场景消息
            IMessage? msg3 = null;
            IMessage? msg4 = null;
            var t3 = Task.Run(() => { msg1 = Dispacher.Receive(room.clients[0].GetStream()); });
            var t4 = Task.Run(() => { msg2 = Dispacher.Receive(stream2); });
            Console.WriteLine("start wait gamestart" + DateTime.Now);
            bool timeout = Task.WaitAll(new[] { t3, t4 }, 5000);
            Console.WriteLine("end wait gamestart" + DateTime.Now);
            if (t3.IsCompleted && t4.IsCompleted || (timeout && (t3.IsCompleted || t4.IsCompleted)))
            {
                room.GameStart();
            }
        }
    }
}