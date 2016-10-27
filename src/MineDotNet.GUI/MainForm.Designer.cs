﻿namespace MineDotNet.GUI
{
    partial class MainForm
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
            this.MainPictureBox = new System.Windows.Forms.PictureBox();
            this.ShowMapsButton = new System.Windows.Forms.Button();
            this.Map0Label = new System.Windows.Forms.Label();
            this.Map0TextBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.MainPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // MainPictureBox
            // 
            this.MainPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.MainPictureBox.Location = new System.Drawing.Point(0, 0);
            this.MainPictureBox.Name = "MainPictureBox";
            this.MainPictureBox.Size = new System.Drawing.Size(513, 513);
            this.MainPictureBox.TabIndex = 0;
            this.MainPictureBox.TabStop = false;
            // 
            // ShowMapsButton
            // 
            this.ShowMapsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ShowMapsButton.Location = new System.Drawing.Point(520, 484);
            this.ShowMapsButton.Name = "ShowMapsButton";
            this.ShowMapsButton.Size = new System.Drawing.Size(236, 23);
            this.ShowMapsButton.TabIndex = 3;
            this.ShowMapsButton.Text = "Show";
            this.ShowMapsButton.UseVisualStyleBackColor = true;
            this.ShowMapsButton.Click += new System.EventHandler(this.ShowMapsButton_Click);
            // 
            // Map0Label
            // 
            this.Map0Label.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Map0Label.AutoSize = true;
            this.Map0Label.Location = new System.Drawing.Point(516, 6);
            this.Map0Label.Name = "Map0Label";
            this.Map0Label.Size = new System.Drawing.Size(31, 13);
            this.Map0Label.TabIndex = 23;
            this.Map0Label.Text = "Map:";
            // 
            // Map0TextBox
            // 
            this.Map0TextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.Map0TextBox.Location = new System.Drawing.Point(519, 22);
            this.Map0TextBox.Multiline = true;
            this.Map0TextBox.Name = "Map0TextBox";
            this.Map0TextBox.Size = new System.Drawing.Size(236, 216);
            this.Map0TextBox.TabIndex = 22;
            // 
            // MainForm
            // 
            this.AcceptButton = this.ShowMapsButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(764, 514);
            this.Controls.Add(this.Map0Label);
            this.Controls.Add(this.Map0TextBox);
            this.Controls.Add(this.ShowMapsButton);
            this.Controls.Add(this.MainPictureBox);
            this.Name = "MainForm";
            this.Text = "Mine viewer";
            ((System.ComponentModel.ISupportInitialize)(this.MainPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox MainPictureBox;
        private System.Windows.Forms.Button ShowMapsButton;
        private System.Windows.Forms.Label Map0Label;
        private System.Windows.Forms.TextBox Map0TextBox;
    }
}

