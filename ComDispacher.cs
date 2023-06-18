using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Google.Protobuf;
using OnlineGame;

namespace Arknights
{
//这个patial放server和client共用的部分
    public static partial class Dispacher
    {
        private static IMessage Parse(int protoIdx, byte[] bytes)
        {
            return parsers[(ProtoIdx)protoIdx].ParseFrom(bytes);
        }


        /// <summary>
        /// 不能直接用这个发消息，只能用SendMsg入列，然后由socket中开启的线程自动发出去
        /// </summary>
        public static void Send(NetworkStream stream, IMessage msg)
        {
            var msg_bytes = msg.ToByteArray();
            var idx = (int)GetProtoIdx(msg);
            stream.Write(BitConverter.GetBytes(idx), 0, sizeof(int));
            stream.Write(BitConverter.GetBytes(msg_bytes.Length), 0, sizeof(int));
            stream.Write(msg_bytes, 0, msg_bytes.Length);
        }

        public static IMessage Receive(NetworkStream stream)
        {
            byte[] rcv_head_bytes = new byte[sizeof(int)];
            stream.Read(rcv_head_bytes, 0, sizeof(int));
            int proto_idx = BitConverter.ToInt32(rcv_head_bytes, 0);
            Array.Clear(rcv_head_bytes, 0, sizeof(int));
            stream.Read(rcv_head_bytes, 0, sizeof(int));
            int need_rcv_data_length = BitConverter.ToInt32(rcv_head_bytes, 0);

            byte[] rcv_data_bytes = new byte[need_rcv_data_length];
            stream.Read(rcv_data_bytes, 0, rcv_data_bytes.Length);

            return Parse(proto_idx, rcv_data_bytes);
        }

        public static ProtoIdx GetProtoIdx(IMessage msg)
        {
            return msg switch
            {
                create_room_s2c => ProtoIdx.create_room_s2c,
                create_room_c2s => ProtoIdx.create_room_c2s,
                join_room_s2c => ProtoIdx.join_room_s2c,
                join_room_c2s => ProtoIdx.join_room_c2s,
                LogicUpdate => ProtoIdx.LogicUpdate,
                RpcMsg => ProtoIdx.RpcMsg,
                KeepAlive => ProtoIdx.KeepAlive,
                GameStart => ProtoIdx.GameStart,
                _ => throw new System.Exception("未知的消息类型"),
            };
        }
    }

    public enum ProtoIdx
    {
        create_room_s2c = 1000,
        create_room_c2s = 1001,
        join_room_s2c = 1002,
        join_room_c2s = 1003,
        GameStart = 1004,
        LogicUpdate = 2000,
        RpcMsg = 2001,
        KeepAlive = 2002,
    }
}