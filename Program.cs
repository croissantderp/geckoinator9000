using System;
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

        static ParallelOptions pOptions = new ParallelOptions();

        static void Main(string[] args)
        {
            if (!Directory.Exists(@"../../../output/"))
            {
                Directory.CreateDirectory(@"../../../output/");
            }
            if (!Directory.Exists(@"../../../input/"))
            {
                Directory.CreateDirectory(@"../../../input/");
            }

            Console.WriteLine("Enter the maximum # of parallel processes allowed (high numbers can cause memory overflow, default 32):");
            Console.Write("> ");
            int maxNum;
            string maxNumIn = Console.ReadLine();
            if (maxNumIn == "")
            {
                maxNum = 32;
            }
            else if (!int.TryParse(maxNumIn, out maxNum))
            {
                Console.WriteLine("Input not a number");
                return;
            }

            pOptions.MaxDegreeOfParallelism = maxNum;

            Console.WriteLine(options);

            while (true)
            {
                Console.Write("> ");

                string option = Console.ReadLine();

                try
                {
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
                                .ToDictionary(a => a[0], a => a[1]);
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

                            Console.WriteLine("Enter number of images to tile horizontally (high values will lead to long processing times, recommended cap: 256. default: 128):");
                            Console.Write("> ");
                            int x;
                            string temp = Console.ReadLine();
                            if (temp == "")
                            {
                                x = 128;
                            }
                            else if (!int.TryParse(temp, out x))
                            {
                                Console.WriteLine("Input not a number");
                                break;
                            }

                            Console.WriteLine("Enter width of individual gecko (default: 32):");
                            Console.Write("> ");
                            int geckoWidth;
                            temp = Console.ReadLine();
                            if (temp == "")
                            {
                                geckoWidth = 32;
                            }
                            else if (!int.TryParse(temp, out geckoWidth))
                            {
                                Console.WriteLine("Input not a number");
                                break;
                            }

                            Console.WriteLine("Use dithering? (default: Y) Y/N:");
                            Console.Write("> ");
                            bool dither;
                            temp = Console.ReadLine().ToUpper();
                            dither = temp == "N" ? false : true;

                            Console.WriteLine("Starting process... (this could take a while)");
                            generateImage(x, geckoWidth, dither);

                            Console.WriteLine("Generation done, go to output directory to see results");
                            break;
                        //quits program
                        case "3":
                            return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("operation failed: " + ex.ToString());
                }
            }
        }

        //geckofys all the images in input folder
        static void generateImage(int x, int geckoWidth, bool dither)
        {
            string[] directory = Directory.GetFiles(@"../../../input");

            if (directory.Length == 0)
            {
                Console.WriteLine("Add images to /input/ to get started");
                return;
            }

            Random random = new Random();

            if (dither)
            {
                //allows images to be geckofy'd in parallel
                Parallel.ForEach(directory, pOptions, path => DITHERgenerateSingleImage(path, random, x, geckoWidth));
            }
            else
            {
                //allows images to be geckofy'd in parallel
                Parallel.ForEach(directory, pOptions, path => generateSingleImage(path, random, x, geckoWidth));
            }
        }

        //geckofys a single image without dithering
        static void generateSingleImage(string path, Random random, int ox, int geckoWidth)
        {
            string name = path.Split("\\").Last();

            //calculate image stats
            Image image = Image.FromFile(path);

            double sourceRatio = (double)ox / image.Width;
            int sx = (int)Math.Round(image.Width * sourceRatio);
            int sy = (int)Math.Round(image.Height * sourceRatio);

            Bitmap sourceBitmap = new Bitmap(image, new Size(sx, sy));
            Bitmap bitmap = new Bitmap(geckoWidth * sx, geckoWidth * sy);

            int[,] alphas = new int[sx, sy];

            for (int y = 0; y < sy; y++)
            {
                for (int x = 0; x < sx; x++)
                {
                    alphas[x, y] = sourceBitmap.GetPixel(x, y).A;
                }
            }

            image.Dispose();

            Console.WriteLine("Finished initializing " + name);

            //gets color of pixels in small source image
            for (int i = 0; i < sx; i++)
            {
                for (int j = 0; j < sy; j++)
                {
                    //gets color of pixel
                    Color c = sourceBitmap.GetPixel(i, j);

                    //gets closest color present in gecko dictionary
                    Color nc = getClosestColor(c, alphas[i, j]);

                    //matches any key with closest color
                    Regex regex = new Regex(@$"^{nc.R}/{nc.G}/{nc.B}.+");

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

            Console.WriteLine("Finished geckofying " + name);

            //saves image as png
            bitmap.Save(@$"../../../output/{string.Join(".", name.Split(".").SkipLast(1))}.png", ImageFormat.Png);

            //disposes all disposable stuff
            sourceBitmap.Dispose();
            bitmap.Dispose();
        }

        //geckofys a single image using Floyd-Steinberg dithering
        static void DITHERgenerateSingleImage(string path, Random random, int ox, int geckoWidth)
        {
            string name = path.Split("\\").Last();

            //calculate image stats
            Image image = Image.FromFile(path);

            double sourceRatio = (double)ox / image.Width;
            int sx = (int)Math.Round(image.Width * sourceRatio);
            int sy = (int)Math.Round(image.Height * sourceRatio);

            Bitmap sourceBitmap = new Bitmap(image, new Size(sx, sy));
            Bitmap bitmap = new Bitmap(geckoWidth * sx, geckoWidth * sy);

            int[,] alphas = new int[sx, sy];

            for (int y = 0; y < sy; y++)
            {
                for (int x = 0; x < sx; x++)
                {
                    alphas[x, y] = sourceBitmap.GetPixel(x, y).A;
                }
            }

            image.Dispose();

            Console.WriteLine("Finished initializing " + name);

            //processes and dithers small source image
            for (int y = 0; y < sy; y++)
            {
                for (int x = 0; x < sx; x++)
                {
                    //gets color of pixel
                    Color c = sourceBitmap.GetPixel(x, y);
                    //gets closest color present in gecko dictionary
                    Color nc = getClosestColor(c, alphas[x, y]);

                    sourceBitmap.SetPixel(x, y, nc);

                    int RedError = c.R - nc.R;
                    int greenError = c.G - nc.G;
                    int BlueError = c.B - nc.B;

                    if (x + 1 < sourceBitmap.Width)
                    {
                        sourceBitmap.SetPixel(x + 1, y, calculateError(sourceBitmap.GetPixel(x + 1, y), RedError, greenError, BlueError, 7));
                        if (y + 1 < sourceBitmap.Height)
                        {
                            sourceBitmap.SetPixel(x + 1, y + 1, calculateError(sourceBitmap.GetPixel(x + 1, y + 1), RedError, greenError, BlueError, 1));
                        }
                    }
                    if (x - 1 > 0 && y + 1 < sourceBitmap.Height)
                    {
                        sourceBitmap.SetPixel(x - 1, y + 1, calculateError(sourceBitmap.GetPixel(x - 1, y + 1), RedError, greenError, BlueError, 3));
                    }
                    if (y + 1 < sourceBitmap.Height)
                    {
                        sourceBitmap.SetPixel(x, y + 1, calculateError(sourceBitmap.GetPixel(x, y + 1), RedError, greenError, BlueError, 5));
                    }
                }
            }

            Console.WriteLine("Finished preparing " + name);

            //gets color of pixels in small source image
            for (int i = 0; i < sx; i++)
            {
                for (int j = 0; j < sy; j++)
                {
                    //gets color of pixel
                    Color c = sourceBitmap.GetPixel(i, j);

                    //matches any key with closest color
                    Regex regex = new Regex(@$"^{c.R}/{c.G}/{c.B}.+");

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

            Console.WriteLine("Finished geckofying " + name);

            //saves image as png
            bitmap.Save(@$"../../../output/{string.Join(".", name.Split(".").SkipLast(1))}.png", ImageFormat.Png);

            //disposes all disposable stuff
            sourceBitmap.Dispose();
            bitmap.Dispose();
        }

        //calculates error amounts for pixel
        static Color calculateError(Color inputColor, int rErr, int gErr, int bErr, int diviser)
        {
            int finalR = inputColor.R + ((rErr * diviser) >> 4);
            int finalG = inputColor.G + ((gErr * diviser) >> 4);
            int finalB = inputColor.B + ((bErr * diviser) >> 4);

            finalR = finalR < 255 ? (finalR > 0 ? finalR : 0) : 255;
            finalG = finalG < 255 ? (finalG > 0 ? finalG : 0) : 255;
            finalB = finalB < 255 ? (finalB > 0 ? finalB : 0) : 255;

            return Color.FromArgb(finalR, finalG, finalB);
        }

        //based on https://www.codeproject.com/articles/17044/find-the-nearest-color-with-c-using-the-euclidean#:~:text=%20Find%20the%20Nearest%20Color%20with%20C%23%20-,paste%20the%20method...%204%20History.%20%20More%20
        static Color getClosestColor(Color input, int alpha)
        {
            double averageA = alpha / 255.0;

            double inputR = Convert.ToDouble(input.R * averageA);
            double inputG = Convert.ToDouble(input.G * averageA);
            double inputB = Convert.ToDouble(input.B * averageA);

            double distance = 500.0;

            //string closestKey = "";
            (double, double, double) thing = (0,0,0);

            //gets closest color present in dictionary
            foreach (string key in geckoDict.Keys)
            {
                double keyRed = double.Parse(key.Split("/")[0]);
                double keyGreen = double.Parse(key.Split("/")[1]);
                double keyBlue = double.Parse(key.Split("/")[2]);

                double red = Math.Pow(keyRed - inputR, 2.0);
                double green = Math.Pow(keyGreen - inputG, 2.0);
                double blue = Math.Pow(keyBlue - inputB, 2.0);

                var temp = Math.Sqrt(red + green + blue);

                if (temp == 0.0)
                {
                    thing = (keyRed, keyGreen, keyBlue);
                    //closestKey = key;
                    break;
                }
                else if (temp < distance)
                {
                    distance = temp;
                    thing = (keyRed, keyGreen, keyBlue);
                    //closestKey = key;
                }
            }

            //returns closest color
            //return closestKey.Split("/")[0] + "/" + closestKey.Split("/")[1] + "/" + closestKey.Split("/")[2];
            return Color.FromArgb((int)thing.Item1, (int)thing.Item2, (int)thing.Item3);
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

            bool edited = false;

            //gets color values of every pixel
            for (int i = 0; i < bitmap.Height; i++)
            {
                for (int j = 0; j < bitmap.Width; j++)
                {
                    Color c = bitmap.GetPixel(j, i);

                    //handles non opaque alpha levels
                    if (c.A < 255)
                    {
                        int alpha = c.A / 255;

                        r += 0;
                        g += 0;
                        b += 0;

                        //sets background to black as well in order to not cause issues
                        bitmap.SetPixel(j, i, Color.FromArgb(255, c.R * alpha, c.G * alpha, c.B * alpha));

                        edited = true;
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

            if (edited)
            {
                Bitmap bitmap2 = new Bitmap(bitmap);
                bitmap.Dispose();

                bitmap2.Save(path, ImageFormat.Png);
                bitmap2.Dispose();
            }
            else
            {
                bitmap.Dispose();
            }

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
