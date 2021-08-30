﻿using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace geckoinator9000
{
    class Program
    {
        public static Dictionary<string, string> geckoDict = new Dictionary<string, string>();

        static string options = "Options:\n0: show options\n1: load images into memory\n2: parse images in /input/ and uploads to /output/\n3: exits the program";

        static void Main(string[] args)
        {
            Console.WriteLine(options);
            
            while (true)
            {
                Console.Write("> ");

                string option = Console.ReadLine();

                switch (option)
                {
                    //options
                    case "0":
                        Console.WriteLine(options);
                        break;
                    //load geckos into memory
                    case "1":
                        if (File.Exists(@"../../../sourceImgs/data.txt"))
                        {
                            StreamReader data = new StreamReader(@"../../../sourceImgs/data.txt");
                            string[] values = data.ReadToEnd().Split(";");
                            geckoDict = values.Select(a => a
                            .Split(","))
                            .Where(part => part.Length == 2)
                            .ToDictionary(a => a[0], a=> a[1]);
                            data.Dispose();
                            Console.WriteLine("Load complete");
                        }
                        else
                        {
                            loadImages();
                            Console.WriteLine("Load complete, written to file");
                        }
                        break;
                    //creates mosaics
                    case "2":
                        if (geckoDict.Count() == 0)
                        {
                            Console.WriteLine("Make sure source images are loaded by running 1");
                            break;
                        }

                        Console.WriteLine("Enter number of images to tile horizontally (high values will lead to long processing times, recommended cap: 128):");
                        Console.Write("> ");
                        int x;
                        if (!int.TryParse(Console.ReadLine(), out x))
                        {
                            Console.WriteLine("Input not a number");
                            break;
                        }

                        Console.WriteLine("Enter width of individual gecko (default 32):");
                        Console.Write("> ");
                        int geckoWidth;
                        string temp = Console.ReadLine();
                        if (temp == "")
                        {
                            geckoWidth = 32;
                        }
                        else if (!int.TryParse(temp, out geckoWidth))
                        {
                            Console.WriteLine("Input not a number");
                            break;
                        }

                        Console.WriteLine("Starting process...");
                        generateImage(x, geckoWidth);

                        Console.WriteLine("Generation done, go to output directory to see results");
                        break;
                    //quits program
                    case "3":
                        return;
                }
            }
        }

        //geckofys all the images in input folder
        static void generateImage(int x, int geckoWidth)
        {
            string[] directory = Directory.GetFiles(@"../../../input");

            if (directory.Length == 0)
            {
                Console.WriteLine("Add images to /input/ to get started");
                return;
            }

            Random random = new Random();

            //allows images to be geckofy'd in parallel
            Parallel.ForEach(directory, path => generateSingleImage(path, random, x, geckoWidth));
        }

        //geckofys a single image
        static void generateSingleImage(string path, Random random, int x, int geckoWidth)
        {
            //calculate image stats
            Image image = Image.FromFile(path);

            double sourceRatio = (double)x / image.Width;
            int sx = (int)Math.Round(image.Width * sourceRatio);
            int sy = (int)Math.Round(image.Height * sourceRatio);

            Bitmap sourceBitmap = new Bitmap(image, new Size(sx, sy));
            Bitmap bitmap = new Bitmap(geckoWidth * sx, geckoWidth * sy);

            //gets color of pixels in small source image
            for (int i = 0; i < sx; i++)
            {
                for (int j = 0; j < sy; j++)
                {
                    //gets color of pixel
                    Color c = sourceBitmap.GetPixel(i, j);

                    //gets closest color present in gecko dictionary
                    string key = getClosestColor(c);

                    //matches any key with closest color
                    Regex regex = new Regex(@$"^{key}.+");

                    string[] keys = geckoDict.Keys.Where(a => regex.Match(a).Success).ToArray();

                    //chooses a random one of the keys if more than 1 present
                    int index = random.Next(keys.Length);

                    string finalKey = keys[index];

                    //appends gecko image to final bitmap
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        Bitmap finalImage = new Bitmap(Image.FromFile(geckoDict[finalKey]), geckoWidth, geckoWidth);
                        g.DrawImage(finalImage, i * geckoWidth, j * geckoWidth);
                        finalImage.Dispose();
                        g.Dispose();
                    }
                }
            }

            string name = path.Split("\\").Last();

            Console.WriteLine("Finished " + name);

            //saves image as png
            bitmap.Save(@$"../../../output/{string.Join(".", name.Split(".").SkipLast(1))}.png", ImageFormat.Png);

            //disposes all disposable stuff
            image.Dispose();
            sourceBitmap.Dispose();
            bitmap.Dispose();
        }

        //based on https://www.codeproject.com/articles/17044/find-the-nearest-color-with-c-using-the-euclidean#:~:text=%20Find%20the%20Nearest%20Color%20with%20C%23%20-,paste%20the%20method...%204%20History.%20%20More%20
        static string getClosestColor(Color input)
        {
            double inputR = Convert.ToDouble(input.R);
            double inputG = Convert.ToDouble(input.G);
            double inputB = Convert.ToDouble(input.B);

            double distance = 500.0;

            string closestKey = "";

            //gets closest color present in dictionary
            foreach (string key in geckoDict.Keys)
            {
                double red = Math.Pow(double.Parse(key.Split("/")[0]) - inputR, 2.0);
                double green = Math.Pow(double.Parse(key.Split("/")[1]) - inputG, 2.0);
                double blue = Math.Pow(double.Parse(key.Split("/")[2]) - inputB, 2.0);

                var temp = Math.Sqrt(red + green + blue);

                if (temp == 0.0)
                {
                    closestKey = key;
                    break;
                }
                else if (temp < distance)
                {
                    distance = temp;
                    closestKey = key;
                }
            }

            //returns closest color
            return closestKey.Split("/")[0] + "/" + closestKey.Split("/")[1] + "/" + closestKey.Split("/")[2];
        }

        //parses images in sourceImgs and saves them into a dictionary
        static void loadImages()
        {
            string[] directory = Directory.GetFiles(@"../../../sourceImgs");

            if (directory.Length == 0)
            {
                Console.WriteLine("Add images to /sourceImgs/ to get started");
                return;
            }

            foreach (string path in directory)
            {
                loadSingleImage(path);
            }

            //writes dictionary to file for future use
            StreamWriter data = new StreamWriter(@"../../../sourceImgs/data.txt");
            data.WriteLine(DictToString(geckoDict, "{0},{1};"));
            data.Dispose();
        }

        static void loadSingleImage(string path)
        {
            //skips non-image content
            if (path.Split(".").Last() != "png")
            {
                Console.WriteLine("Only pngs allowed, continuing: " + path);
                return;
            }

            Bitmap bitmap = new Bitmap(path);
            int total = 0;
            int r = 0, g = 0, b = 0;

            if (bitmap.Height != bitmap.Width)
            {
                Console.WriteLine("Only square images allowed, continuing: " + path);
                return;
            }

            //gets color values of every pixel
            for (int i = 0; i < bitmap.Height; i++)
            {
                for (int j = 0; j < bitmap.Width; j++)
                {
                    Color c = bitmap.GetPixel(j, i);

                    //skips if alpha levels are below a threshold
                    if (c.A < 127)
                    {
                        Console.WriteLine(c.A + " - " + path);
                        r += 0;
                        g += 0;
                        b += 0;
                    }
                    else
                    {
                        r += c.R;
                        g += c.G;
                        b += c.B;
                    }

                    total++;
                }
            }

            //extra digit in case of repeat results
            int extra = geckoDict.Keys.Where(a => a.Contains((r / total) + "/" + (g / total) + "/" + (b / total))).Count();

            //averaging red, green and blue values
            geckoDict.Add((r / total) + "/" + (g / total) + "/" + (b / total) + "/" + extra, path);
            bitmap.Dispose();

            Console.WriteLine("Finished: " + path);
        }

        //dictionary to string
        public static string DictToString<T, V>(IEnumerable<KeyValuePair<T, V>> items, string format)
        {
            format = String.IsNullOrEmpty(format) ? "{0}='{1}' " : format;

            StringBuilder itemString = new StringBuilder();
            foreach (var (key, value) in items)
                itemString.AppendFormat(format, key, value);

            return itemString.ToString();
        }
    }
}
