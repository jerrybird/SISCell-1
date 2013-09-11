using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace SISCell
{
    class I_FTP : iProtocol
    {
        private INI cfg;
        private LOG err;

        FtpWebRequest reqFTP;
        string _server;//服务器ip地址
        string _user;//用户名
        string _password;//密码
        //string enconding;
        string[] ftpPath;
        string[] localPath;
        string[] nameModel;
        string[] fileName;
        int[] period;
        string[] lastDT;
        string[][][] unit;
        int[] fno, rno, cno;
        bool flag;

        public I_FTP()
        {
            cfg = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "config\\ftp.ini");
            err = new LOG();
            _server = cfg.GetVal("Connect", "Server");
            _user = cfg.GetVal("Connect", "User");
            _password = cfg.GetVal("Connect", "Password");

            ftpPath = cfg.GetVal("Data", "FtpPath").Split(';');
            localPath = new string[ftpPath.Length];
            unit = new string[ftpPath.Length][][];
            nameModel = cfg.GetVal("Data", "FileName").Split(';');
            fileName = new string[nameModel.Length];
            string[] tmpperiod = cfg.GetVal("Data", "Period").Split(';');
            period = new int[tmpperiod.Length];
            lastDT = cfg.GetVal("Data", "LastTime").Split(';');

            for (int i = 0; i < ftpPath.Length; ++i)
            {
                localPath[i] = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + ftpPath[i].Replace('/', '\\');
                if (!Directory.Exists(localPath[i])) Directory.CreateDirectory(localPath[i]);
                FileInfo[] filelist = new DirectoryInfo(localPath[i]).GetFiles();
                if (0 != filelist.Length)
                {
                    fileName[i] = filelist[i].Name;
                    GetFileData(i);
                }
            }

            for (int i = 0; i < tmpperiod.Length; ++i)
            {                
                string[] tmp = tmpperiod[i].Split('_');
                switch (tmp[0])
                {
                    case "d":
                        period[i] = 1440 * int.Parse(tmp[1]);
                        break;
                    case "m":
                        int.TryParse(tmp[1], out period[i]);
                        break;
                }             
            }

            flag = true;
        }

        #region iProtocol 成员

        public bool Connected
        {
            get { return true; }
        }

        public bool Connect()
        {
            return true;
        }

        public void DisConnect()
        {

        }

        public void InitPt(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            fno = new int[nNum + sNum];
            rno = new int[nNum + sNum];
            cno = new int[nNum + sNum];

            string[] tmp;
            foreach (numInf nr in nrst)
            {
                tmp = nr.srcId.Split('_');
                int.TryParse(tmp[0], out fno[nr.sn]);
                int.TryParse(tmp[1], out rno[nr.sn]);
                int.TryParse(tmp[2], out cno[nr.sn]);
            }
            foreach (strInf sr in srst)
            {
                tmp = sr.srcId.Split('_');
                int.TryParse(tmp[0], out fno[sr.sn]);
                int.TryParse(tmp[1], out rno[sr.sn]);
                int.TryParse(tmp[2], out cno[sr.sn]);
            }

        }

        public bool GetRtValue(int nNum, numInf[] nrst, int sNum, strInf[] srst)
        {
            if (0 == DateTime.Now.Second % 20 && flag)
            {
                for (int i = 0; i < fileName.Length; ++i)
                {
                    if (DownLoad(i))
                    {
                        GetFileData(i);
                        cfg.SetVal("Data", "LastTime", string.Join(";", lastDT));
                    }
                }
                try
                {
                    for (int i = 0; i < nrst.Length; ++i)
                    {
                        int idx = nrst[i].sn;
                        float.TryParse(unit[fno[idx]][rno[idx]][cno[idx]], out nrst[i].val);
                        nrst[i].dtm=DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    err.WrtMsg(ex.Message);
                }
                flag = false;
                return true;
            }
            else
            {
                flag = (0 != DateTime.Now.Second % 20);
                return false;
            }
        }

        #endregion

        private bool DownLoad(int idx)
        {
            DateTime dt = DateTime.Parse(lastDT[idx]).AddMinutes(period[idx]);
            fileName[idx] = nameModel[idx].Replace("yyyy", dt.Year.ToString()).Replace("MM", dt.Month.ToString("D2")).Replace("dd", dt.Day.ToString("D2")).Replace("HH", dt.Hour.ToString("D2")).Replace("mm", dt.Minute.ToString("D2"));
            string fname = Encoding.GetEncoding("GB2312").GetString(Encoding.UTF8.GetBytes(fileName[idx]));
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri("ftp://" + _server + "/" + ftpPath[idx] + fname));
            reqFTP.Credentials = new NetworkCredential(_user, _password);
            reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;
            try
            {
                FtpWebResponse ftpResponse = (FtpWebResponse)reqFTP.GetResponse();

                Stream stream = ftpResponse.GetResponseStream();
                StreamReader reader = new StreamReader(stream, Encoding.Default);
                StreamWriter writer = new StreamWriter(localPath[idx] + fileName[idx], false);
                writer.Write(reader.ReadToEnd());

                stream.Close();
                reader.Close();
                writer.Close();

                lastDT[idx] = dt.ToString();
                return true;
            }
            catch (Exception ex)
            {
                err.WrtMsg(ex.Message + "filename:" + fileName[idx]);
                return false;
            }
        }

        private void GetFileData(int idx)
        {
            try
            {
                string strtmp = File.ReadAllText(localPath[idx] + fileName[idx], Encoding.UTF8);
                string[] strline = strtmp.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                unit[idx] = new string[strline.Length][];
                int i = 0;
                foreach (string str in strline)
                {
                    unit[idx][i++] = str.Split('\t');
                }
            }
            catch (Exception ex)
            { err.WrtMsg(ex.Message); }
        }
    }
}
