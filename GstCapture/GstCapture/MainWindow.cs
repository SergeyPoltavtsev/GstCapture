using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using Gtk;


public partial class MainWindow: Gtk.Window
{	
	Dictionary<String, GstCapture.cameraDevice> cameras = new Dictionary<string, GstCapture.cameraDevice>();
	static Gst.Pipeline gstRecording = null;
	static String path = null, dir = null;
	public static bool isRecording;
	public const ulong timeout = 3000000000; // 3s = 3000000000ns
	string platform;



	public MainWindow (): base (Gtk.WindowType.Toplevel)
	{

		Build ();
		PLanformIdentification ();
		switch (platform) {
			case "Win":
				MicScan ("dshowaudiosrc", "device-name");
				CameraScanWin ();
			break;
			case "Unix":
				MicScan ("pulsesrc", "device");
				CameraScanWin ();
			break;
			default:
			MessageDialog md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, "Не известна аппаратная платформа");
			break;
		
		}



		txtFolderOut.Text = "E:/Diplom/Video"; //Environment.GetFolderPath (Environment.SpecialFolder.Personal);

	}
	
	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		StopRecording();
		Gst.Application.Deinit();
		Application.Quit ();
		a.RetVal = true;
	}

	void FillCombo (Gtk.ComboBox cb)
	{
		cb.Clear();
		CellRendererText cell = new CellRendererText ();
		cb.PackStart (cell, false);
		cb.AddAttribute (cell, "text", 0);
		ListStore store = new ListStore (typeof (string));
		cb.Model = store;

	}





	private void PLanformIdentification()
	{
		OperatingSystem os = Environment.OSVersion;
		PlatformID     pid = os.Platform;
		switch (pid) 
		{
			case PlatformID.Win32NT:
			case PlatformID.Win32S:
			case PlatformID.Win32Windows:
			case PlatformID.WinCE:
			platform = "Win";
			break;
			case PlatformID.Unix:
			platform = "Unix";
			break;
			default:
			platform = "InvalidPlatform";
			break;
		}

	}

	private void MicScan(string source, string devprop )
	{
		Gst.Element e = Gst.ElementFactory.Make(source);
		Gst.Interfaces.PropertyProbeAdapter ppa = new Gst.Interfaces.PropertyProbeAdapter(e.Handle);

		object[] devices = ppa.ProbeAndGetValues(devprop);

		foreach (object dev in devices)
		{
			FillCombo (cbxMic);
			cbxMic.AppendText (String.Format ("{0}", dev.ToString ()));
		}


	}


	private void CameraScanWin()
	{

		Gst.Element e = Gst.ElementFactory.Make ("dshowvideosrc");
		Gst.Interfaces.PropertyProbeAdapter ppa = new Gst.Interfaces.PropertyProbeAdapter (e.Handle);

		object[] devices = ppa.ProbeAndGetValues ("device-name");

		FillCombo (cbxCamera);

		foreach (String dev in devices) {
			Gst.Caps caps = e.GetStaticPad ("src").Caps;

			foreach (Gst.Structure cap in caps) {
				if (cap.HasField ("width") && cap.HasField ("height")) {
					Gst.GLib.Value s;
					int height = 0, width = 0;

					s = cap.GetValue ("width");
					if (s.Val is int) {
						width = (int)s.Val;
					}
					s = cap.GetValue ("height");
					if (s.Val is int) {
						height = (int)s.Val;
					}

					if (width == 320 && height == 240) {
						s = cap.GetValue ("framerate");

						Gst.Fraction f;
						if (s.Val is Gst.FractionRange) {
							f = new Gst.FractionRange (s).Max;  // ? Min
						} else if (s.Val is Gst.Fraction) {
							f = new Gst.Fraction (s);
						} else if (s.Val is int) {
							f = new Gst.Fraction ((int)s.Val, 1);
						} else
							continue;

						cameras [dev] = new GstCapture.cameraDevice (dev, f, width, height);

						cbxCamera.AppendText (dev);	


					}
				}
			}
		}
	}


	private void CameraScanUnix ()
	{

		Gst.Element e = Gst.ElementFactory.Make ("v4l2src");
		Gst.Interfaces.PropertyProbeAdapter ppa = new Gst.Interfaces.PropertyProbeAdapter (e.Handle);

		object[] devices = ppa.ProbeAndGetValues ("device");

		FillCombo (cbxCamera);

		foreach (String dev in devices) {

			Gst.Fraction f = new Gst.Fraction(30,1);

			cameras [dev] = new GstCapture.cameraDevice (dev, f , 320 , 240);

			cbxCamera.AppendText (dev);	


		}
	}

	protected void btnRec_Clicked (object sender, EventArgs e)
	{
		if (!isRecording)
		{
			if (!ckbxMic.Active) {
				MessageDialog md = new MessageDialog (null, DialogFlags.Modal, MessageType.Info, ButtonsType.YesNo, "Микрофон отключен в настройках. Записывать без звука?");
				ResponseType result = (ResponseType)md.Run ();

				if (result == ResponseType.Yes) {
					md.Destroy ();

					switch (platform) {
						case "Win":
						StartRecordingWin ();
						break;
						case "Unix":
						StartRecordingUnix ();
						break;


					}

				} else
					md.Destroy ();
			} 
			else 
			{
				switch (platform) {
					case "Win":
					StartRecordingWin ();
					break;
					case "Unix":
					StartRecordingUnix ();
					break;
				}
			}
		}
		else
		{
			StopRecording();
			btnRec.Label = "Rec";
			lblStatus.Text = "Stop record";
		}


	}

	public void StartRecordingWin()
	{
		if (txtFolderOut.Text != String.Empty && Directory.Exists(txtFolderOut.Text.Trim()))
		{
			String _path = txtFolderOut.Text.Trim();

			DateTime dt = DateTime.Now;

			Encoding w = Encoding.GetEncoding("windows-1251"); // HACK

			String aargs = null,
			cargs = null,
			sargs = null;

			if ((cbxMic.ActiveText != null) && ckbxMic.Active)
			{
				String aDev = cbxMic.ActiveText;
				//aargs = String.Format(" dshowaudiosrc device-name=\"{0}\"", w.GetString(Encoding.UTF8.GetBytes(aDev)));
				aargs = String.Format(" directsoundsrc");
				//aargs += " ! audio/x-raw-int, rate = 44100, channels = 1, depth = 16 ! queue ! faac ! tee name = audio";
				aargs += " ! audio/x-raw-int, rate = 44100, channels = 1, depth = 16 ! queue ! ffenc_adpcm_swf ! tee name = audio";
			}

			dir = String.Format("{0:yyyy-MM-dd_HH-mm-ss}", dt);
			path = Directory.CreateDirectory(
				System.IO.Path.Combine(_path, dir)
				).FullName;

			Directory.SetCurrentDirectory(path);
			Environment.SetEnvironmentVariable("GST_DEBUG", "3");

			if (cbxCamera.Active != -1 && ckbxCamera.Active)
			{
				GstCapture.cameraDevice cDev = cameras[cbxCamera.ActiveText];
				int gop = 450;

				cargs = " flvmux name=camera ! filesink location=\"camera.flv\"";

				cargs += String.Format(" dshowvideosrc device-name=\"{0}\"", w.GetString(Encoding.UTF8.GetBytes(cDev.device)));
				cargs += String.Format(" ! video/x-raw-yuv, framerate = {0}/{1}, width={2}, height={3}" +
				                       " ! videorate ! video/x-raw-yuv, framerate = {4}/1" +
				                       " ! queue ! ffmpegcolorspace" +
				                       " ! queue ! ffenc_flv gop-size = {5} ! camera.",
				                       cDev.framerate.Numerator, cDev.framerate.Denominator, cDev.width, cDev.height,
				                       15, gop);
				if (aargs != null)
				{
					//cargs += " audio. ! queue ! audio/mpeg ! camera.";
					cargs += " audio. ! queue ! audio/x-adpcm ! camera.";
				}

			}

			if (ckbxDisplay.Active)//sDev != null)
			{
				int gop = 150;
				sargs = " flvmux name=\"screen\" ! filesink location=\"screen.flv\"";

				sargs += " gdiscreencapsrc cursor = true";
				sargs += String.Format(" ! video/x-raw-rgb, framerate = {0}/1" +
				                       " ! ffmpegcolorspace" +
				                       " ! ffenc_flashsv gop-size = {1} ! screen.",
				                       5, gop);

				if (aargs != null)
				{
					// sargs += " audio. ! queue ! audio/mpeg ! screen.";
					sargs += " audio. ! queue ! audio/x-adpcm ! screen.";
				}

			}

			try
			{
				string final = cargs + sargs + aargs;
				gstRecording = (Gst.Pipeline)Gst.Parse.Launch(cargs + sargs + aargs);
				lblStatus.Text = "Recording ...";
				btnRec.Label = "Stop";
			
			}
			catch (Exception error)
			{
				GstCapture.MessageBox.Show(String.Format("{0}: {1}", error.Message, cargs + sargs + aargs));
			}

			if (gstRecording != null)
			{
				gstRecording.SetState(Gst.State.Playing);

				Gst.State s;
				gstRecording.GetState(out s, timeout);

				isRecording = true;

			}

			Directory.SetCurrentDirectory(_path);
		}
	}


	public void StartRecordingUnix()
	{
		if (txtFolderOut.Text != String.Empty && Directory.Exists(txtFolderOut.Text.Trim()))
		{
			GstCapture.cameraDevice cDev = cameras[cbxCamera.ActiveText];
			//String sDev = "yes";
			String aDev = cbxMic.ActiveText;
			String _path = txtFolderOut.Text.Trim();

			DateTime dt = DateTime.Now;

			//Encoding w = Encoding.GetEncoding("windows-1251"); // HACK

			String aargs = null,
			cargs = null,
			sargs = null;


			if ((aDev != null) && ckbxMic.Active)
			{


				//aargs = String.Format(" dshowaudiosrc device-name=\"{0}\"", w.GetString(Encoding.UTF8.GetBytes(aDev)));
				aargs = String.Format(" pulsesrc");
				//aargs += " ! audio/x-raw-int, rate = 44100, channels = 1, depth = 16 ! queue ! faac ! tee name = audio";
				aargs += " ! audio/x-raw-int, rate = 44100, channels = 1, depth = 16 ! queue ! ffenc_adpcm_swf ! tee name = audio";

			}

			dir = String.Format("{0:yyyy-MM-dd_HH-mm-ss}", dt);
			path = Directory.CreateDirectory(
				System.IO.Path.Combine(_path, dir)
				).FullName;

			Directory.SetCurrentDirectory(path);
			Environment.SetEnvironmentVariable("GST_DEBUG", "3");

			if (cDev != null && ckbxCamera.Active)
			{
				int gop = 450;

				cargs = " flvmux name=camera ! filesink location=\"camera.flv\"";

				cargs += String.Format(" v4l2src device =\"{0}\"", cDev.device.ToString());
				cargs += String.Format(" ! video/x-raw-yuv, framerate = {0}/{1}, width={2}, height={3}" +
				                       " ! videorate ! video/x-raw-yuv, framerate = {4}/1" +
				                       " ! queue ! ffenc_flv name = cv gop-size = {5} ! camera.",
				                       cDev.framerate.Numerator, cDev.framerate.Denominator, cDev.width, cDev.height,
				                       30, gop);
				if (aargs != null)
				{
					//cargs += " audio. ! queue ! audio/mpeg ! camera.";
					cargs += " audio. ! queue ! audio/x-adpcm ! camera.";
				}

			}

			if (ckbxDisplay.Active)//sDev != null)
			{
				int gop = 150;
				sargs = " flvmux name=\"screen\" ! filesink location=\"screen.flv\"";

				sargs += " ximagesrc use-damage=false";
				sargs += String.Format(" ! video/x-raw-rgb, framerate = {0}/1" +
				                       " ! ffmpegcolorspace ! queue " +
				                       " ! ffenc_flashsv name=sv gop-size = {1} ! screen.",
				                       5, gop);


				if (aargs != null)
				{
					// sargs += " audio. ! queue ! audio/mpeg ! screen.";
					sargs += " audio. ! queue ! audio/x-adpcm ! screen.";
				}

			}

			try
			{
				String final =  cargs + sargs + aargs;
				gstRecording = (Gst.Pipeline)Gst.Parse.Launch(final);

				lblStatus.Text = "Recording";

			}
			catch (Exception error)
			{
				GstCapture.MessageBox.Show(String.Format("{0}: {1}", error.Message, cargs + sargs + aargs));
			}

			if (gstRecording != null)
			{
				gstRecording.SetState(Gst.State.Playing);

				Gst.State s;
				gstRecording.GetState(out s, timeout);

				isRecording = true;

			}

			Directory.SetCurrentDirectory(_path);
		}
	}

	public static void StopRecording()
	{
		if (gstRecording != null)
		{
			gstRecording.SetState(Gst.State.Null);

			Gst.State s;
			gstRecording.GetState(out s, timeout);

			gstRecording.Dispose();
			gstRecording = null;
		}


		isRecording = false;

	}

	protected void btnPause_Clicked (object sender, EventArgs e)
	{
		lblStatus.Text = "pause";
	}

	protected void btnFolderOut_Click (object sender, EventArgs e)
	{
		FileChooserDialog dialog = new FileChooserDialog ("Укажите каталог для сохранения записываемого видео", this, FileChooserAction.SelectFolder, new object[] { "Cancel", ResponseType.Cancel, "Select", ResponseType.Accept });
		dialog.SetCurrentFolder (Environment.GetFolderPath (Environment.SpecialFolder.MyComputer));
		if (dialog.Run () == (int) ResponseType.Accept) 
		{
			txtFolderOut.Text = dialog.Filename;
		}
		dialog.Destroy ();

	}


}



