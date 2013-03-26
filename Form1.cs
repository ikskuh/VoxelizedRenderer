using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using OCL;
using System.IO;
using System.Runtime.InteropServices;

namespace VoxelizedRenderer
{
	[StructLayout(LayoutKind.Explicit)]
	public struct Camera
	{
		[FieldOffset(0)]
		public Float3 Position;

		[FieldOffset(16)]
		public Float3 Direction;

		[FieldOffset(32)]
		public Float3 Right;

		[FieldOffset(48)]
		public Float3 top;

		[FieldOffset(64)]
		public float FocalLength;

		[FieldOffset(68)]
		public float Aspect;

		public void Update()
		{
			this.Right = Float3.Cross(this.Direction, Float3.UnitY);
			this.Right.Normalize();
			this.top = Float3.Cross(this.Direction, Right);
			this.top.Normalize();
		}
	}

	public partial class Form1 : Form
	{
		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(Keys vKey);


		byte[] bitmapBuffer;
		Camera camera;
		Memory cameraBuffer;
		Context context;
		Device device;
		Kernel kernel;
		CommandQueue queue;
		Bitmap renderTarget;
		Memory target;
		Memory world;

		int[] worldData;
		int sizeX = 256;
		int sizeY = 128;
		int sizeZ = 256;

		public Form1()
		{
			InitializeComponent();
			this.ClientSize = new Size(800, 600);
			this.DoubleBuffered = true;
		}

		void setWorld(int x, int y, int z, Byte4 value)
		{
			setWorld(x, y, z, BitConverter.ToInt32(value.GetBytes(), 0));
		}

		void setWorld(int x, int y, int z, int value)
		{
			int offset = sizeX * sizeY * z + sizeX * y + x;
			worldData[offset] = value;
		}

		Byte4 getWorld(int x, int y, int z)
		{
			int offset = sizeX * sizeY * z + sizeX * y + x;
			return new Byte4(worldData[offset]);
		}

		protected override void OnLoad(EventArgs e)
		{
			foreach (var platform in OpenCL.GetPlatforms())
			{
				var devices = platform.GetDevices(DeviceType.GPU);
				if (devices.Length > 0)
					this.device = devices[0];
			}
			context = Context.Create(device);
			queue = context.CreateQueue(device, CommandQueueProperties.None);
			renderTarget = new Bitmap(800, 600);
			this.BackgroundImage = renderTarget;
			bitmapBuffer = new byte[4 * renderTarget.Width * renderTarget.Height];

			string source = File.ReadAllText("kernel.cu");
			Program pgm = context.CreateProgram(source);
			try
			{
				pgm.Build("", device);
				while (pgm.GetBuildStatus(device) == BuildStatus.InProgress) System.Threading.Thread.Sleep(1);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + ":\n" + pgm.GetBuildLog(device));
				return;
			}
			kernel = pgm.CreateKernel("render");

			camera = new Camera();
			camera.Position = new Float3(sizeX / 2, sizeY / 2, sizeZ / 2);
			camera.Direction = new Float3(1, 0, 0);
			camera.Direction.Normalize();
			camera.FocalLength = 300;
			camera.Aspect = 800.0f / 600.0f;
			camera.Update();

			worldData = new int[sizeX * sizeY * sizeZ];

			Random rnd = new Random();
			Bitmap bmp = (Bitmap)Bitmap.FromFile("heightmap.png");
			for (int x = 0; x < sizeX; x++)
			{
				for (int z = 0; z < sizeZ; z++)
				{
					Color c = bmp.GetPixel(x, z);
					int height = (int)(0.5 * c.R);
					for (int y = 0; y < height; y++)
					{
						setWorld(x, y, z, Color.Gray.ToArgb());
					}
					Color gras = Color.FromArgb(0, rnd.Next(120, 137), 0);
					setWorld(x, height, z, gras.ToArgb());
				}
			}

			int numTrees = rnd.Next(10, 500);
			for (int i = 0; i < numTrees; i++)
			{
				int x = rnd.Next(1, sizeX - 1);
				int z = rnd.Next(1, sizeZ - 1);

				int sy = 0;
				for (sy = 0; sy < sizeY - 4; sy++)
				{
					if (getWorld(x, sy, z).X == 0)
						break;
				}

				int height = rnd.Next(4, 12);

				for (int y = 1; y < 4 + height; y++)
				{
					setWorld(x, sy + y, z, Color.Brown.ToArgb());

					if (y > 3)
					{
						setWorld(x - 1, sy + y, z, Color.Lime.ToArgb());
						setWorld(x, sy + y, z - 1, Color.Lime.ToArgb());
						setWorld(x + 1, sy + y, z, Color.Lime.ToArgb());
						setWorld(x, sy + y, z + 1, Color.Lime.ToArgb());
					}
				}
				setWorld(x, sy + height + 4, z, Color.Lime.ToArgb());
			}

			target = context.CreateBuffer(MemoryFlags.ReadOnly | MemoryFlags.CopyHostPtr, bitmapBuffer);
			cameraBuffer = context.CreateBuffer<Camera>(MemoryFlags.ReadWrite | MemoryFlags.CopyHostPtr, camera);
			world = context.CreateBuffer(MemoryFlags.ReadOnly | MemoryFlags.CopyHostPtr, worldData);

			base.OnLoad(e);

			timerRender.Start();
		}

		float rotation = 0;

		private void Render()
		{
			kernel.SetArgument(0, target);
			kernel.SetArgument(1, renderTarget.Width);
			kernel.SetArgument(2, renderTarget.Height);
			kernel.SetArgument(3, cameraBuffer);
			kernel.SetArgument(4, world);
			kernel.SetArgument(5, sizeX);
			kernel.SetArgument(6, sizeY);
			kernel.SetArgument(7, sizeZ);

			queue.EnqueueWriteBuffer<Camera>(
				cameraBuffer,
				true,
				0,
				camera).Wait();

			queue.EnqueueNDRangeKernel(
				kernel,
				2,
				null,
				new uint[] { (uint)renderTarget.Width, (uint)renderTarget.Height },
				null).Wait();

			queue.EnqueueReadBuffer(
				target,
				true,
				0,
				bitmapBuffer).Wait();

			var bmpLock = renderTarget.LockBits(new Rectangle(0, 0, renderTarget.Width, renderTarget.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
			Marshal.Copy(bitmapBuffer, 0, bmpLock.Scan0, bitmapBuffer.Length);
			renderTarget.UnlockBits(bmpLock);
			this.Invalidate();
		}

		private void timerRender_Tick(object sender, EventArgs e)
		{
			timerRender.Stop();

			if (GetAsyncKeyState(Keys.Left) != 0)
				rotation -= 0.1f;
			if (GetAsyncKeyState(Keys.Right) != 0)
				rotation += 0.1f;
			camera.Direction = new Float3(
				(float)(Math.Cos(rotation)),
				0,
				(float)(Math.Sin(rotation)));
			camera.Update();

			Float3 offset = new Float3(0, 0, 0);
			if (GetAsyncKeyState(Keys.W) != 0)
				camera.Position += camera.Direction;
			if (GetAsyncKeyState(Keys.S) != 0)
				camera.Position -= camera.Direction;
			if (GetAsyncKeyState(Keys.A) != 0)
				camera.Position -= camera.Right;
			if (GetAsyncKeyState(Keys.D) != 0)
				camera.Position += camera.Right;

			this.Render();
			timerRender.Start();
		}
	}
}
