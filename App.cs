using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using FlashCap;
using Microsoft.Extensions.Logging;

namespace vcrtspd
{
    class App
    {
        RtspServer rtspServer = null;
        SimpleH264Encoder h264_encoder = null;
        SimpleG711Encoder ulaw_encoder = null;

        byte[] raw_sps = null;
        byte[] raw_pps = null;

        int port = 8554;

        uint fps = 25;

        ILogger logger;

        public App(ILoggerFactory loggerFactory, CaptureDeviceDescriptor device, VideoCharacteristics mode)
        {
            logger = loggerFactory.CreateLogger<App>();

            // Our programme needs several things...
            //   1) The RTSP Server to send the NALs to RTSP Clients
            //   2) A H264 Encoder to convert the YUV video into NALs
            //   3) A G.711 u-Law audio encoder to convert PCM audio into G711 data
            //   4) A YUV Video Source and PCM Audo Souce (in this case I use a dummy Test Card)

            /////////////////////////////////////////
            // Step 1 - Start the RTSP Server
            /////////////////////////////////////////
            rtspServer = new RtspServer(port, null, null, loggerFactory);
            try
            {
                rtspServer.StartListen();
            }
            catch
            {
                logger.LogDebug("Error: Could not start server");
                return;
            }

            logger.LogDebug("RTSP URL is rtsp://localhost:" + port);

            /////////////////////////////////////////
            // Step 2 - Create the H264 Encoder. It will feed NALs into the RTSP server
            /////////////////////////////////////////

            //uint width = 640;
            //uint height = 480;

            var width = (uint)mode.Width;
            var height = (uint)mode.Height;

            h264_encoder = new SimpleH264Encoder(width, height, fps);

            raw_sps = h264_encoder.GetRawSPS();
            raw_pps = h264_encoder.GetRawPPS();

            /////////////////////////////////////////
            // Step 3 - Start the Video and Audio Test Card (dummy YUV image and dummy PCM audio)
            // It will feed YUV Images into the event handler, which will compress the video into NALs and pass them into the RTSP Server
            // It will feed PCM Audio into the event handler, which will compress the audio into G711 uLAW packets and pass them into the RTSP Server
            /////////////////////////////////////////

            var av_source = new CaptureSource(device, mode);
            //var av_source = new TestCard((int)width, (int)height, 25);
            av_source.ReceivedYUVFrame += Video_source_ReceivedYUVFrame; // the event handler is where all the magic happens

            logger.LogDebug("Press ENTER to exit");

            string readline = null;
            while (readline == null)
            {
                readline = Console.ReadLine();

                // Avoid maxing out CPU on systems that instantly return null for ReadLine
                if (readline == null) Thread.Sleep(500);
            }

            /////////////////////////////////////////
            // Shutdown
            /////////////////////////////////////////
            av_source.ReceivedYUVFrame -= Video_source_ReceivedYUVFrame;

            av_source.Disconnect();
            rtspServer.StopListen();
        }

        private void Video_source_ReceivedYUVFrame(uint timestamp_ms, int width, int height, byte[] yuv_data)
        {
            // Compress the YUV and feed into the RTSP Server
            byte[] raw_video_nal = h264_encoder.CompressFrame(yuv_data);
            bool isKeyframe = true; // the Simple/Tiny H264 Encoders only return I-Frames for every video frame.


            // Put the NALs into a List
            List<byte[]> nal_array = new List<byte[]>();

            // We may want to add the SPS and PPS to the H264 stream as in-band data.
            // This may be of use if the client did not parse the SPS/PPS in the SDP or if the H264 encoder
            // changes properties (eg a new resolution or framerate which gives a new SPS or PPS).
            // Also looking towards H265, the VPS/SPS/PPS do not need to be in the SDP so would be added here.

            bool add_sps_pps_to_keyframe = true;
            if (add_sps_pps_to_keyframe && isKeyframe)
            {
                nal_array.Add(raw_sps);
                nal_array.Add(raw_pps);
            }

            // add the rest of the NALs
            nal_array.Add(raw_video_nal);

            // Pass the NAL array into the RTSP Server
            rtspServer.FeedInRawSPSandPPS(raw_sps, raw_pps);
            rtspServer.FeedInRawNAL(timestamp_ms, nal_array);
        }

        private void Audio_source_ReceivedAudioFrame(uint timestamp_ms, short[] audio_frame)
        {
            // Compress the audio into G711 and feed into the RTSP Server
            byte[] g711_data = ulaw_encoder.EncodeULaw(audio_frame);

            // Pass the audio data into the RTSP Server
            rtspServer.FeedInAudioPacket(timestamp_ms, g711_data);
        }
    }
}
