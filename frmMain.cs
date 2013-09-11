using System;
using System.Collections;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Text;

namespace SISCell
{
    //数值型结构体
    public struct numInf
    {
        public int sn;
        public string srcId;
        public string dstId;
        public string desc;
        public float val;
        public DateTime dtm;
        public string ratio;
        public int datatype;
    }

    //字符串型结构体
    public struct strInf
    {
        public int sn;
        public string srcId;
        public string dstId;
        public string desc;
        public string val;
        public DateTime dtm;
    }

    public partial class frmMain : Form
    {
        System.Threading.Timer thIO = null;
        System.Threading.Timer thCheck = null;
        bool work_IO = false;
        bool work_Check = false;

        delegate void AddMsgCallback(string msg);
        delegate void UpdateCFGCallback();
        delegate void ColorLBLCallback(ToolStripLabel lbl, Color color);
        AddMsgCallback am;
        UpdateCFGCallback uc;
        ColorLBLCallback cl;

        int periodrt, periodhst;
        bool snapOn;
        public static byte bufMod = 0;

        int numNum = 0, strNum = 0;
        numInf[] numRecord;
        strInf[] strRecord;
        iProtocol inBrg;
        oProtocol outBrg;

        BUFFER cbuffer = new BUFFER();
        PACK cpack = new PACK();

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            //读测点配置类型
            INI cfgMain = new INI(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "main.ini");
            this.Text = cfgMain.GetVal("Display", "Title");
            string tagInfoType = cfgMain.GetVal("TagInfo", "Type");
            AddMsg(string.Format("测点配置类型为{0}.", tagInfoType));

            //读测点配置
            if ("CSV" == tagInfoType)
            {
                ArrayList numdtl = new ArrayList();
                ArrayList strdtl = new ArrayList();
                using (StreamReader sr = new StreamReader(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "tag.csv", Encoding.Default))
                {
                    string strline;
                    while (null != (strline = sr.ReadLine()))
                    {
                        string[] strfield = strline.Split(',');
                        if (99 != int.Parse(strfield[4])) numdtl.Add(strfield);
                        else strdtl.Add(strfield);
                    }
                }

                numNum = numdtl.Count;
                strNum = strdtl.Count;

                numRecord = new numInf[numNum];
                int idx = 0;
                foreach (string[] temp in numdtl)
                {
                    int.TryParse(temp[0], out numRecord[idx].sn);
                    numRecord[idx].srcId = temp[1];
                    numRecord[idx].dstId = temp[2];
                    numRecord[idx].desc = temp[3];
                    int.TryParse(temp[4], out numRecord[idx].datatype);
                    numRecord[idx++].ratio = temp[5];
                }

                strRecord = new strInf[strNum];
                idx = 0;
                foreach (string[] temp in strdtl)
                {
                    int.TryParse(temp[0], out strRecord[idx].sn);
                    strRecord[idx].srcId = temp[1];
                    strRecord[idx].dstId = temp[2];
                    strRecord[idx++].desc = temp[3];
                }
            }
            else
            {
                int.TryParse(cfgMain.GetVal("TagInfo", "NumCount"), out numNum);
                numRecord = new numInf[numNum];
                int.TryParse(cfgMain.GetVal("TagInfo", "StrCount"), out strNum);
                strRecord = new strInf[strNum];
            }

            string inputType = cfgMain.GetVal("IO", "Input");
            tslblIn.Text = inputType;
            Factory inProtocol = new Factory();
            inBrg = inProtocol.MakeInput(inputType);
            if (inBrg.Connect())
            {
                inBrg.InitPt(numNum, numRecord, strNum, strRecord);
                tslblIn.BackColor = Color.Green;
            }
            else
            {
                tslblIn.BackColor = Color.Red;
            }

            string outputType = cfgMain.GetVal("IO", "Output");
            tslblOut.Text = outputType;
            Factory outProtocol = new Factory();
            outBrg = outProtocol.MakeOutput(outputType);
            if (outBrg.Connect())
            {
                outBrg.InitPt(numNum, numRecord, strNum, strRecord);
                tslblOut.BackColor = Color.Green;
            }
            else
            {
                tslblOut.BackColor = Color.Red;
            }

            tslblCount.Text = string.Format("N:{0}_S:{1}", numNum.ToString(), strNum.ToString());
            AddMsg(String.Format("成功获取测点信息,数值点{0}个,字符串点{1}个.", numNum, strNum));

            cfg.Redraw = false;
            cfg.Rows.Count = strNum + numNum + 1;
            foreach (numInf nR in numRecord)
            {
                cfg[nR.sn + 1, 0] = nR.sn;
                cfg[nR.sn + 1, 1] = nR.srcId;
                cfg[nR.sn + 1, 2] = nR.dstId;
                cfg[nR.sn + 1, 3] = nR.desc;
            }

            foreach (strInf sR in strRecord)
            {
                cfg[sR.sn + 1, 0] = sR.sn;
                cfg[sR.sn + 1, 1] = sR.srcId;
                cfg[sR.sn + 1, 2] = sR.dstId;
                cfg[sR.sn + 1, 3] = sR.desc;
            }
            cfg.AutoSizeCol(0);
            cfg.AutoSizeCol(1);
            cfg.AutoSizeCol(2);
            cfg.AutoSizeCol(3);
            cfg.Cols[4].Width = (cfg.Width - cfg.Cols[0].Width - cfg.Cols[1].Width - cfg.Cols[2].Width - cfg.Cols[3].Width) / 2;
            cfg.Cols[5].Width = cfg.Cols[4].Width;
            cfg.Redraw = true;

            int.TryParse(cfgMain.GetVal("IO", "PeriodRT"), out periodrt);
            if (0 == periodrt) periodrt = 1;
            int.TryParse(cfgMain.GetVal("IO", "PeriodHST"), out periodhst);

            DateTime now = DateTime.Now;
            int idue = 1000 - now.Millisecond;
            thIO = new System.Threading.Timer(new TimerCallback(ThreadIO), null, idue, 1000);

            now = DateTime.Now;
            idue = 1000 - now.Millisecond;
            thCheck = new System.Threading.Timer(new TimerCallback(ThreadCheck), null, idue, 1000);

            am = new AddMsgCallback(AddMsg);
            uc = new UpdateCFGCallback(UpdateCFG);
            cl = new ColorLBLCallback(ColorLBL);
        }

        public void ThreadIO(Object state)
        {
            if (work_IO) return;

            work_IO = true;

            //读实时
            int sec = DateTime.Now.Second;
            if (0 == sec % periodrt && inBrg.Connected)
            {
                

                if (inBrg.GetRtValue(numNum, numRecord, strNum, strRecord))
                {
                    if (0 == bufMod) bufMod = (byte)(0 == DateTime.Now.Second % periodhst ? 3 : 1);

                    this.BeginInvoke(am, new object[] { string.Format("读数据成功,{0}测点更新.", numNum + strNum) });
                    if (!tsbtnPTInfo.Checked && !tsbtnSnap.Checked) this.BeginInvoke(uc);

                    if (outBrg.Connected)
                    {
                        outBrg.SetRtValue(numNum, numRecord, strNum, strRecord);
                        this.BeginInvoke(am, new object[] { "数据已发送." });
                    }
                    else
                    {
                        byte[] buf = cpack.PutData(numNum, numRecord, strNum, strRecord);
                        cbuffer.PutData(buf);
                        this.BeginInvoke(am, new object[] { "数据写入缓存." });
                    }
                    bufMod = 0;
                }
            }

            //补缓存
            if (outBrg.Connected)
            {               
                byte[] tmpbyt = cbuffer.GetData();
                if (null != tmpbyt && 0 == cpack.Verify(tmpbyt, tmpbyt.Length))
                {
                    cpack.GetData(numNum, numRecord, strNum, strRecord);
                    outBrg.SetRtValue(numNum, numRecord, strNum, strRecord);
                    this.BeginInvoke(am, new object[] { "缓存已发送." });
                }
            }

            work_IO = false;
        }

        public void ThreadCheck(Object state)
        {
            if (work_Check) return;

            work_Check = true;

            if (inBrg.Connected)
            {
                this.BeginInvoke(cl, new object[] { tslblIn, Color.Green });
            }
            else
            {
                this.BeginInvoke(cl, new object[] { tslblIn, Color.Red });
                try { inBrg.Connect(); }
                catch { }
            }

            if (outBrg.Connected)
            {
                this.BeginInvoke(cl, new object[] { tslblOut, Color.Green });
            }
            else
            {
                this.BeginInvoke(cl, new object[] { tslblOut, Color.Red });
                outBrg.Connect();
            }

            work_Check = false;
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            inBrg.DisConnect();
            outBrg.DisConnect();
        }

        private void tsbtnDetail_Click(object sender, EventArgs e)
        {
            if (tsbtnPTInfo.Checked)
            {
                snapOn = false;
                this.Size = new Size(640, this.Size.Height - 337);
                tsbtnSnap.Enabled = false;
                cfg.Visible = false;
                lstLog.Location = new Point(0, 34);
            }
            else
            {
                snapOn = !tsbtnSnap.Checked;
                this.Size = new Size(640, this.Size.Height + 337);
                tsbtnSnap.Enabled = true;
                cfg.Visible = true;
                lstLog.Location = new Point(0, 371);
            }
        }

        private void tsbtnSnap_Click(object sender, EventArgs e)
        {
            snapOn = !tsbtnSnap.Checked;
        }

        private void UpdateCFG()
        {
            cfg.Redraw = false;
            foreach (numInf nR in numRecord)
            {
                cfg[nR.sn + 1, 4] = nR.val.ToString();
                cfg[nR.sn + 1, 5] = nR.dtm;
            }
            foreach (strInf sR in strRecord)
            {
                cfg[sR.sn + 1, 4] = sR.val;
                cfg[sR.sn + 1, 5] = sR.dtm;
            }
            cfg.AutoSizeCol(4);
            cfg.AutoSizeCol(5);
            cfg.Redraw = true;
        }

        private void ColorLBL(ToolStripLabel lbl, Color color)
        {
            lbl.BackColor = color;
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = (DialogResult.Cancel == MessageBox.Show("确定关闭程序吗？", "关闭程序", MessageBoxButtons.OKCancel, MessageBoxIcon.Question));
        }

        private void AddMsg(string msg)
        {
            if (4096 <= lstLog.Items.Count)
            {
                while (1024 <= lstLog.Items.Count)
                {
                    lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
                }
            }
            lstLog.Items.Insert(0, string.Format("[{0}]\t{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), msg));
        }
    }
}
