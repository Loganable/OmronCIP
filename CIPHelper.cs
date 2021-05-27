﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OmronCIP
{
    public class CIPHelper
    {
        #region 服务代码
        /// <summary>
        /// CIP命令中的读取数据的服务
        /// </summary>
        public const byte CIP_READ_DATA = 0x4C;

        /// <summary>
        /// CIP命令中的写入数据的服务
        /// </summary>
        public const byte CIP_WRITE_DATA = 0x4D;
        #endregion

        #region 数据类型代码
        /// <summary>
        /// bool型数据，一个字节长度
        /// </summary>
        public const byte CIP_Type_Bool = 0xC1;

        /// <summary>
        /// 短整型，两个字节长度
        /// </summary>
        public const byte CIP_Type_Short = 0xC3;

        /// <summary>
        /// 整型，四个字节长度
        /// </summary>
        public const byte CIP_Type_Int = 0xC4;

        /// <summary>
        /// 长整型，八个字节长度
        /// </summary>
        public const byte CIP_Type_Long = 0xC5;

        /// <summary>
        /// 无符号短整型，两个字节长度
        /// </summary>
        public const byte CIP_Type_UShort = 0xC7;

        /// <summary>
        /// 无符号整型，四个字节长度
        /// </summary>
        public const byte CIP_Type_UInt = 0xC8;

        /// <summary>
        /// 无符号长整型，八个字节长度
        /// </summary>
        public const byte CIP_Type_ULong = 0xC9;

        /// <summary>
        /// 单精度浮点型，四个字节长度
        /// </summary>
        public const byte CIP_Type_Float = 0xCA;

        /// <summary>
        /// 双精度浮点型，八个字节长度
        /// </summary>
        public const byte CIP_Type_Double = 0xCB;

        /// <summary>
        /// 字符串类型数据
        /// </summary>
        public const byte CIP_Type_String = 0xD0;
        #endregion

        #region 辅助生成指令的方法
        /// <summary>
        /// 将标签名转换为Byte数组
        /// </summary>
        /// <param name="tagName">标签名</param>
        /// <returns>请求路径对应字节数组</returns>
        public static byte[] BuildRequestPathCommand(string tagName)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                string[] tagNames = tagName.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < tagNames.Length; i++)
                {
                    string strIndex = string.Empty;
                    int indexFirst = tagNames[i].IndexOf('[');
                    int indexSecond = tagNames[i].IndexOf(']');
                    if (indexFirst > 0 && indexSecond > 0 && indexSecond > indexFirst)
                    {
                        strIndex = tagNames[i].Substring(indexFirst + 1, indexSecond - indexFirst - 1);
                        tagNames[i] = tagNames[i].Substring(0, indexFirst);
                    }

                    ms.WriteByte(0x91);                        // 固定
                    byte[] nameBytes = Encoding.UTF8.GetBytes(tagNames[i]);
                    ms.WriteByte((byte)nameBytes.Length);    // 节点的长度值
                    ms.Write(nameBytes, 0, nameBytes.Length);
                    if (nameBytes.Length % 2 == 1) ms.WriteByte(0x00);

                    if (!string.IsNullOrEmpty(strIndex))
                    {
                        string[] indexs = strIndex.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int j = 0; j < indexs.Length; j++)
                        {
                            int index = Convert.ToInt32(indexs[j]);
                            if (index < 256)
                            {
                                ms.WriteByte(0x28);
                                ms.WriteByte((byte)index);
                            }
                            else
                            {
                                ms.WriteByte(0x29);
                                ms.WriteByte(0x00);
                                ms.WriteByte(BitConverter.GetBytes(index)[0]);
                                ms.WriteByte(BitConverter.GetBytes(index)[1]);
                            }
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 打包生成一个请求读取数据的节点信息，CIP指令信息
        /// </summary>
        /// <param name="tagName">地址</param>
        /// <param name="length">指代数组的长度</param>
        /// <returns>读取指令CIP报文对应字节数组</returns>
        public static byte[] PackRequsetRead(string tagName, int length = 1)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_READ_DATA;
            offset++;

            byte[] requestPath = BuildRequestPathCommand(tagName);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;

            buffer[1] = (byte)((offset - 2) / 2);
            buffer[offset++] = BitConverter.GetBytes(length)[0];
            buffer[offset++] = BitConverter.GetBytes(length)[1];

            byte[] data = new byte[offset];
            Array.Copy(buffer, 0, data, 0, offset);
            return data;
        }

        /// <summary>
        /// 根据指定的数据和类型，打包生成对应的写入指令
        /// </summary>
        /// <param name="tagName">地址信息</param>
        /// <param name="typeCode">数据类型</param>
        /// <param name="value">字节值</param>
        /// <param name="length">如果节点为数组，就是数组长度</param>
        /// <returns>写入指令CIP报文对应字节数组</returns>
        public static byte[] PackRequestWrite(string tagName, ushort typeCode, byte[] value, int length = 1)
        {
            byte[] buffer = new byte[1024];
            int offset = 0;
            buffer[offset++] = CIP_WRITE_DATA;
            offset++;

            byte[] requestPath = BuildRequestPathCommand(tagName);
            requestPath.CopyTo(buffer, offset);
            offset += requestPath.Length;

            buffer[1] = (byte)((offset - 2) / 2);

            buffer[offset++] = BitConverter.GetBytes(typeCode)[0];     // 数据类型
            buffer[offset++] = BitConverter.GetBytes(typeCode)[1];

            buffer[offset++] = BitConverter.GetBytes(length)[0];       // 固定
            buffer[offset++] = BitConverter.GetBytes(length)[1];

            value.CopyTo(buffer, offset);                              // 数值
            offset += value.Length;

            byte[] data = new byte[offset];
            Array.Copy(buffer, 0, data, 0, offset);
            return data;
        }

        /// <summary>
        /// 生成读取直接节点数据信息的内容
        /// </summary>
        /// <param name="slot">PLC所在的槽号</param>
        /// <param name="cips">cip指令内容</param>
        /// <returns>最终的指令值</returns>
        public static byte[] PackCommandSpecificData(byte slot, params byte[][] cips)
        {
            MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);
            ms.WriteByte(0x01);     // 超时
            ms.WriteByte(0x00);
            ms.WriteByte(0x02);     // 项数
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);     // 连接的地址项
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);     // 长度
            ms.WriteByte(0x00);
            ms.WriteByte(0xB2);     // 连接的项数
            ms.WriteByte(0x00);
            ms.WriteByte(0x00);     // 后面数据包的长度，等全部生成后在赋值
            ms.WriteByte(0x00);

            ms.WriteByte(0x52);     // 服务
            ms.WriteByte(0x02);     // 请求路径大小
            ms.WriteByte(0x20);     // 请求路径
            ms.WriteByte(0x06);
            ms.WriteByte(0x24);
            ms.WriteByte(0x01);
            ms.WriteByte(0x0A);     // 超时时间
            ms.WriteByte(0xF0);
            ms.WriteByte(0x00);     // CIP指令长度
            ms.WriteByte(0x00);

            int count = 0;
            if (cips.Length == 1)
            {
                ms.Write(cips[0], 0, cips[0].Length);
                count += cips[0].Length;
            }
            else
            {
                ms.WriteByte(0x0A);   // 固定
                ms.WriteByte(0x02);
                ms.WriteByte(0x20);
                ms.WriteByte(0x02);
                ms.WriteByte(0x24);
                ms.WriteByte(0x01);
                count += 8;

                ms.Write(BitConverter.GetBytes((ushort)cips.Length), 0, 2);  // 写入项数
                ushort offset = (ushort)(0x02 + 2 * cips.Length);
                count += 2 * cips.Length;

                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(BitConverter.GetBytes(offset), 0, 2);
                    offset = (ushort)(offset + cips[i].Length);
                }

                for (int i = 0; i < cips.Length; i++)
                {
                    ms.Write(cips[i], 0, cips[i].Length);
                    count += cips[i].Length;
                }
            }
            ms.WriteByte(0x01);     // Path Size
            ms.WriteByte(0x00);
            ms.WriteByte(0x01);     // port
            ms.WriteByte(slot);

            byte[] data = ms.ToArray();
            ms.Dispose();
            BitConverter.GetBytes((short)count).CopyTo(data, 24);
            data[14] = BitConverter.GetBytes((short)(data.Length - 16))[0];
            data[15] = BitConverter.GetBytes((short)(data.Length - 16))[1];
            return data;
        }

        /// <summary>
        /// 将CommandSpecificData的命令，打包成可发送的数据指令
        /// </summary>
        /// <param name="msgType">报文类型代码</param>
        /// <param name="sessionID">会话id</param>
        /// <param name="commandSpecificData">CommandSpecificData命令</param>
        /// <returns>最终可发送的数据命令</returns>
        public static byte[] PackRequest(byte msgType, byte[] sessionID, byte[] commandSpecificData)
        {
            byte[] buffer = new byte[commandSpecificData.Length + 24];
            Array.Copy(commandSpecificData, 0, buffer, 24, commandSpecificData.Length);
            BitConverter.GetBytes(msgType).CopyTo(buffer, 0);
            sessionID.CopyTo(buffer, 4);
            BitConverter.GetBytes((ushort)commandSpecificData.Length).CopyTo(buffer, 2);
            return buffer;
        }
        #endregion

        #region 自定义方法
        /// <summary>
        /// 解析应答报文
        /// </summary>
        /// <param name="response">PLC应答报文</param>
        /// <returns>返回解析后的数据内容对象</returns>
        public static object UnpackResult(byte[] response)
        {            
            if (response.Length < 44) throw new Exception("报文长度异常！");
            if (response[42] != 0x00 || response[43] != 0x00) throw new Exception("报文状态异常");

            object result = null;
            int dataLength;
            if (response[40] == 0xCC)//Read应答报文
            {
                switch (response[44])
                {
                    case CIP_Type_Bool:
                        dataLength = response.Length - 46;
                        if (dataLength == 2 && response[46] <= 1 && response[47] == 0)
                        {
                            result = Convert.ToInt16((response[46] & 0x00ff) | (response[47] << 8));
                        }
                        else
                        {
                            for (int j = 0; j < dataLength; j++)
                            {
                                result += Convert.ToString(Reverse(response[46 + j]), 2).PadLeft(8, '0');
                            }
                        }
                        break;
                    case CIP_Type_Short:
                        dataLength = (response.Length - 46) / 2;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToInt16(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<short> sList = new List<short>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                sList.Add(BitConverter.ToInt16(response.Skip(46 + 2 * j).ToArray(), 0));
                            }
                            result = sList;
                        }
                        break;
                    case CIP_Type_Int:
                        dataLength = (response.Length - 46) / 4;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToInt32(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<int> intList = new List<int>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                intList.Add(BitConverter.ToInt32(response.Skip(46 + 4 * j).ToArray(), 0));
                            }
                            result = intList;
                        }
                        break;
                    case CIP_Type_Long:
                        dataLength = (response.Length - 46) / 8;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToInt64(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<long> longList = new List<long>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                longList.Add(BitConverter.ToInt32(response.Skip(46 + 8 * j).ToArray(), 0));
                            }
                            result = longList;
                        }
                        break;
                    case CIP_Type_UShort:
                        dataLength = (response.Length - 46) / 2;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToUInt16(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<ushort> usList = new List<ushort>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                usList.Add(BitConverter.ToUInt16(response.Skip(46 + 2 * j).ToArray(), 0));
                            }
                            result = usList;
                        }
                        break;
                    case CIP_Type_UInt:
                        dataLength = (response.Length - 46) / 4;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToUInt32(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<uint> uintList = new List<uint>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                uintList.Add(BitConverter.ToUInt32(response.Skip(46 + 4 * j).ToArray(), 0));
                            }
                            result = uintList;
                        }
                        break;
                    case CIP_Type_ULong:
                        dataLength = (response.Length - 46) / 8;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToUInt64(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<ulong> ulongList = new List<ulong>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                ulongList.Add(BitConverter.ToUInt64(response.Skip(46 + 8 * j).ToArray(), 0));
                            }
                            result = ulongList;
                        }
                        break;
                    case CIP_Type_Float:
                        dataLength = (response.Length - 46) / 4;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToSingle(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<float> floatList = new List<float>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                floatList.Add(BitConverter.ToSingle(response.Skip(46 + 4 * j).ToArray(), 0));
                            }
                            result = floatList;
                        }
                        break;
                    case CIP_Type_Double:
                        dataLength = (response.Length - 46) / 8;
                        if (dataLength == 1)
                        {
                            result = BitConverter.ToDouble(response.Skip(46).ToArray(), 0);
                        }
                        else
                        {
                            List<double> floatList = new List<double>();
                            for (int j = 0; j < dataLength; j++)
                            {
                                floatList.Add(BitConverter.ToDouble(response.Skip(46 + 8 * j).ToArray(), 0));
                            }
                            result = floatList;
                        }
                        break;
                    case CIP_Type_String:
                        dataLength = (response[46] & 0x00ff) | (response[47] << 8);
                        byte[] temp = new byte[dataLength];
                        Array.Copy(response, 48, temp, 0, dataLength);
                        result = Encoding.UTF8.GetString(temp);
                        break;
                    default:
                        throw new Exception("数据类型异常");
                }
            }
            else
                throw new Exception("报文格式错误");
            return result;
        }

        /// <summary>
        /// 获取数据类型代码
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns>对应类型代码</returns>
        public static byte GetTypeCode(TypeCode type)
        {
            byte typeCode = 0;
            switch (type)
            {
                case TypeCode.Boolean:
                    typeCode = CIP_Type_Bool;
                    break;
                case TypeCode.Int16:
                    typeCode = CIP_Type_Short;
                    break;
                case TypeCode.Int32:
                    typeCode = CIP_Type_Int;
                    break;
                case TypeCode.Int64:
                    typeCode = CIP_Type_Long;
                    break;
                case TypeCode.UInt16:
                    typeCode = CIP_Type_UShort;
                    break;
                case TypeCode.UInt32:
                    typeCode = CIP_Type_UInt;
                    break;
                case TypeCode.UInt64:
                    typeCode = CIP_Type_ULong;
                    break;
                case TypeCode.Single:
                    typeCode = CIP_Type_Float;
                    break;
                case TypeCode.Double:
                    typeCode = CIP_Type_Double;
                    break;
                case TypeCode.String:
                    typeCode = CIP_Type_String;
                    break;
                default:
                    break;
            }
            return typeCode;
        }

        /// <summary>
        /// 将数据对象转化成byte数组
        /// </summary>
        /// <param name="data">数据对象</param>
        /// <param name="type">数据类型</param>
        /// <returns>数据对象对应的byte数组</returns>
        public static byte[] DataToBytes(object data, TypeCode type)
        {
            byte[] value;
            switch (type)
            {
                case TypeCode.Boolean:
                    value = new byte[2];
                    value[0] = Convert.ToByte(data);
                    break;
                case TypeCode.Int16:
                    value = BitConverter.GetBytes(Convert.ToInt16(data));
                    break;
                case TypeCode.Int32:
                    value = BitConverter.GetBytes(Convert.ToInt32(data));
                    break;
                case TypeCode.Int64:
                    value = BitConverter.GetBytes(Convert.ToInt64(data));
                    break;
                case TypeCode.UInt16:
                    value = BitConverter.GetBytes(Convert.ToUInt16(data));
                    break;
                case TypeCode.UInt32:
                    value = BitConverter.GetBytes(Convert.ToUInt32(data));
                    break;
                case TypeCode.UInt64:
                    value = BitConverter.GetBytes(Convert.ToUInt64(data));
                    break;
                case TypeCode.Single:
                    value = BitConverter.GetBytes(Convert.ToSingle(data));
                    break;
                case TypeCode.Double:
                    value = BitConverter.GetBytes(Convert.ToDouble(data));
                    break;
                case TypeCode.String:
                    value = Encoding.UTF8.GetBytes(Convert.ToString(data));
                    break;
                default:
                    value = new byte[] { 0x00, 0x00 };
                    break;
            }
            return value;
        }

        /// <summary>
        /// 逆置Byte(8位)数据的比特位，例如 11001000 -> 00010011
        /// </summary>
        /// <param name="data">需逆置的Byte数据</param>
        /// <returns>逆置后的数据</returns>
        private static byte Reverse(byte data)
        {
            data = (byte)(((data >> 1) & 0x55) | ((data & 0x55) << 1));
            data = (byte)(((data >> 2) & 0x33) | ((data & 0x33) << 2));
            data = (byte)(((data >> 4) & 0x0F) | ((data & 0x0F) << 4));
            return data;
        }
        #endregion
    }
}
