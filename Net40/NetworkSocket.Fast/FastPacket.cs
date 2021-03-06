﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NetworkSocket.Fast
{
    /// <summary>
    /// 通讯协议的封包
    /// </summary>
    [DebuggerDisplay("Command = {Command}")]
    public sealed class FastPacket : PacketBase
    {
        /// <summary>
        /// 获取命令值
        /// </summary>
        public int Command { get; private set; }

        /// <summary>
        /// 获取哈希值
        /// </summary>
        public int HashCode { get; private set; }

        /// <summary>
        /// 获取数据体的数据
        /// </summary>
        public byte[] Body { get; private set; }

        /// <summary>
        /// 通讯协议的封包
        /// </summary>
        /// <param name="cmd">命令值</param>
        public FastPacket(int cmd)
        {
            this.Command = cmd;
            this.HashCode = Guid.NewGuid().GetHashCode();
        }

        /// <summary>
        /// 通讯协议的封包
        /// </summary>
        /// <param name="cmd">命令值</param>
        /// <param name="hashCode">哈希码</param>
        /// <param name="body">数据体</param>
        public FastPacket(int cmd, int hashCode, byte[] body)
        {
            this.Command = cmd;
            this.HashCode = hashCode;
            this.Body = body;
        }

        /// <summary>
        /// 转换为二进制数据
        /// </summary>
        /// <returns></returns>
        public override byte[] ToByteArray()
        {
            // 总长度(4) + command(4) + hashCode(4)
            const int headLength = 12;
            // 总长度
            int totalLength = this.Body == null ? headLength : headLength + this.Body.Length;

            var builder = new ByteBuilder(totalLength);
            builder.Add(totalLength, Endians.Big);
            builder.Add(this.Command, Endians.Big);
            builder.Add(this.HashCode, Endians.Big);
            builder.Add(this.Body);
            return builder.Source;
        }

        /// <summary>
        /// 将参数序列化并写入为Body
        /// </summary>
        /// <param name="serializer">序列化工具</param>
        /// <param name="parameters">参数</param>
        public void SetBodyBinary(ISerializer serializer, params object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return;
            }
            var builder = new ByteBuilder(8);
            foreach (var item in parameters)
            {
                // 序列化参数为二进制内容
                var paramBytes = serializer.Serialize(item);
                // 添加参数内容长度            
                builder.Add(paramBytes == null ? 0 : paramBytes.Length, Endians.Big);
                // 添加参数内容
                builder.Add(paramBytes);
            }
            this.Body = builder.ToArray();
        }

        /// <summary>
        /// 将Body的数据解析为参数
        /// </summary>        
        /// <returns></returns>
        public List<byte[]> GetBodyParameter()
        {
            var parameterList = new List<byte[]>();

            if (this.Body == null || this.Body.Length < 4)
            {
                return parameterList;
            }

            var index = 0;
            while (index < this.Body.Length)
            {
                // 参数长度
                var length = ByteConverter.ToInt32(this.Body, index, Endians.Big);
                index = index + 4;
                var paramBytes = new byte[length];
                // 复制出参数的数据
                Buffer.BlockCopy(this.Body, index, paramBytes, 0, length);
                index = index + length;
                parameterList.Add(paramBytes);
            }

            return parameterList;
        }

        /// <summary>
        /// 解析一个数据包       
        /// 不足一个封包时返回null
        /// </summary>
        /// <param name="builder">接收到的历史数据</param>
        /// <returns></returns>
        public static FastPacket GetPacket(ByteBuilder builder)
        {
            // 包头长度
            const int headLength = 12;
            // 不会少于12
            if (builder.Length < headLength)
            {
                return null;
            }

            // 包长
            int totalLength = builder.ToInt32(0, Endians.Big);
            // 包长要小于等于数据长度
            if (totalLength > builder.Length || totalLength < headLength)
            {
                return null;
            }

            // cmd
            int cmd = builder.ToInt32(4, Endians.Big);
            // 哈希值
            int hashCode = builder.ToInt32(8, Endians.Big);
            // 实体数据
            byte[] body = builder.ToArray(12, totalLength - headLength);

            // 清空本条数据
            builder.Remove(totalLength);
            return new FastPacket(cmd, hashCode, body);
        }
    }
}
