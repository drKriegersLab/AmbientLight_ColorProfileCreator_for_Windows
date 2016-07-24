﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows;



namespace AmbientLight_ColorProfileCreator_for_Windows
{
    public class ColorCapture
    {
        #region variable definitions

        /**** Exterbak DLL imports for capture pixels and mouse position ****/ 

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDCm, int x, int y, int nWidth, int nHeigh, IntPtr hSrcDC, int xSrc, int ySrc, int swRop);


        [DllImport("user32.dll")]

        //Variable definitions for cursor's capturing
        static extern bool GetCursorPos(ref Point lpPoint);
        Point cursor = new Point();

        //thread definitions
        Thread thread_colorCapturing_with_mouse;
        Thread thread_colorCapturing_with_grid;



        private Color captured_color = Color.Black; //storage color data
        private Color captured_color2 = Color.Black; //storage color data


        //get screen specific datas
        private int screenHeight = Screen.PrimaryScreen.Bounds.Height;
        private int screenWidth = Screen.PrimaryScreen.Bounds.Width;
        

        #endregion


        #region public_functions_and interfaces
        /// <summary>
        /// Init function
        /// </summary>
        public ColorCapture()
        {
            
            //start_capturing_with_mouse();
            start_capturing_with_grid();
            logger.add(LogTypes.ColorCapturing, "screenHeight: " + screenHeight);
            logger.add(LogTypes.ColorCapturing, "screenWidth: " + screenWidth);
        }
        public SolidBrush getColor()
        {
            return new SolidBrush(captured_color);
        }
        public SolidBrush getColor2()
        {
            return new SolidBrush(captured_color2);
        }
        /**** Thread management ****/


        public void start_capturing_with_mouse()
        {
            thread_colorCapturing_with_mouse = new Thread(capture_pixel_color_with_mouse);
            thread_colorCapturing_with_mouse.Start();
            logger.add(LogTypes.ColorCapturing, "colorCapturing with mouse thread started");
        }


        public void stop_capturing_with_mouse()
        {
            thread_colorCapturing_with_mouse.Abort();
            logger.add(LogTypes.ColorCapturing, "colorCapturing with mouse thread aborted");
        }


        public void start_capturing_with_grid()
        {
            thread_colorCapturing_with_grid = new Thread(capture_pixel_color_with_full_image);
            thread_colorCapturing_with_grid.Start();
            //while (!thread_colorCapturing_with_grid.IsAlive) ;
            logger.add(LogTypes.ColorCapturing, "colorCapturing with total image started");
        }


        public void stop_capturing_with_grid()
        {
            thread_colorCapturing_with_grid.Abort();
            logger.add(LogTypes.ColorCapturing, "colorCapturing with total image thread aborted");
        }
        #endregion

        #region private_capture_functions_wMouse

        /***** Capture methods ****/

        private void capture_pixel_color_with_mouse()
        {
            //create a bitmap (which consists of the pixel data for a graphios image and its attrubtes)
            Bitmap screenCopy = new Bitmap(1, 1); //initialize bitmap with specified size (int32, int32) --> this contains one pixel
            using (Graphics gdest = Graphics.FromImage(screenCopy)) //create a Graphics (IDisposable) object with "screenCopy" bitmap
                                                                    //because it is provide a mechanism for releasing unmanaged resources after using, therefore it is very memory-friendly
            {
                while (true)
                {
                    using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        GetCursorPos(ref cursor);
                        int x = cursor.X;
                        int y = cursor.Y;
                        IntPtr hSrcDC = gsrc.GetHdc(); //gets the handle to device, which associated with this Graphics object
                        IntPtr hDC = gdest.GetHdc();
                        int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, x, y, (int)CopyPixelOperation.SourceCopy); //fastest way for access screen that Windows show
                        //TODO: this method may won't working on GPU-accekerated content --> we will get a black image from videos, games etc.
                        //but it's working on my PC :-)
                        gdest.ReleaseHdc();
                        gsrc.ReleaseHdc();
                    }
                    captured_color = Color.FromArgb(screenCopy.GetPixel(0, 0).ToArgb()); //convert captured pixel to Color object
                    Thread.Sleep(100);
                }
            }
        }

        #endregion

        


      
        #region private_capture_functions_wFullImage

        private void capture_pixel_color_with_full_image(object stateInformation)
        {
            Color pixelColor = new Color();
            long t_last = 0;
            long t_curr = 0;

            int retval = 0;
            int A = 0;
            int R = 0;
            int G = 0;
            int B = 0;

            
            int stride; //width of a single row of pixels
            System.Drawing.Imaging.BitmapData srcData; //pointer of address of the bitmap's first line
            IntPtr Scan0; //
            long[] totals; //arraw for store summ of RGB components

            //create a bitmap (which consists of the pixel data for a graphios image and its attrubtes)
            Bitmap screenCopy = new Bitmap(screenWidth, screenHeight); //initialize bitmap with specified size (int32 width, int32 height) --> this contains one pixel
            using (Graphics gdest = Graphics.FromImage(screenCopy)) //create a Graphics (IDisposable) object with "screenCopy" bitmap
                                                                    //because it is provide a mechanism for releasing unmanaged resources after using, therefore it is very memory-friendly
            {
                while (true)
                {
                    A = 0;
                    R = 0;
                    G = 0;
                    B = 0;
                    t_curr = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    logger.add(LogTypes.ColorCapturing," image processing turnaround time: " + Convert.ToSingle(t_curr - t_last) / 1000);
                    //Console.Write(" b1 {0}  ", Convert.ToSingle(t_curr - t_last) / 1000);
                    using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        IntPtr hSrcDC = gsrc.GetHdc(); //gets the handle to source device, which associated with this Graphics object
                        IntPtr hDC = gdest.GetHdc();   //get handler of destination device
                        /*
                        BitBlt: bit-block transfer of the color data between specified source and destination devive
                        inputs:  
                            - hdcDest: handle to the destination device
                            - nXDest:  x coordinate of upper-left corner of the destination rectangle
                            - nYDest:  y coordinate of upper-left corner of the destination rectangle
                            - nWiddth: width of the destination rectangle
                            - nHeight: height of the destination rectangle
                            - hdcSrc:  handler of source
                            - nXSrc:   x coordinate of upper-left corner of the source rectangle
                            - nYSrc:   y coordinate of upper-left corner of the source rectangle
                            - dwRop:   opration code. What we do.
                        */
                        retval = BitBlt(hDC, 0, 0, screenWidth, screenHeight, hSrcDC, 0, 0, (int)CopyPixelOperation.SourceCopy); //fastest way for access screen that Windows show
                                                                                                                                 //TODO: this method may won't working on GPU-accekerated content --> we will get a black image from videos, games etc.
                        t_curr = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        //logger.add(LogTypes.ColorCapturing, "2. capture time: " + Convert.ToSingle(t_curr - t_last) / 1000);


                        //lock the bitmap's bits and add its first line's pointer to srcData
                        srcData = screenCopy.LockBits(new Rectangle(0, 0, screenWidth, screenHeight),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        stride =  srcData.Stride;  //get width of a single row of pixels in the bitmap
                        Scan0 = srcData.Scan0; //get the first address of the firs pixel data (and this means the first scan line too)

                        totals = new long[] {0, 0, 0, 0, 0, 0 }; //set total RGB values to zero

                        unsafe
                        { 
                            byte* p = (byte*)(void*)Scan0; //add the first addres of first pixel data to pointer

                            for (int y = 0; y < screenHeight; y++) //iterate on height coordinates
                            {
                                for (int x = 0; x < screenWidth; x++) //iterate on width coordinates
                                {
                                    for (int color = 0; color < 3; color++) //iteration on colors
                                    {
                                        int idx = (y * stride) + x * 3 + color;
                                        if (x < (screenWidth / 2))
                                            totals[color] += p[idx];
                                        else
                                            totals[color + 3] += p[idx];

                                    }
                                }
                            }
                        }
                        screenCopy.UnlockBits(srcData);


                        int avgB1 = Convert.ToInt32(totals[0] / (screenWidth * screenHeight));
                        int avgG1= Convert.ToInt32(totals[1] / (screenWidth * screenHeight));
                        int avgR1 = Convert.ToInt32(totals[2] / (screenWidth * screenHeight));

                        int avgB2 = Convert.ToInt32(totals[3] / (screenWidth * screenHeight));
                        int avgG2 = Convert.ToInt32(totals[4] / (screenWidth * screenHeight));
                        int avgR2 = Convert.ToInt32(totals[5] / (screenWidth * screenHeight));

                        //logger.add(LogTypes.ColorCapturing, avgR1.ToString() + "  " + avgG1.ToString() + "  " + avgB1.ToString() + "  " + avgR2.ToString() + "  " + avgG2.ToString() + "  " + avgB2.ToString());

                        t_curr = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        //logger.add(LogTypes.ColorCapturing, "3. conversion time: " + Convert.ToSingle(t_curr - t_last) / 1000);
                        //Console.Write(" b3 {0}  ", Convert.ToSingle(t_curr - t_last) / 1000);
                        gdest.ReleaseHdc();
                        gsrc.ReleaseHdc();
                        captured_color = Color.FromArgb(Convert.ToInt32(avgR1),
                                                        Convert.ToInt32(avgG1),
                                                        Convert.ToInt32(avgB1));
                        captured_color2 = Color.FromArgb(Convert.ToInt32(avgR2),
                                                        Convert.ToInt32(avgG2),
                                                        Convert.ToInt32(avgB2));
                    }
                }
            }
        }
        #endregion
    }
}