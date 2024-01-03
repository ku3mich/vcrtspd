using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using FlashCap;

// (c) Roger Hardiman 2016, 2021

// This class uses a System Timer to generate a YUV image at regular intervals and Audio at regular intervals
// The ReceivedYUVFrame event is fired for each new YUV image
// The ReceivedAudioFrame event is fired for each chunk of Audio

namespace vcrtspd;

public class CaptureSource
{
    private System.Timers.Timer frame_timer;
    private byte[] yuv_frame = null;
    private int width = 0;
    private int height = 0;

    Stopwatch stopwatch = new Stopwatch();

    private long frame_count = 0;
    public event ReceivedYUVFrameHandler ReceivedYUVFrame;
    CaptureDevice CaptureDevice;

    public CaptureSource(CaptureDeviceDescriptor device, VideoCharacteristics mode)
    {
        width = mode.Width;
        height = mode.Height;

        // YUV size
        int y_size = width * height;
        int u_size = (width >> 1) * (height >> 1);
        int v_size = (width >> 1) * (height >> 1);
        yuv_frame = new byte[y_size + u_size + v_size];

        // Set all values to 127
        for (int x = 0; x < yuv_frame.Length; x++)
        {
            yuv_frame[x] = 127;
        }

        stopwatch.Start();

        // Start timer. The Timer will generate each YUV frame
        frame_timer = new System.Timers.Timer();
        frame_timer.Interval = 1; // on first pass timer will fire straight away (cannot have zero interval)
        frame_timer.AutoReset = false; // do not restart timer after the time has elapsed
        frame_timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
        {
            // send a video frame
            var img = yuv_frame;
            if (ReceivedYUVFrame != null && yuv_frame != null)
                ReceivedYUVFrame((uint)stopwatch.ElapsedMilliseconds, width, height, img);

            frame_count++;

            // Some CPU cycles will have been used in Sending the YUV Frame.
            // Compute the delay required (the Timer Interval) before sending the next YUV frame
            long time_for_next_tick_ms = (frame_count * 1000) / mode.FramesPerSecond.Numerator;
            long time_to_wait = time_for_next_tick_ms - stopwatch.ElapsedMilliseconds;
            if (time_to_wait <= 0) time_to_wait = 1; // cannot have negative or zero intervals
            frame_timer.Interval = time_to_wait;
            frame_timer.Start();
        };

        frame_timer.Start();


        void YUVfromRGB(out byte y0, out byte uv, byte R, byte G, byte B)
        {
            var y = 0.257 * R + 0.504 * G + 0.098 * B + 16;
            var u = -0.148 * R - 0.291 * G + 0.439 * B + 128;
            var v = 0.439 * R - 0.368 * G - 0.071 * B + 128;

            y0 = (byte)y;
            uv = (byte)((((byte)(u / 16)) << 4) | ((byte)(v / 16)));
        }

        CaptureDevice = device.OpenAsync(mode, (scope) =>
        {
            if (ReceivedYUVFrame == null)
                return;

            var image = new Bitmap(Image.FromStream(new MemoryStream(scope.Buffer.CopyImage())));

            for (var i = 0; i < height; i++)
                for (var j = 0; j < width; j++)
                {
                    var color = image.GetPixel(j, i);

                    var r = color.R;
                    var g = color.G;
                    var b = color.B;

                    //var gr = (0.299 * r + 0.587 * g + 0.114 * b);

                    yuv_frame[i * width + j] = (byte)(((66 * (r) + 129 * (g) + 25 * (b) + 128) >> 8) + 16);
                    /*
                    var u = (byte)(((-38 * (r) - 74 * (g) + 112 * (b) + 128) >> 8) + 128);
                    var v = (byte)(((112 * (r) - 94 * (g) - 18 * (b) + 128) >> 8) + 128);

                    var u4 = u / 4;
                    var v4 = v / 4;

                    yuv_frame[640 * 480 + i * width / 2 + j / 2] = (byte)((byte)((v << 2) | u) & 127);
                    */
                }
        }).Result;

        CaptureDevice.StartAsync();
    }

    // Dispose
    public void Disconnect()
    {
        frame_timer.Stop();
        frame_timer.Dispose();
        CaptureDevice.StopAsync().RunSynchronously();
        CaptureDevice.Dispose();
    }

}