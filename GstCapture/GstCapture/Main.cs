using System;
using Gtk;
using Gst;

namespace GstCapture
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Gst.Application.Init ();
			Gtk.Application.Init ();
			MainWindow win = new MainWindow ();
			win.Show ();
			Gtk.Application.Run ();
		}


	}

	public class cameraDevice
	{
		public String device;
		public Gst.Fraction framerate;
		public int width;
		public int height;

		public cameraDevice(String device, Gst.Fraction framerate, int width, int height)
		{
			this.device = device;
			this.framerate = framerate;
			this.width = width;
			this.height = height;
		}
	}

	public static class MessageBox 
	{ 
		public static void Show(Gtk.Window parent_window, DialogFlags flags, Gtk.MessageType msgtype, ButtonsType btntype, string msg)
		{ 
			MessageDialog md = new MessageDialog (parent_window, flags, msgtype, btntype, msg); 
			md.Run (); 
			md.Destroy(); 
		} 
		public static void Show(string msg)
		{ 
			MessageDialog md = new MessageDialog (null, DialogFlags.Modal, Gtk.MessageType.Info, ButtonsType.YesNo, msg);
			md.Run ();
			md.Destroy();
		}
	} 



}
