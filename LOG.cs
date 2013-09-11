using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace SISCell
{
    class LOG
    {
        private string _path;

        public LOG()
        {
            _path = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "log\\";
            if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);
        }

        public void WrtMsg(string msg)
        {
            string filename = _path + DateTime.Now.ToString("yyyyMMdd") + ".log";

            StackTrace st = new StackTrace(true);
            MethodBase mb = st.GetFrame(1).GetMethod();

            using (StreamWriter sw = new StreamWriter(filename, true))
            {
                sw.WriteLine(string.Format("{0}\t类名:{1}\t方法名:{2}\t错误描述:{3}", DateTime.Now.ToString("HH:mm:ss"), mb.DeclaringType.Name, mb.Name, msg));
            }
        }
    }
}
