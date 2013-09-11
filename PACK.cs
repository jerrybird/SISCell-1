using System;
using System.IO;
using System.Text;
using SevenZip;
using System.Collections.Generic;

namespace SISCell
{
    class PACK
    {
        /*数据包格式
        ----------------------------------------|
        |数据包头|包长度|保留位|--------数据正文-----|校验位|
        |0------1|2----5|6----7|---------------------|--2---|

        数据包头        ---DBDB(2)
        包长度          ---uint(4)
        保留位          ---ushort(2)
        数据正文
        校验位         ---无符号字符(2), 数据正文CRC校验所得*/

        /*数据正文
        ----------------------------------------|
        |数值点个数|字符点个数|-----------数值点信息----------|------------字符点信息-----------|
        |8--------9|10------11|SN  | 时标|数据|SN  | 时标|数据|SN  | 时标|数据 |SN  | 时标|数据 |
                              |2   |  8  | 4  |2   |  8  |  4 |2   |  8  |  ?  |2   |  8  |  ?  |

        数值点个数     ---ushort(2)
        字符点个数     ---ushort(2)

        数值点信息
        SN             ---ushort(2)
        时间           ---uint(4)
        数据           ---float(4)

        字符点信息
        SN             ---ushort(2)
        时间           ---uint(4)
        字符长度       ---ushort(2)
        字符           ---byte(1)*长度

        数据报总长度为：4+数值点信息长度+字符点信息长度
        数值点信息长度为：10*个数
        字符点信息长度为：8*个数+所有长度(不定)*/

        private LOG err = new LOG();
        public byte[] _pack;
        private int _packSize;
        private int _buflen;
        private const int MAX_LEN = 1024;
        private byte[][] Ring = new byte[MAX_LEN][];
        private int iget = 0;
        private int iput = 0;
        private static readonly byte[] _head = new byte[] { 0xDB, 0xDB };
        private static readonly ushort[] CRC16Table = 
        {  
           0x0, 0x1021, 0x2042, 0x3063, 0x4084, 0x50A5, 0x60C6, 0x70E7,  
           0x8108, 0x9129, 0xA14A, 0xB16B, 0xC18C, 0xD1AD, 0xE1CE, 0xF1EF,  
           0x1231, 0x210, 0x3273, 0x2252, 0x52B5, 0x4294, 0x72F7, 0x62D6,  
           0x9339, 0x8318, 0xB37B, 0xA35A, 0xD3BD, 0xC39C, 0xF3FF, 0xE3DE,  
           0x2462, 0x3443, 0x420, 0x1401, 0x64E6, 0x74C7, 0x44A4, 0x5485,  
           0xA56A, 0xB54B, 0x8528, 0x9509, 0xE5EE, 0xF5CF, 0xC5AC, 0xD58D,  
           0x3653, 0x2672, 0x1611, 0x630, 0x76D7, 0x66F6, 0x5695, 0x46B4,  
           0xB75B, 0xA77A, 0x9719, 0x8738, 0xF7DF, 0xE7FE, 0xD79D, 0xC7BC,  
           0x48C4, 0x58E5, 0x6886, 0x78A7, 0x840, 0x1861, 0x2802, 0x3823,  
           0xC9CC, 0xD9ED, 0xE98E, 0xF9AF, 0x8948, 0x9969, 0xA90A, 0xB92B,  
           0x5AF5, 0x4AD4, 0x7AB7, 0x6A96, 0x1A71, 0xA50, 0x3A33, 0x2A12,  
           0xDBFD, 0xCBDC, 0xFBBF, 0xEB9E, 0x9B79, 0x8B58, 0xBB3B, 0xAB1A,  
           0x6CA6, 0x7C87, 0x4CE4, 0x5CC5, 0x2C22, 0x3C03, 0xC60, 0x1C41,  
           0xEDAE, 0xFD8F, 0xCDEC, 0xDDCD, 0xAD2A, 0xBD0B, 0x8D68, 0x9D49,  
           0x7E97, 0x6EB6, 0x5ED5, 0x4EF4, 0x3E13, 0x2E32, 0x1E51, 0xE70,  
           0xFF9F, 0xEFBE, 0xDFDD, 0xCFFC, 0xBF1B, 0xAF3A, 0x9F59, 0x8F78,  
           0x9188, 0x81A9, 0xB1CA, 0xA1EB, 0xD10C, 0xC12D, 0xF14E, 0xE16F,  
           0x1080, 0xA1, 0x30C2, 0x20E3, 0x5004, 0x4025, 0x7046, 0x6067,  
           0x83B9, 0x9398, 0xA3FB, 0xB3DA, 0xC33D, 0xD31C, 0xE37F, 0xF35E,  
           0x2B1, 0x1290, 0x22F3, 0x32D2, 0x4235, 0x5214, 0x6277, 0x7256,  
           0xB5EA, 0xA5CB, 0x95A8, 0x8589, 0xF56E, 0xE54F, 0xD52C, 0xC50D,  
           0x34E2, 0x24C3, 0x14A0, 0x481, 0x7466, 0x6447, 0x5424, 0x4405,  
           0xA7DB, 0xB7FA, 0x8799, 0x97B8, 0xE75F, 0xF77E, 0xC71D, 0xD73C,  
           0x26D3, 0x36F2, 0x691, 0x16B0, 0x6657, 0x7676, 0x4615, 0x5634,  
           0xD94C, 0xC96D, 0xF90E, 0xE92F, 0x99C8, 0x89E9, 0xB98A, 0xA9AB,  
           0x5844, 0x4865, 0x7806, 0x6827, 0x18C0, 0x8E1, 0x3882, 0x28A3,  
           0xCB7D, 0xDB5C, 0xEB3F, 0xFB1E, 0x8BF9, 0x9BD8, 0xABBB, 0xBB9A,  
           0x4A75, 0x5A54, 0x6A37, 0x7A16, 0xAF1, 0x1AD0, 0x2AB3, 0x3A92,  
           0xFD2E, 0xED0F, 0xDD6C, 0xCD4D, 0xBDAA, 0xAD8B, 0x9DE8, 0x8DC9,  
           0x7C26, 0x6C07, 0x5C64, 0x4C45, 0x3CA2, 0x2C83, 0x1CE0, 0xCC1,  
           0xEF1F, 0xFF3E, 0xCF5D, 0xDF7C, 0xAF9B, 0xBFBA, 0x8FD9, 0x9FF8,  
           0x6E17, 0x7E36, 0x4E55, 0x5E74, 0x2E93, 0x3EB2, 0xED1, 0x1EF0  
        };

        public int Verify(byte[] receive, int packlen)   //效验包的内容
        {
            if ((receive[0] == 0xDB && receive[1] == 0xDB))
            {
                _packSize = BitConverter.ToInt32(receive, 2);
                _pack = new byte[_packSize];
                _buflen = 0;
            }

            if (_buflen + packlen > _packSize) return 3;

            Buffer.BlockCopy(receive, 0, _pack, _buflen, packlen);
            _buflen += packlen;
            if (_buflen < _packSize) return 2;

            if (CRC16(_pack, 8, _packSize - 10) != BitConverter.ToUInt16(_pack, _packSize - 2)) return 1;

            Ring[iput++] = _pack;
            iput %= MAX_LEN;

            return 0;
        }

        private ushort CRC16(byte[] data, int start, int length)
        {
            ushort crc16 = 0x0000;
            while (length-- > 0)
            {
                crc16 = (ushort)((crc16 << 8) ^ CRC16Table[((crc16 >> 8) ^ data[start++]) & 0xff]);
            }
            return crc16;
        }

        public bool GetData(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {

            byte[] tmp = null;
            if (null == (tmp = Ring[iget])) return false;
            Ring[iget++] = null;
            iget %= MAX_LEN;

            int _size = tmp.Length;
            frmMain.bufMod = tmp[6];
            byte[] data = new byte[_size - 8];
            Buffer.BlockCopy(tmp, 8, data, 0, _size - 10);
            Uncompress(ref data);

            try
            {
                int idx = 4;
                for (int i = 0; i < nrst.Length; ++i)
                {
                    nrst[i].sn = BitConverter.ToInt16(data, idx);
                    nrst[i].dtm = new DateTime(1970, 1, 1).AddSeconds((BitConverter.ToInt32(data, idx + 2)));
                    nrst[i].val = BitConverter.ToSingle(data, idx + 6);
                    idx += 10;
                }

                int strlen = 0;
                for (int i = 0; i < srst.Length; ++i)
                {
                    srst[i].sn = BitConverter.ToInt16(data, idx);
                    srst[i].dtm = new DateTime(1970, 1, 1).AddSeconds((BitConverter.ToInt32(data, idx + 2)));
                    strlen = BitConverter.ToUInt16(data, idx + 6);
                    srst[i].val = Encoding.Default.GetString(data, idx + 8, strlen);
                    idx += 8 + strlen;
                }
            }
            catch (Exception ex)
            {
                err.WrtMsg(ex.Message);
                return false;
            }

            return true;
        }

        public byte[] PutData(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            int nlen = 10 * nNum;
            byte[] numbyt = new byte[nlen];

            int numidx = 0;
            foreach (numInf nr in nrst)
            {
                Buffer.BlockCopy(BitConverter.GetBytes((short)nr.sn), 0, numbyt, numidx, 2);
                Buffer.BlockCopy(BitConverter.GetBytes((int)(nr.dtm - new DateTime(1970, 1, 1)).TotalSeconds), 0, numbyt, numidx + 2, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(nr.val), 0, numbyt, numidx + 6, 4);
                numidx += 10;
            }

            int slen = 0;
            slen = 8 * sNum;
            byte[][] bval = new byte[sNum][];
            int i = 0;
            foreach (strInf sr in srst)
            {
                bval[i] = Encoding.Default.GetBytes(sr.val);
                slen += bval[i++].Length;
            }
            byte[] strbyt = new byte[slen];

            int stridx = 0;
            i = 0;
            foreach (strInf sr in srst)
            {
                Buffer.BlockCopy(BitConverter.GetBytes((short)sr.sn), 0, strbyt, stridx, 2);
                Buffer.BlockCopy(BitConverter.GetBytes((int)(sr.dtm - new DateTime(1970, 1, 1)).TotalSeconds), 0, strbyt, stridx + 2, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((short)bval[i].Length), 0, strbyt, stridx + 6, 2);
                Buffer.BlockCopy(bval[i], 0, strbyt, stridx + 8, bval[i].Length);
                stridx += 8 + bval[i++].Length;
            }

            byte[] data = new byte[4 + nlen + slen];
            Buffer.BlockCopy(BitConverter.GetBytes((short)nNum), 0, data, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((short)sNum), 0, data, 2, 2);
            Buffer.BlockCopy(numbyt, 0, data, 4, nlen);
            Buffer.BlockCopy(strbyt, 0, data, 4 + nlen, slen);
            Compress(ref data);

            _packSize = 8 + data.Length + 2;
            _pack = new byte[_packSize];
            Buffer.BlockCopy(_head, 0, _pack, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(_packSize), 0, _pack, 2, 4);
            _pack[6] = frmMain.bufMod;
            Buffer.BlockCopy(data, 0, _pack, 8, data.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(CRC16(_pack, 8, _packSize - 10)), 0, _pack, _packSize - 2, 2);

            return _pack;
        }

        private void Compress(ref byte[] input)
        {
            SevenZipCompressor compressor = new SevenZipCompressor();
            compressor.CompressionMethod = CompressionMethod.Lzma;
            compressor.CompressionLevel = CompressionLevel.Ultra;
            using (MemoryStream msin = new MemoryStream(input))
            using (MemoryStream msout = new MemoryStream())
            {
                compressor.CompressStream(msin, msout);
                msout.Position = 0;
                input = new byte[msout.Length];
                msout.Read(input, 0, input.Length);
            }
        }

        private void Uncompress(ref byte[] input)
        {
            using (MemoryStream msin = new MemoryStream())
            {
                msin.Write(input, 0, input.Length);
                msin.Position = 0;
                using (SevenZipExtractor extractor = new SevenZipExtractor(msin))
                using (MemoryStream msout = new MemoryStream())
                {
                    extractor.ExtractFile(0, msout);
                    msout.Position = 0;
                    input = new byte[msout.Length];
                    msout.Read(input, 0, input.Length);
                }
            }
        }
    }
}
