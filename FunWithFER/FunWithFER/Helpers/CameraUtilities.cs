using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace FunWithFER.Helpers
{
    public static class CameraUtilities
    {
        public static async Task<DeviceInformation> FindBestCameraAsync(DeviceClass cameraClass = DeviceClass.VideoCapture)
        {
            var devices = await DeviceInformation.FindAllAsync(cameraClass);

            Debug.WriteLine($"{devices.Count} devices found");

            // If there are no cameras connected to the device
            if (devices.Count == 0)
                return null;

            foreach (var device in devices)
            {
                Debug.WriteLine($"\t{device.Name}");
            }
            
            // If there is only one camera, return that one
            if (devices.Count == 1)
                return devices.FirstOrDefault();

            // If there are multiple cameras, make a decision on which is best. For my tests, I prefer to use high-res USB webcam

            var externalCamera = devices.FirstOrDefault(
                     x => x.Name.Contains("HD Pro Webcam C920") || // this is the known name for my device
                     x.EnclosureLocation != null && x.EnclosureLocation.Panel == Panel.Unknown); // this means its not a camera attached to the device

            if (externalCamera != null)
                return externalCamera;

            // If there's no external webcam, start working on what is available, this option is usually the front facing camera in a laptop or phone
            var frontCamera = devices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == Panel.Front);

            if (frontCamera != null)
                return frontCamera;

            // last fallback
            return devices.FirstOrDefault();
        }
    }
}
