namespace SISCell
{
    partial class frmMain
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            this.lstLog = new System.Windows.Forms.ListBox();
            this.cfg = new C1.Win.C1FlexGrid.C1FlexGrid();
            this.BottomToolStripPanel = new System.Windows.Forms.ToolStripPanel();
            this.TopToolStripPanel = new System.Windows.Forms.ToolStripPanel();
            this.tlsMenu = new System.Windows.Forms.ToolStrip();
            this.tslblIn = new System.Windows.Forms.ToolStripLabel();
            this.tslblOut = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.tslblCount = new System.Windows.Forms.ToolStripLabel();
            this.tsbtnPTInfo = new System.Windows.Forms.ToolStripButton();
            this.tsbtnSnap = new System.Windows.Forms.ToolStripButton();
            this.RightToolStripPanel = new System.Windows.Forms.ToolStripPanel();
            this.LeftToolStripPanel = new System.Windows.Forms.ToolStripPanel();
            this.ContentPanel = new System.Windows.Forms.ToolStripContentPanel();
            ((System.ComponentModel.ISupportInitialize)(this.cfg)).BeginInit();
            this.tlsMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // lstLog
            // 
            this.lstLog.FormattingEnabled = true;
            this.lstLog.ItemHeight = 12;
            this.lstLog.Location = new System.Drawing.Point(0, 371);
            this.lstLog.Name = "lstLog";
            this.lstLog.Size = new System.Drawing.Size(624, 76);
            this.lstLog.TabIndex = 1;
            // 
            // cfg
            // 
            this.cfg.ColumnInfo = resources.GetString("cfg.ColumnInfo");
            this.cfg.Location = new System.Drawing.Point(0, 31);
            this.cfg.Name = "cfg";
            this.cfg.Size = new System.Drawing.Size(624, 337);
            this.cfg.Styles = new C1.Win.C1FlexGrid.CellStyleCollection(resources.GetString("cfg.Styles"));
            this.cfg.TabIndex = 5;
            // 
            // BottomToolStripPanel
            // 
            this.BottomToolStripPanel.Location = new System.Drawing.Point(0, 0);
            this.BottomToolStripPanel.Name = "BottomToolStripPanel";
            this.BottomToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.BottomToolStripPanel.RowMargin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.BottomToolStripPanel.Size = new System.Drawing.Size(0, 0);
            // 
            // TopToolStripPanel
            // 
            this.TopToolStripPanel.Location = new System.Drawing.Point(0, 0);
            this.TopToolStripPanel.Name = "TopToolStripPanel";
            this.TopToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.TopToolStripPanel.RowMargin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.TopToolStripPanel.Size = new System.Drawing.Size(0, 0);
            // 
            // tlsMenu
            // 
            this.tlsMenu.BackColor = System.Drawing.SystemColors.Control;
            this.tlsMenu.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tlsMenu.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tlsMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tslblIn,
            this.tslblOut,
            this.toolStripSeparator1,
            this.tslblCount,
            this.tsbtnPTInfo,
            this.tsbtnSnap});
            this.tlsMenu.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.tlsMenu.Location = new System.Drawing.Point(0, 0);
            this.tlsMenu.Name = "tlsMenu";
            this.tlsMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.tlsMenu.ShowItemToolTips = false;
            this.tlsMenu.Size = new System.Drawing.Size(624, 28);
            this.tlsMenu.TabIndex = 6;
            // 
            // tslblIn
            // 
            this.tslblIn.AutoSize = false;
            this.tslblIn.BackColor = System.Drawing.SystemColors.Control;
            this.tslblIn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tslblIn.ForeColor = System.Drawing.SystemColors.ControlText;
            this.tslblIn.Name = "tslblIn";
            this.tslblIn.Size = new System.Drawing.Size(100, 22);
            // 
            // tslblOut
            // 
            this.tslblOut.AutoSize = false;
            this.tslblOut.BackColor = System.Drawing.SystemColors.Control;
            this.tslblOut.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tslblOut.Name = "tslblOut";
            this.tslblOut.Size = new System.Drawing.Size(100, 22);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 28);
            // 
            // tslblCount
            // 
            this.tslblCount.Name = "tslblCount";
            this.tslblCount.Size = new System.Drawing.Size(106, 25);
            this.tslblCount.Text = "测点数量未知";
            // 
            // tsbtnPTInfo
            // 
            this.tsbtnPTInfo.AutoToolTip = false;
            this.tsbtnPTInfo.BackColor = System.Drawing.Color.Gold;
            this.tsbtnPTInfo.CheckOnClick = true;
            this.tsbtnPTInfo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbtnPTInfo.Image = ((System.Drawing.Image)(resources.GetObject("tsbtnPTInfo.Image")));
            this.tsbtnPTInfo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsbtnPTInfo.Name = "tsbtnPTInfo";
            this.tsbtnPTInfo.Size = new System.Drawing.Size(78, 25);
            this.tsbtnPTInfo.Text = "测点信息";
            this.tsbtnPTInfo.TextDirection = System.Windows.Forms.ToolStripTextDirection.Horizontal;
            this.tsbtnPTInfo.Click += new System.EventHandler(this.tsbtnDetail_Click);
            // 
            // tsbtnSnap
            // 
            this.tsbtnSnap.AutoToolTip = false;
            this.tsbtnSnap.BackColor = System.Drawing.Color.Gold;
            this.tsbtnSnap.Checked = true;
            this.tsbtnSnap.CheckOnClick = true;
            this.tsbtnSnap.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tsbtnSnap.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbtnSnap.Image = ((System.Drawing.Image)(resources.GetObject("tsbtnSnap.Image")));
            this.tsbtnSnap.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsbtnSnap.Name = "tsbtnSnap";
            this.tsbtnSnap.Size = new System.Drawing.Size(46, 25);
            this.tsbtnSnap.Text = "快照";
            this.tsbtnSnap.Click += new System.EventHandler(this.tsbtnSnap_Click);
            // 
            // RightToolStripPanel
            // 
            this.RightToolStripPanel.Location = new System.Drawing.Point(0, 0);
            this.RightToolStripPanel.Name = "RightToolStripPanel";
            this.RightToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.RightToolStripPanel.RowMargin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.RightToolStripPanel.Size = new System.Drawing.Size(0, 0);
            // 
            // LeftToolStripPanel
            // 
            this.LeftToolStripPanel.Location = new System.Drawing.Point(0, 0);
            this.LeftToolStripPanel.Name = "LeftToolStripPanel";
            this.LeftToolStripPanel.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.LeftToolStripPanel.RowMargin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.LeftToolStripPanel.Size = new System.Drawing.Size(0, 0);
            // 
            // ContentPanel
            // 
            this.ContentPanel.Size = new System.Drawing.Size(150, 147);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 442);
            this.Controls.Add(this.tlsMenu);
            this.Controls.Add(this.cfg);
            this.Controls.Add(this.lstLog);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "frmMain";
            this.Text = "SISCell";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.frmMain_FormClosed);
            this.Load += new System.EventHandler(this.frmMain_Load);
            ((System.ComponentModel.ISupportInitialize)(this.cfg)).EndInit();
            this.tlsMenu.ResumeLayout(false);
            this.tlsMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox lstLog;
        private C1.Win.C1FlexGrid.C1FlexGrid cfg;
        private System.Windows.Forms.ToolStripPanel BottomToolStripPanel;
        private System.Windows.Forms.ToolStripPanel TopToolStripPanel;
        private System.Windows.Forms.ToolStrip tlsMenu;
        private System.Windows.Forms.ToolStripLabel tslblIn;
        private System.Windows.Forms.ToolStripLabel tslblOut;
        private System.Windows.Forms.ToolStripButton tsbtnPTInfo;
        private System.Windows.Forms.ToolStripButton tsbtnSnap;
        private System.Windows.Forms.ToolStripPanel RightToolStripPanel;
        private System.Windows.Forms.ToolStripPanel LeftToolStripPanel;
        private System.Windows.Forms.ToolStripContentPanel ContentPanel;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel tslblCount;
    }
}

