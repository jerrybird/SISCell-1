using System;
using System.Collections.Generic;
using System.IO;

namespace SISCell
{
    class BUFFER
    {
        private string _path;
        private FileInfo[] fget;
        private FileInfo[] fput;
        private int iget = 0;
        private int iput = 0;

        public BUFFER()
        {
            _path = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "buffer\\";
        }

        public bool IsEmpty(ref FileInfo[] flst)
        {
            if (!Directory.Exists(_path)) return true;
            flst = new DirectoryInfo(_path).GetFiles();
            return (0 == flst.Length);
        }

        public byte[] GetData()
        {
            
            if (0 == iget || fget.Length == iget)
            {
                iget = 0;
                if (IsEmpty(ref fget)) return null;
            }

            byte[] rst = File.ReadAllBytes(fget[iget].FullName);
            fget[iget++].Delete();
            return rst;

        }

        public void PutData(byte[] data)
        {
            if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);

            if (IsEmpty(ref fput))
            {
                iput = 0;
            }
            else
            {
                if (0 == iput)
                {
                    string tmp = fput[fput.Length - 1].Name;
                    iput = int.Parse(tmp.Substring(0, 6)) + 1;
                }
            }

            string fname = iput++.ToString("D6") + ".bin";
            File.WriteAllBytes(_path + fname, data);
        }
    }
}
