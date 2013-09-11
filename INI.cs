using System.Runtime.InteropServices;
using System.Text;

namespace SISCell
{
    class INI
    {
        #region
        //读取键的整型值
        [DllImport("kernel32", EntryPoint = "GetPrivateProfileIntW", CharSet = CharSet.Unicode)]
        private static extern int getKeyIntValue(string section, string Key, int nDefault, string filename);

        //读取字符串键值
        [DllImport("kernel32", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode)]
        private static extern int getKeyValue(string section, string key, int lpDefault, [MarshalAs(UnmanagedType.LPWStr)] string szValue, int nlen, string filename);

        //写字符串键值
        [DllImport("kernel32", EntryPoint = "WritePrivateProfileStringW", CharSet = CharSet.Unicode)]
        private static extern bool setKeyValue(string section, string key, string szValue, string filename);
        #endregion

        private string m_Path = null;		//ini文件路径

        /// ini文件路径
        public string Path
        {
            set { m_Path = value; }
            get { return m_Path; }
        }

        public INI(string szPath)
        {
            m_Path = szPath;
        }

        /// 读整型键值
        public int GetInt(string section, string key)
        {
            return getKeyIntValue(section, key, -1, m_Path);
        }

        /// 读字符串键值
        public string GetVal(string section, string key)
        {
            string szBuffer = new string('0', 256);
            int nlen = getKeyValue(section, key, 0, szBuffer, 256, m_Path);
            return szBuffer.Substring(0, nlen);
        }

        /// 写整型键值
        public bool SetInt(string section, string key, int dwValue)
        {
            return setKeyValue(section, key, dwValue.ToString(), m_Path);
        }

        /// 写字符串键值
        public bool SetVal(string section, string key, string szValue)
        {
            return setKeyValue(section, key, szValue, m_Path);
        }
    }
}
