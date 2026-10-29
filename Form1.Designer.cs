namespace 期中報告
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            importButton = new Button();
            historyUrlTextBox = new TextBox();
            recordsGridView = new DataGridView();
            poolComboBox = new ComboBox();
            exportButton = new Button();
            ((System.ComponentModel.ISupportInitialize)recordsGridView).BeginInit();
            SuspendLayout();
            // 
            // importButton
            // 
            importButton.Location = new Point(12, 12);
            importButton.Name = "importButton";
            importButton.Size = new Size(94, 29);
            importButton.TabIndex = 0;
            importButton.Text = "import";
            importButton.UseVisualStyleBackColor = true;
            importButton.Click += importButton_Click;
            // 
            // historyUrlTextBox
            // 
            historyUrlTextBox.Location = new Point(12, 47);
            historyUrlTextBox.Name = "historyUrlTextBox";
            historyUrlTextBox.Size = new Size(697, 27);
            historyUrlTextBox.TabIndex = 1;
            // 
            // recordsGridView
            // 
            recordsGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            recordsGridView.Location = new Point(12, 96);
            recordsGridView.Name = "recordsGridView";
            recordsGridView.RowHeadersWidth = 51;
            recordsGridView.Size = new Size(776, 342);
            recordsGridView.TabIndex = 2;
            // 
            // poolComboBox
            // 
            poolComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            poolComboBox.FormattingEnabled = true;
            poolComboBox.Location = new Point(112, 14);
            poolComboBox.Name = "poolComboBox";
            poolComboBox.Size = new Size(151, 27);
            poolComboBox.TabIndex = 3;
            // 
            // exportButton
            // 
            exportButton.Location = new Point(694, 14);
            exportButton.Name = "exportButton";
            exportButton.Size = new Size(94, 29);
            exportButton.TabIndex = 4;
            exportButton.Text = "匯出 CSV";
            exportButton.UseVisualStyleBackColor = true;
            exportButton.Click += exportButton_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(exportButton);
            Controls.Add(poolComboBox);
            Controls.Add(recordsGridView);
            Controls.Add(historyUrlTextBox);
            Controls.Add(importButton);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)recordsGridView).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button importButton;
        private TextBox historyUrlTextBox;
        private DataGridView recordsGridView;
        private ComboBox poolComboBox;
        private Button exportButton;
    }
}
