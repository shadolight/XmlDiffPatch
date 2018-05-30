using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Windows.Forms.Html;
using System.IO;
namespace VisualXmlDiff
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class Browser : System.Windows.Forms.Form
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		HtmlControl hc = new HtmlControl();
		string navigateTo = null;

		public Browser( string url )
		{
			navigateTo = url;
			InitializeComponent();

			this.Controls.Add( hc );
		}

		private Browser()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			//
			// TODO: Add any constructor code after InitializeComponent call
			//
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			// 
			// Browser
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(632, 453);
			this.Name = "Browser";
			this.Text = "Diff";
			this.Resize += new System.EventHandler(this.Browser_Resize);
			this.Load += new System.EventHandler(this.Browser_Load);
			this.Closed += new System.EventHandler(this.Browser_Closed);

		}
		#endregion

		private void Browser_Load(object sender, System.EventArgs e)
		{
			hc.Size = this.Size;
			string currPath = AppDomain.CurrentDomain.BaseDirectory ;
			hc.Navigate( "file:///" + currPath + navigateTo );
			hc.Show();
		}

		private void Browser_Closed(object sender, System.EventArgs e)
		{
			if ( File.Exists ( navigateTo ) )
			{
				File.Delete( navigateTo );
			}
		}

		private void Browser_Resize(object sender, System.EventArgs e)
		{
			hc.Size = this.Size;
		}
	}
}
