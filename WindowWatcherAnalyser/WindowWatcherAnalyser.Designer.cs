namespace WindowWatcherAnalyser
{
    partial class WindowWatcherAnalyser
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBoxGraph = new System.Windows.Forms.PictureBox();
            this.dateTimePickerCurrentDay = new System.Windows.Forms.DateTimePicker();
            this.buttonPrevDay = new System.Windows.Forms.Button();
            this.buttonNextDay = new System.Windows.Forms.Button();
            this.buttonZoomIn = new System.Windows.Forms.Button();
            this.buttonZoomOut = new System.Windows.Forms.Button();
            this.buttonScrollLeft = new System.Windows.Forms.Button();
            this.buttonScrollRight = new System.Windows.Forms.Button();
            this.buttonFullDay = new System.Windows.Forms.Button();
            this.vScrollBarGraph = new System.Windows.Forms.VScrollBar();
            this.comboBoxProcess = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxGraph)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBoxGraph
            // 
            this.pictureBoxGraph.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxGraph.BackColor = System.Drawing.Color.Black;
            this.pictureBoxGraph.Location = new System.Drawing.Point(12, 41);
            this.pictureBoxGraph.Name = "pictureBoxGraph";
            this.pictureBoxGraph.Size = new System.Drawing.Size(954, 468);
            this.pictureBoxGraph.TabIndex = 0;
            this.pictureBoxGraph.TabStop = false;
            this.pictureBoxGraph.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBoxGraph_Paint);
            this.pictureBoxGraph.SizeChanged += new System.EventHandler(this.pictureBoxGraph_SizeChanged);
            // 
            // dateTimePickerCurrentDay
            // 
            this.dateTimePickerCurrentDay.CustomFormat = "dddd, dd MMMM yyyy,  hh:mm tt";
            this.dateTimePickerCurrentDay.DropDownAlign = System.Windows.Forms.LeftRightAlignment.Right;
            this.dateTimePickerCurrentDay.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dateTimePickerCurrentDay.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePickerCurrentDay.Location = new System.Drawing.Point(96, 12);
            this.dateTimePickerCurrentDay.Name = "dateTimePickerCurrentDay";
            this.dateTimePickerCurrentDay.Size = new System.Drawing.Size(300, 22);
            this.dateTimePickerCurrentDay.TabIndex = 1;
            this.dateTimePickerCurrentDay.ValueChanged += new System.EventHandler(this.dateTimePickerCurrentDay_ValueChanged);
            // 
            // buttonPrevDay
            // 
            this.buttonPrevDay.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonPrevDay.Location = new System.Drawing.Point(12, 11);
            this.buttonPrevDay.Name = "buttonPrevDay";
            this.buttonPrevDay.Size = new System.Drawing.Size(36, 24);
            this.buttonPrevDay.TabIndex = 2;
            this.buttonPrevDay.Text = "<<";
            this.buttonPrevDay.UseVisualStyleBackColor = true;
            this.buttonPrevDay.Click += new System.EventHandler(this.buttonPrevDay_Click);
            // 
            // buttonNextDay
            // 
            this.buttonNextDay.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonNextDay.Location = new System.Drawing.Point(444, 11);
            this.buttonNextDay.Name = "buttonNextDay";
            this.buttonNextDay.Size = new System.Drawing.Size(36, 24);
            this.buttonNextDay.TabIndex = 2;
            this.buttonNextDay.Text = ">>";
            this.buttonNextDay.UseVisualStyleBackColor = true;
            this.buttonNextDay.Click += new System.EventHandler(this.buttonNextDay_Click);
            // 
            // buttonZoomIn
            // 
            this.buttonZoomIn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonZoomIn.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonZoomIn.Location = new System.Drawing.Point(843, 11);
            this.buttonZoomIn.Name = "buttonZoomIn";
            this.buttonZoomIn.Size = new System.Drawing.Size(66, 24);
            this.buttonZoomIn.TabIndex = 2;
            this.buttonZoomIn.Text = "Zoom In";
            this.buttonZoomIn.UseVisualStyleBackColor = true;
            this.buttonZoomIn.Click += new System.EventHandler(this.buttonZoomIn_Click);
            // 
            // buttonZoomOut
            // 
            this.buttonZoomOut.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonZoomOut.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonZoomOut.Location = new System.Drawing.Point(915, 11);
            this.buttonZoomOut.Name = "buttonZoomOut";
            this.buttonZoomOut.Size = new System.Drawing.Size(71, 24);
            this.buttonZoomOut.TabIndex = 2;
            this.buttonZoomOut.Text = "Zoom Out";
            this.buttonZoomOut.UseVisualStyleBackColor = true;
            this.buttonZoomOut.Click += new System.EventHandler(this.buttonZoomOut_Click);
            // 
            // buttonScrollLeft
            // 
            this.buttonScrollLeft.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonScrollLeft.Location = new System.Drawing.Point(54, 11);
            this.buttonScrollLeft.Name = "buttonScrollLeft";
            this.buttonScrollLeft.Size = new System.Drawing.Size(36, 24);
            this.buttonScrollLeft.TabIndex = 2;
            this.buttonScrollLeft.Text = "<";
            this.buttonScrollLeft.UseVisualStyleBackColor = true;
            this.buttonScrollLeft.Click += new System.EventHandler(this.buttonScrollLeft_Click);
            // 
            // buttonScrollRight
            // 
            this.buttonScrollRight.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonScrollRight.Location = new System.Drawing.Point(402, 10);
            this.buttonScrollRight.Name = "buttonScrollRight";
            this.buttonScrollRight.Size = new System.Drawing.Size(36, 24);
            this.buttonScrollRight.TabIndex = 2;
            this.buttonScrollRight.Text = ">";
            this.buttonScrollRight.UseVisualStyleBackColor = true;
            this.buttonScrollRight.Click += new System.EventHandler(this.buttonScrollRight_Click);
            // 
            // buttonFullDay
            // 
            this.buttonFullDay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFullDay.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonFullDay.Location = new System.Drawing.Point(771, 11);
            this.buttonFullDay.Name = "buttonFullDay";
            this.buttonFullDay.Size = new System.Drawing.Size(66, 24);
            this.buttonFullDay.TabIndex = 2;
            this.buttonFullDay.Text = "Full Day";
            this.buttonFullDay.UseVisualStyleBackColor = true;
            this.buttonFullDay.Click += new System.EventHandler(this.buttonFullDay_Click);
            // 
            // vScrollBarGraph
            // 
            this.vScrollBarGraph.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.vScrollBarGraph.Location = new System.Drawing.Point(969, 41);
            this.vScrollBarGraph.Maximum = 21;
            this.vScrollBarGraph.Name = "vScrollBarGraph";
            this.vScrollBarGraph.Size = new System.Drawing.Size(17, 468);
            this.vScrollBarGraph.TabIndex = 3;
            this.vScrollBarGraph.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vScrollBarGraph_Scroll);
            // 
            // comboBoxProcess
            // 
            this.comboBoxProcess.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxProcess.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxProcess.FormattingEnabled = true;
            this.comboBoxProcess.Location = new System.Drawing.Point(486, 14);
            this.comboBoxProcess.Name = "comboBoxProcess";
            this.comboBoxProcess.Size = new System.Drawing.Size(279, 21);
            this.comboBoxProcess.TabIndex = 4;
            this.comboBoxProcess.SelectedIndexChanged += new System.EventHandler(this.comboBoxProcess_SelectedIndexChanged);
            // 
            // WindowWatcherAnalyser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(998, 521);
            this.Controls.Add(this.comboBoxProcess);
            this.Controls.Add(this.vScrollBarGraph);
            this.Controls.Add(this.buttonZoomOut);
            this.Controls.Add(this.buttonFullDay);
            this.Controls.Add(this.buttonZoomIn);
            this.Controls.Add(this.buttonScrollRight);
            this.Controls.Add(this.buttonNextDay);
            this.Controls.Add(this.buttonScrollLeft);
            this.Controls.Add(this.buttonPrevDay);
            this.Controls.Add(this.dateTimePickerCurrentDay);
            this.Controls.Add(this.pictureBoxGraph);
            this.Name = "WindowWatcherAnalyser";
            this.Text = "Window Watcher Analyser";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxGraph)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBoxGraph;
        private System.Windows.Forms.DateTimePicker dateTimePickerCurrentDay;
        private System.Windows.Forms.Button buttonPrevDay;
        private System.Windows.Forms.Button buttonNextDay;
        private System.Windows.Forms.Button buttonZoomIn;
        private System.Windows.Forms.Button buttonZoomOut;
        private System.Windows.Forms.Button buttonScrollLeft;
        private System.Windows.Forms.Button buttonScrollRight;
        private System.Windows.Forms.Button buttonFullDay;
        private System.Windows.Forms.VScrollBar vScrollBarGraph;
        private System.Windows.Forms.ComboBox comboBoxProcess;
    }
}

