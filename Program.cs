using FlashCap;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace vcrtspd;

static partial class Program
{
    private static ILoggerFactory loggerFactory;
    static CaptureDevices Devices = new CaptureDevices();

    static void ShowDescriptors()
    {
        foreach (var descriptor in Devices.EnumerateDescriptors())
        {
            foreach (var ch in descriptor.Characteristics)
                Console.WriteLine($"[{descriptor.Name}]/+/[{ch.Width}x{ch.Height}x{ch.PixelFormat}]");
        }
    }

    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("use one of devices as an argument:");
            ShowDescriptors();

            return;
        }

        var device = args[0];
        var desc = Devices
            .EnumerateDescriptors()
            .SelectMany(s => s.Characteristics, (d, ch) => new { characteristics = ch, descriptor = d, id = $"[{d.Name}]/+/[{ch.Width}x{ch.Height}x{ch.PixelFormat}]" })
            .FirstOrDefault(s => s.id == device);

        if (desc == null)
        {
            Console.WriteLine($"ERROR: device: |{device}| not found, use one of following:");
            Console.WriteLine();
            ShowDescriptors();

            return;
        }

        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("RtspCameraExample", LogLevel.Debug)
                .AddFilter("Rtsp", LogLevel.Debug)
                .AddConsole();
        });

        var demo = new App(loggerFactory, desc.descriptor, desc.characteristics);
    }
}
