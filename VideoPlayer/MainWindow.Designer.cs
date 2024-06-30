namespace VideoPlayer
{
    partial class PlayerExampleForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PlayerExampleForm));
            tbInput = new TextBox();
            bOpen = new Button();
            tbOutput = new TextBox();
            bEncrypt = new Button();
            label1 = new Label();
            SuspendLayout();
            // 
            // tbInput
            // 
            tbInput.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbInput.BorderStyle = BorderStyle.FixedSingle;
            tbInput.Location = new Point(12, 123);
            tbInput.Name = "tbInput";
            tbInput.Size = new Size(613, 27);
            tbInput.TabIndex = 0;
            // 
            // bOpen
            // 
            bOpen.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            bOpen.Location = new Point(631, 122);
            bOpen.Name = "bOpen";
            bOpen.Size = new Size(85, 27);
            bOpen.TabIndex = 1;
            bOpen.Text = "Open File";
            bOpen.UseVisualStyleBackColor = true;
            // 
            // tbOutput
            // 
            tbOutput.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tbOutput.BorderStyle = BorderStyle.FixedSingle;
            tbOutput.Enabled = false;
            tbOutput.Location = new Point(12, 156);
            tbOutput.Name = "tbOutput";
            tbOutput.Size = new Size(613, 27);
            tbOutput.TabIndex = 0;
            // 
            // bEncrypt
            // 
            bEncrypt.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            bEncrypt.Location = new Point(631, 156);
            bEncrypt.Name = "bEncrypt";
            bEncrypt.Size = new Size(85, 27);
            bEncrypt.TabIndex = 1;
            bEncrypt.Text = "Encrypt";
            bEncrypt.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(715, 103);
            label1.TabIndex = 2;
            label1.Text = resources.GetString("label1.Text");
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(724, 200);
            Controls.Add(label1);
            Controls.Add(bEncrypt);
            Controls.Add(bOpen);
            Controls.Add(tbOutput);
            Controls.Add(tbInput);
            Name = "Form1";
            Text = "Demonstration Form";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox tbInput;
        private Button bOpen;
        private TextBox tbOutput;
        private Button bEncrypt;
        private Label label1;
    }
}
