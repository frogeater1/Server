using System.Collections.Concurrent;
using System.Net.Sockets;
using Google.Protobuf;
using OnlineGame;

namespace Arknights;

public static class RoomManager
{
    public static ConcurrentDictionary<string, Room> rooms = new();

    public static void CreateRoom(string RoomName, TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        if (rooms.ContainsKey(RoomName))
        {
            Dispacher.Send(stream, new create_room_s2c { ResCode = (int)ResCode.DuplicateName });
            return;
        }

        var room = new Room(RoomName, client);
        try
        {
            ResCode res = rooms.TryAdd(RoomName, room) ? ResCode.Success : ResCode.DuplicateName;
            Dispacher.Send(stream, new create_room_s2c { ResCode = (int)res });
        }
        catch (Exception e)
        {
            Console.WriteLine(e.StackTrace);
            throw;
        }
    }

    public static void JoinRoom(string name, TcpClient client2)
    {
        NetworkStream stream2 = client2.GetStream();
        if (!rooms.TryGetValue(name, out Room? room))
        {
            Dispacher.Send(stream2, new join_room_s2c { ResCode = (int)ResCode.CantFindRoom });
            return;
        }

        lock (room)
        {
            //多线程
            if (room.clients[1] != null)
            {
                Dispacher.Send(stream2, new join_room_s2c { ResCode = (int)ResCode.RoomIsFull });
                return;
            }

            try
            {
                Dispacher.Send(room.clients[0].GetStream(), new KeepAlive
                {
                    Data = 1,
                });
                Dispacher.Send(stream2, new KeepAlive()
                {
                    Data = 1,
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return;
            }

            IMessage? msg1 = null;
            IMessage? msg2 = null;
            var t1 = Task.Run(() => { msg1 = Dispacher.Receive(room.clients[0].GetStream()); });
            var t2 = Task.Run(() => { msg2 = Dispacher.Receive(stream2); });
            Console.WriteLine("start wait alive" + DateTime.Now);
            Task.WaitAll(new[] { t1, t2 }, 5000);
            Console.WriteLine("end wait alive" + DateTime.Now);
            if (!t1.IsCompleted || msg1 is not KeepAlive { Data: 1 })
            {
                rooms.TryRemove(name, out _);
                Dispacher.Send(stream2, new join_room_s2c { ResCode = (int)ResCode.CantFindRoom });
                return;
            }

            if (!t2.IsCompleted || msg2 is not KeepAlive { Data: 1 })
            {
                return;
            }

            try
            {
                Dispacher.Send(stream2, new join_room_s2c { ResCode = (int)ResCode.Success });
                Dispacher.Send(room.clients[0].GetStream(), new join_room_s2c { ResCode = (int)ResCode.Success });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                throw;
            }

            room.Join(client2);
        }
    }
}