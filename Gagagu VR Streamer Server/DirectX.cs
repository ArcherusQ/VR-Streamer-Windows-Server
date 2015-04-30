﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using System.Diagnostics;
using System.Drawing;


namespace Gagagu_VR_Streamer_Server
{
    /// <summary>
    /// Class for Directx capture
    /// </summary>
    public class DirectX
    {
        const int numAdapter = 0;
        const int numOutput = 0;

        Factory1 factory = null;
        Adapter1 adapter = null;
        Device device = null;
        Output output = null;
        Output1 output1 = null;

        Texture2DDescription textureDesc;
        Texture2D screenTexture;
        OutputDuplication duplicatedOutput;
        System.Drawing.Bitmap bitmap = null;
        System.Drawing.Rectangle boundsRect;
        
        SharpDX.DXGI.Resource screenResource;
        OutputDuplicateFrameInformation duplicateFrameInformation;
        DataBox mapSource;
        BitmapData mapDest;
        IntPtr sourcePtr;
        IntPtr destPtr;
        int width = 0;
        int height = 0;

        /// <summary>
        /// Init some variables one times to spend execution time.
        /// </summary>
        public DirectX()
        {

            try
            {
                factory = new Factory1();
                adapter = factory.GetAdapter1(numAdapter);
                device = new Device(adapter);
                output = adapter.GetOutput(numOutput);
                output1 = output.QueryInterface<Output1>();
                // get screen wize

                width = ((SharpDX.Mathematics.Interop.RawRectangle)output.Description.DesktopBounds).Right - ((SharpDX.Mathematics.Interop.RawRectangle)output.Description.DesktopBounds).Left;
                height = ((SharpDX.Mathematics.Interop.RawRectangle)output.Description.DesktopBounds).Bottom - ((SharpDX.Mathematics.Interop.RawRectangle)output.Description.DesktopBounds).Top;

                //width = ((SharpDX.Rectangle)output.Description.DesktopBounds).Width;
                //height = ((SharpDX.Rectangle)output.Description.DesktopBounds).Height;

                textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };

                screenTexture = new Texture2D(device, textureDesc);
                duplicatedOutput = output1.DuplicateOutput(device);
           
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Dispose all
        /// </summary>
        public void close()
        {
            duplicatedOutput.Dispose();
            screenTexture.Dispose();
            output1.Dispose();
            output.Dispose();
            device.Dispose();
            adapter.Dispose();
            factory.Dispose();
        }

        /// <summary>
        /// Creates a screenshot over directx and returns the specified rect as bitmap
        /// </summary>
        /// <param name="CaptureRect">rect to capture from screen</param>
        /// <returns></returns>
        [STAThread]
        public System.Drawing.Bitmap capture(System.Drawing.Rectangle CaptureRect)
        {
            
            // Checkings
            if (factory == null)
                return null;

            if (adapter == null)
                return null;

            if (device == null)
                return null;

            if (output == null)
                return null;

            if (output1 == null)
                return null;

            try
            {

                // Set a min and max rect to prevent errors
                if (CaptureRect.Height <=50) {
                     CaptureRect.Height = 50;
                }

                if (CaptureRect.Y + CaptureRect.Height > height)
                {
                    if (CaptureRect.Y > CaptureRect.Height)
                    {
                        CaptureRect.Y = height - 50;
                        CaptureRect.Height = 50;
                    }
                    else {
                        CaptureRect.Y = 0;
                        CaptureRect.Height = height;
                    }
                }

                if (CaptureRect.Width <= 50)
                {
                    CaptureRect.Width = 50;
                }

                if (CaptureRect.X + CaptureRect.Width > width)
                {
                    if (CaptureRect.X > CaptureRect.Width)
                    {
                        CaptureRect.X = width - 50;
                        CaptureRect.Width = 50;
                    }
                    else
                    {
                        CaptureRect.X = 0;
                        CaptureRect.Width = width;
                    }
                }

                // init
                bool captureDone = false;
                bitmap = new System.Drawing.Bitmap(CaptureRect.Width, CaptureRect.Height, PixelFormat.Format32bppArgb);
                boundsRect = new System.Drawing.Rectangle(0, 0, CaptureRect.Width, CaptureRect.Height);
                // offset in x direction (rect.x)
                int offsetX = (CaptureRect.X * 4);

                // the capture needs some time
                for (int i = 0; !captureDone; i++)
                {
                    try
                    {
                        //capture
                        duplicatedOutput.AcquireNextFrame(1000, out duplicateFrameInformation, out screenResource);
                        // only for wait
                        if (i > 0)
                        {

                            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                                device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                            mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);
                            mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                            sourcePtr = mapSource.DataPointer;
                            destPtr = mapDest.Scan0;

                            // set x position offset to rect.x
                            int rowPitch = mapSource.RowPitch - offsetX;
                            // set pointer to y position
                            sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch * CaptureRect.Y);
                            
                            for (int y = 0; y < CaptureRect.Height; y++)
                            {
                                // set pointer to x position
                                sourcePtr = IntPtr.Add(sourcePtr, offsetX);
                                // copy pixel to bmp
                                Utilities.CopyMemory(destPtr, sourcePtr, (CaptureRect.Width * 4));
                                // incement pointert to next line
                                sourcePtr = IntPtr.Add(sourcePtr, rowPitch);
                                destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                            }

                            bitmap.UnlockBits(mapDest);
                            device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                            captureDone = true;
                        }

                        screenResource.Dispose();
                        duplicatedOutput.ReleaseFrame();

                    }
                    catch  //                    catch (SharpDXException e)
                    {
                        //if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                        //{
                        //   // throw e;
                        //}

                        return null;
                    }
                }

            }
            catch 
            {
                  return null;
            }

            return bitmap;
        }


       
    }
}
