using System;
using System.Drawing;
using System.Drawing.Imaging;

var bmp = new Bitmap(100, 100);
using var g = Graphics.FromImage(bmp);
g.Clear(Color.FromArgb(100, 150, 200));
bmp.Save(@"C:\temp_test.png", ImageFormat.Png);
bmp.Dispose();
Console.WriteLine("Created C:\\temp_test.png");
