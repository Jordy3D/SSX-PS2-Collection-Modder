﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using SSX_Modder.Utilities;
using ImageMagick;

namespace SSX_Modder.FileHandlers
{
    class SSHHandler
    {
        public string MagicWord;
        public int Size;
        public int Ammount;
        public string format;
        public string group;
        public string endingstring;
        public List<SSHImage> sshImages = new List<SSHImage>();
        public void LoadSSH(string path)
        {
            sshImages = new List<SSHImage>();
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                MagicWord = StreamUtil.ReadString(stream, 4);

                if (MagicWord == "SHPS")
                {
                    Size = StreamUtil.ReadInt32(stream);

                    Ammount = StreamUtil.ReadInt32(stream);

                    format = StreamUtil.ReadString(stream, 4);

                    try
                    {
                        StandardToBitmap(stream, (int)stream.Position);
                    }
                    catch
                    {
                        sshImages = new List<SSHImage>();
                        MessageBox.Show("Error reading File " + MagicWord + " " + format);
                    }
                }
                stream.Dispose();
                stream.Close();
            }
        }

        public void StandardToBitmap(Stream stream, int offset)
        {
            stream.Position = offset;
            byte[] tempByte;
            for (int i = 0; i < Ammount; i++)
            {
                SSHImage tempImage = new SSHImage();

                tempImage.shortname = StreamUtil.ReadString(stream, 4);

                tempImage.offset = StreamUtil.ReadInt32(stream);

                sshImages.Add(tempImage);
            }

            group = StreamUtil.ReadString(stream, 4);

            endingstring = StreamUtil.ReadString(stream, 4);

            for (int i = 0; i < sshImages.Count; i++)
            {
                SSHImage tempImage = sshImages[i];
                SSHImageHeader tempImageHeader = new SSHImageHeader();

                stream.Position = tempImage.offset;

                tempByte = new byte[1];
                stream.Read(tempByte, 0, tempByte.Length);
                tempImageHeader.MatrixFormat = tempByte[0];

                tempImageHeader.Size = StreamUtil.ReadInt24(stream);

                tempImageHeader.Width = StreamUtil.ReadInt16(stream);

                tempImageHeader.Height = StreamUtil.ReadInt16(stream);

                tempImageHeader.Xaxis = StreamUtil.ReadInt16(stream);

                tempImageHeader.Yaxis = StreamUtil.ReadInt16(stream);

                //Add Other Flags Later
                tempImageHeader.LXPos = StreamUtil.ReadInt12(stream);

                tempImageHeader.TYPos = StreamUtil.ReadInt12(stream);

                int RealSize;

                if (tempImageHeader.Size != 0)
                {
                    RealSize = tempImageHeader.Size - 16;
                }
                else
                {
                    RealSize = tempImageHeader.Width * tempImageHeader.Height;
                    if (tempImageHeader.MatrixFormat == 5)
                    {
                        RealSize = RealSize * 4;
                    }
                }

                //Read Matrix
                tempByte = new byte[RealSize];
                stream.Read(tempByte, 0, tempByte.Length);
                tempImage.Matrix = tempByte;

                //Decompress
                if (tempImageHeader.MatrixFormat == 130)
                { 
                    RefpackHandler refpackHandler = new RefpackHandler();
                    tempImage.Matrix = refpackHandler.Decompress(tempImage.Matrix);
                }

                //Split Image Into Proper Bytes
                if (tempImageHeader.MatrixFormat == 1)
                {
                    tempByte = new byte[RealSize * 2];
                    int posPoint = 0;
                    for (int a = 0; a < tempImage.Matrix.Length; a++)
                    {
                        tempByte[posPoint] = (byte)ByteUtil.ByteToBitConvert(tempImage.Matrix[a], 0, 3);
                        posPoint++;
                        tempByte[posPoint] = (byte)ByteUtil.ByteToBitConvert(tempImage.Matrix[a], 4, 7);
                        posPoint++;
                    }
                    tempImage.Matrix = tempByte;
                }

                //INDEXED COLOUR
                if (tempImageHeader.MatrixFormat == 2 || tempImageHeader.MatrixFormat == 1 || tempImageHeader.MatrixFormat == 130)
                {
                    int Spos = (int)stream.Position;
                    bool find = false;
                    while (!find)
                    {
                        if (stream.ReadByte() == 0x21)
                        {
                            Spos = (int)stream.Position;
                            find = true;
                        }
                    }
                    SSHColourTable sshTable = new SSHColourTable();

                    sshTable.Size = StreamUtil.ReadInt24(stream);

                    sshTable.Width = StreamUtil.ReadInt16(stream);

                    sshTable.Height = StreamUtil.ReadInt16(stream);

                    sshTable.Total = StreamUtil.ReadInt16(stream);

                    sshTable.Format = StreamUtil.ReadInt32(stream);

                    sshTable.colorTable = new List<Color>();

                    int tempSize = (sshTable.Size / 4) - 4;
                    if (sshTable.Size == 0)
                    {
                        tempSize = sshTable.Total;
                    }

                    stream.Position = Spos +15;

                    for (int a = 0; a < tempSize; a++)
                    {
                        sshTable.colorTable.Add(StreamUtil.ReadColour(stream));
                    }
                    tempImage.sshTable = sshTable;
                }

                //Find End Of Image
                long endPos = -1;

                if (i + 1 < sshImages.Count)
                {
                    endPos = sshImages[i + 1].offset;
                }
                else
                {
                    endPos = stream.Length;
                }

                //Colour Correction
                int tempRead = stream.ReadByte();
                if(tempRead==105)
                {
                    SSHColourTable sshTable = tempImage.sshTable;
                    for (int c = 0; c < sshTable.colorTable.Count; c++)
                    {
                        Color tempColor = sshTable.colorTable[c];
                        int A = 255;
                        int R = tempColor.R * 2 - 1;
                        if (R < 0)
                        {
                            R = 0;
                        }
                        else if (R > 255)
                        {
                            R = 255;
                        }
                        int G = tempColor.G * 2 - 1;
                        if (G < 0)
                        {
                            G = 0;
                        }
                        else if (G > 255)
                        {
                            G = 255;
                        }
                        int B = tempColor.B * 2 - 1;
                        if (B < 0)
                        {
                            B = 0;
                        }
                        else if (B > 255)
                        {
                            B = 255;
                        }
                        sshTable.colorTable[c] = Color.FromArgb(A, R, G, B);
                    }
                    tempImage.sshTable = sshTable;
                }
                else
                {
                    stream.Position -= 1;
                }


                //Get LongName
                endPos = ByteUtil.FindPosition(stream, new byte[1] { 0x70 }, stream.Position - 1, endPos);
                if (endPos != -1)
                {
                    stream.Position = endPos;

                    tempImage.unknownEnd = StreamUtil.ReadInt32(stream);

                    tempImage.longname = StreamUtil.ReadNullEndString(stream);
                }

                //Create Bitmap Image
                tempImage.bitmap = new Bitmap(tempImageHeader.Width, tempImageHeader.Height, PixelFormat.Format32bppArgb);
                int post = 0;
                if (tempImageHeader.MatrixFormat == 1)
                {
                    for (int y = 0; y < tempImageHeader.Height; y++)
                    {
                        for (int x = 0; x < tempImageHeader.Width; x++)
                        {
                            int colorPos = tempImage.Matrix[post];
                            if (tempImage.sshTable.Format != 0)
                            {
                                //colorPos = simulateSwitching4th5thBit(colorPos);
                            }
                            tempImage.bitmap.SetPixel(x, y, tempImage.sshTable.colorTable[colorPos]);
                            post++;
                        }
                    }
                }
                else
                if (tempImageHeader.MatrixFormat == 2 || tempImageHeader.MatrixFormat == 130)
                {
                    if (tempImageHeader.LXPos == 0)
                    {
                        for (int y = 0; y < tempImageHeader.Height; y++)
                        {
                            for (int x = 0; x < tempImageHeader.Width; x++)
                            {
                                int colorPos = tempImage.Matrix[post];
                                if (tempImage.sshTable.Format != 0)
                                {
                                    colorPos = ByteUtil.ByteBitSwitch(colorPos);
                                }
                                tempImage.bitmap.SetPixel(x, y, tempImage.sshTable.colorTable[colorPos]);
                                post++;
                            }
                        }
                    }
                    else
                    {

                        byte[,] MatrixRedo = ByteUtil.ByteArraySwap(tempImage.Matrix, tempImageHeader);

                        for (int a = 0; a < tempImageHeader.Height; a++)
                        {
                            for (int b = 0; b < tempImageHeader.Width; b++)
                            {
                                int colorPos = MatrixRedo[a, b];
                                if (tempImage.sshTable.Format != 0)
                                {
                                    colorPos = ByteUtil.ByteBitSwitch(colorPos);
                                }
                                tempImage.bitmap.SetPixel(b, a, tempImage.sshTable.colorTable[colorPos]);
                            }
                        }
                    }
                }
                else
                if (tempImageHeader.MatrixFormat == 5)
                {
                    SSHColourTable colourTable = new SSHColourTable();
                    colourTable.colorTable = new List<Color>();
                    for (int y = 0; y < tempImageHeader.Height; y++)
                    {
                        for (int x = 0; x < tempImageHeader.Width; x++)
                        {
                            int R = tempImage.Matrix[post];
                            post++;
                            int G = tempImage.Matrix[post];
                            post++;
                            int B = tempImage.Matrix[post];
                            post++;
                            int A = tempImage.Matrix[post]*2-1;
                            post++;
                            if (A < 0)
                            {
                                A = 0;
                            }
                            else if (A > 255)
                            {
                                A = 255;
                            }
                            tempImage.bitmap.SetPixel(x, y, Color.FromArgb(A, R, G, B));
                            if (!colourTable.colorTable.Contains(Color.FromArgb(A, R, G, B)))
                            {
                                colourTable.colorTable.Add(Color.FromArgb(A, R, G, B));
                            }
                        }
                    }
                    tempImage.sshTable = colourTable;
                }
                else
                {
                    MessageBox.Show("Error reading File " + MagicWord + " " + format + "- Matrix " + tempImageHeader.MatrixFormat.ToString());
                    break;
                }
                tempImage.sshHeader = tempImageHeader;
                sshImages[i] = tempImage;
            }
            stream.Dispose();
        }

        public void BMPExtract(string path)
        {
            string index = format + Environment.NewLine;
            for (int i = 0; i < sshImages.Count; i++)
            {
                byte[] temp = new byte[4];
                temp[0] = sshImages[i].sshHeader.MatrixFormat;
                int tempint = BitConverter.ToInt32(temp, 0);
                index += sshImages[i].shortname + "." + sshImages[i].longname + ".png" + "," + tempint.ToString() + Environment.NewLine;
                sshImages[i].bitmap.Save(path + "\\" + sshImages[i].shortname + "." + sshImages[i].longname + ".png", ImageFormat.Png);
            }

            File.WriteAllText(path + "\\Index.txt", index);
        }

        public void BMPOneExtract(string path, int i)
        {
            sshImages[i].bitmap.Save(path, ImageFormat.Png);
        }

        public void LoadSingle(string path, int i)
        {
            Stream stream = File.Open(path, FileMode.Open);

            var ImageTemp = Image.FromStream(stream);
            stream.Close();
            stream.Dispose();
            SSHImage temp = sshImages[i];
            temp.bitmap = (Bitmap)ImageTemp;
            SSHColourTable colourTable = new SSHColourTable();
            colourTable.colorTable = new List<Color>();
            for (int y = 0; y < temp.bitmap.Height; y++)
            {
                for (int x = 0; x < temp.bitmap.Width; x++)
                {
                    Color color = temp.bitmap.GetPixel(x, y);
                    if (!colourTable.colorTable.Contains(color))
                    {
                        colourTable.colorTable.Add(color);
                    }
                }
            }
            temp.sshTable = colourTable;
            sshImages[i] = temp;
        }

        public void LoadFolder(string path)
        {
            MagicWord = "";
            Size = 0;
            group = "";
            endingstring = "";
            sshImages = new List<SSHImage>();
            string[] paths = File.ReadAllLines(path + "\\Index.txt");
            format = paths[0].Replace(Environment.NewLine, "");
            Ammount = paths.Length - 1;
            int[] Maxtrixarray = new int[paths.Length - 1];
            for (int i = 1; i < paths.Length; i++)
            {
                string[] temp1 = paths[i].Split(',');
                paths[i] = path + "\\" + temp1[0];
                Maxtrixarray[i - 1] = int.Parse(temp1[1]);
            }

            for (int i = 1; i < paths.Length; i++)
            {
                Stream stream = File.Open(paths[i], FileMode.Open);
                SSHImage tempImage = new SSHImage();
                SSHImageHeader imageHeader = new SSHImageHeader();
                var ImageTemp = Image.FromStream(stream);
                stream.Close();
                stream.Dispose();
                tempImage.bitmap = (Bitmap)ImageTemp;
                imageHeader.MatrixFormat = (byte)Maxtrixarray[i - 1];

                string name = Path.GetFileName(paths[i].Replace(".png", ""));
                string[] NameList = name.Split('.');

                tempImage.longname = NameList[1];
                tempImage.shortname = NameList[0];
                imageHeader.Width = tempImage.bitmap.Width;
                imageHeader.Height = tempImage.bitmap.Height;
                SSHColourTable colourTable = new SSHColourTable();
                colourTable.colorTable = new List<Color>();

                for (int y = 0; y < tempImage.bitmap.Height; y++)
                {
                    for (int x = 0; x < tempImage.bitmap.Width; x++)
                    {
                        Color color = sshImages[i].bitmap.GetPixel(x, y);
                        if (!colourTable.colorTable.Contains(color))
                        {
                            colourTable.colorTable.Add(color);
                        }
                    }
                }
                tempImage.sshTable = colourTable;
                tempImage.sshHeader = imageHeader;
                sshImages.Add(tempImage);
            }
        }

        public void SaveSSH(string path)
        {
            Stream stream = new MemoryStream();
            MagicWord = "SHPS";
            byte[] tempByte = new byte[4];
            Encoding.ASCII.GetBytes(MagicWord).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, tempByte.Length);

            tempByte = new byte[4];
            stream.Write(tempByte, 0, tempByte.Length);

            tempByte = new byte[4];
            BitConverter.GetBytes(sshImages.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, tempByte.Length);

            tempByte = new byte[4];
            Encoding.ASCII.GetBytes(format).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, tempByte.Length);

            List<int> intPos = new List<int>();

            for (int i = 0; i < sshImages.Count; i++)
            {
                tempByte = new byte[4];
                Encoding.ASCII.GetBytes(sshImages[i].shortname).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, tempByte.Length);
                intPos.Add((int)stream.Position);
                tempByte = new byte[4];
                stream.Write(tempByte, 0, tempByte.Length);
            }

            tempByte = new byte[16];
            Encoding.ASCII.GetBytes("Buy ERTS").CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, tempByte.Length);

            for (int i = 0; i < sshImages.Count; i++)
            {
                int temp = (int)stream.Position;
                stream.Position = intPos[i];

                //Set Start Offset

                tempByte = new byte[4];
                BitConverter.GetBytes(temp).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, tempByte.Length);

                stream.Position = temp;

                tempByte = new byte[4];
                BitConverter.GetBytes(sshImages[i].sshHeader.MatrixFormat).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);

                //Set SSH Header Info
                if (sshImages[i].sshHeader.MatrixFormat == 1)
                {
                    tempByte = new byte[4];
                    BitConverter.GetBytes((sshImages[i].sshHeader.Width * sshImages[i].sshHeader.Height / 2) + 16).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 3);
                }
                else
                if (sshImages[i].sshHeader.MatrixFormat == 2)
                {
                    tempByte = new byte[4];
                    BitConverter.GetBytes(sshImages[i].sshHeader.Width * sshImages[i].sshHeader.Height + 16).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 3);
                }
                else if (sshImages[i].sshHeader.MatrixFormat == 5)
                {
                    tempByte = new byte[4];
                    BitConverter.GetBytes((sshImages[i].sshHeader.Width * sshImages[i].sshHeader.Height * 4) + 16).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 3);
                }

                tempByte = new byte[4];
                BitConverter.GetBytes(sshImages[i].sshHeader.Width).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 2);

                tempByte = new byte[4];
                BitConverter.GetBytes(sshImages[i].sshHeader.Height).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 2);

                tempByte = new byte[4];
                BitConverter.GetBytes(sshImages[i].sshHeader.Xaxis).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 2);

                tempByte = new byte[4];
                BitConverter.GetBytes(sshImages[i].sshHeader.Yaxis).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 2);

                //Fix Later
                tempByte = new byte[4];
                //sshImages[i].sshHeader.Format
                BitConverter.GetBytes(0).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, tempByte.Length);

                if (sshImages[i].sshHeader.MatrixFormat == 1)
                {
                    Maxtrix1Write(stream, i, stream.Position);
                }
                if (sshImages[i].sshHeader.MatrixFormat == 2)
                {
                    Maxtrix2Write(stream, i, stream.Position);
                }
                else if (sshImages[i].sshHeader.MatrixFormat == 5)
                {
                    Maxtrix5Write(stream, i);
                }
                else if (sshImages[i].sshHeader.MatrixFormat==130)
                {
                    MessageBox.Show("Error Can't Compress file (Compresson method doesn't exist)");
                }

                //ending
                tempByte = new byte[4] { 0x70, 0x00, 0x00, 0x00 };
                stream.Write(tempByte, 0, tempByte.Length);

                tempByte = new byte[sshImages[i].longname.Length + 1];
                Encoding.ASCII.GetBytes(sshImages[i].longname).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, tempByte.Length);

                tempByte = new byte[9];
                Encoding.ASCII.GetBytes("Buy ERTS").CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, tempByte.Length);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var file = File.Create(path);
            stream.Position = 0;
            stream.CopyTo(file);
            stream.Dispose();
            file.Close();
        }

        public void Maxtrix1Write(Stream stream, int i, long pos)
        {
            stream.Position = pos;
            byte[] tempByte;
            SSHColourTable colourTable = new SSHColourTable();
            colourTable.colorTable = new List<Color>();
            byte[] ByteCombine = new byte[2];
            int bytepos = 0;
            //colourTable.colorTable.Add(Color.FromArgb(0, 0, 0, 0));
            for (int y = 0; y < sshImages[i].bitmap.Height; y++)
            {
                for (int x = 0; x < sshImages[i].bitmap.Width; x++)
                {
                    Color color = sshImages[i].bitmap.GetPixel(x, y);
                    if (colourTable.colorTable.Contains(color))
                    {
                        int index = colourTable.colorTable.IndexOf(color);
                        tempByte = new byte[4];
                        BitConverter.GetBytes(index).CopyTo(tempByte, 0);
                        ByteCombine[bytepos] = tempByte[0];
                    }
                    else
                    {
                        colourTable.colorTable.Add(color);
                        int index = colourTable.colorTable.Count - 1;
                        tempByte = new byte[4];
                        BitConverter.GetBytes(index).CopyTo(tempByte, 0);
                        ByteCombine[bytepos] = tempByte[0];
                    }
                    bytepos++;

                    if (bytepos == 2)
                    {
                        bytepos = 0;
                        tempByte = new byte[4];
                        BitConverter.GetBytes(ByteUtil.BitConbineConvert(ByteCombine[0], ByteCombine[1], 0, 4, 4)).CopyTo(tempByte, 0);
                        ByteCombine = new byte[2];
                        stream.Write(tempByte, 0, 1);
                    }
                }
            }

            if (colourTable.colorTable.Count > 16)
            {
                MessageBox.Show("Error More Than 16 Colours " + sshImages[i].longname);
            }

            //Colour Table
            tempByte = new byte[4];
            BitConverter.GetBytes(0x21).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 1);

            tempByte = new byte[4];
            BitConverter.GetBytes((colourTable.colorTable.Count * 4) + 16).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 3);

            tempByte = new byte[4];
            BitConverter.GetBytes(colourTable.colorTable.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 2);

            tempByte = new byte[4];
            BitConverter.GetBytes(1).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 2);

            tempByte = new byte[4];
            BitConverter.GetBytes(colourTable.colorTable.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 2);

            tempByte = new byte[6] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            //BitConverter.GetBytes(colourTable.colorTable.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, tempByte.Length);

            for (int a = 0; a < colourTable.colorTable.Count; a++)
            {
                tempByte = new byte[4];
                int R = colourTable.colorTable[a].R;
                int G = colourTable.colorTable[a].G;
                int B = colourTable.colorTable[a].B;
                int A = (colourTable.colorTable[a].A + 1) / 2;
                BitConverter.GetBytes(R).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
                tempByte = new byte[4];
                BitConverter.GetBytes(G).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
                tempByte = new byte[4];
                BitConverter.GetBytes(B).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
                tempByte = new byte[4];
                BitConverter.GetBytes(A).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
            }
        }

        public void Maxtrix2Write(Stream stream, int i, long pos)
        {
            stream.Position = pos;
            byte[] tempByte;
            SSHColourTable colourTable = new SSHColourTable();
            colourTable.colorTable = new List<Color>();
            //colourTable.colorTable.Add(Color.FromArgb(0, 0, 0, 0));
            for (int y = 0; y < sshImages[i].bitmap.Height; y++)
            {
                for (int x = 0; x < sshImages[i].bitmap.Width; x++)
                {
                    Color color = sshImages[i].bitmap.GetPixel(x, y);
                    if (colourTable.colorTable.Contains(color))
                    {
                        int index = colourTable.colorTable.IndexOf(color);
                        index = ByteUtil.ByteBitSwitch(index);
                        tempByte = new byte[4];
                        BitConverter.GetBytes(index).CopyTo(tempByte, 0);
                        stream.Write(tempByte, 0, 1);
                    }
                    else
                    {
                        colourTable.colorTable.Add(color);
                        int index = colourTable.colorTable.Count - 1;
                        index = ByteUtil.ByteBitSwitch(index);
                        tempByte = new byte[4];
                        BitConverter.GetBytes(index).CopyTo(tempByte, 0);
                        stream.Write(tempByte, 0, 1);
                    }
                }
            }

            if (colourTable.colorTable.Count > 256)
            {
                MessageBox.Show("Error More Than 256 Colours " + sshImages[i].longname);
            }

            //Colour Table
            tempByte = new byte[4];
            BitConverter.GetBytes(0x21).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 1);

            tempByte = new byte[4];
            BitConverter.GetBytes((colourTable.colorTable.Count * 4) + 16).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 3);

            tempByte = new byte[4];
            BitConverter.GetBytes(colourTable.colorTable.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 2);

            tempByte = new byte[4];
            BitConverter.GetBytes(1).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 2);

            tempByte = new byte[4];
            BitConverter.GetBytes(colourTable.colorTable.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, 2);

            tempByte = new byte[6] { 0x00, 0x00, 0x00, 0x20, 0x00, 0x00 };
            //BitConverter.GetBytes(colourTable.colorTable.Count).CopyTo(tempByte, 0);
            stream.Write(tempByte, 0, tempByte.Length);

            for (int a = 0; a < colourTable.colorTable.Count; a++)
            {
                tempByte = new byte[4];
                int R = colourTable.colorTable[a].R;
                int G = colourTable.colorTable[a].G;
                int B = colourTable.colorTable[a].B;
                int A = (colourTable.colorTable[a].A + 1) / 2;
                BitConverter.GetBytes(R).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
                tempByte = new byte[4];
                BitConverter.GetBytes(G).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
                tempByte = new byte[4];
                BitConverter.GetBytes(B).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
                tempByte = new byte[4];
                BitConverter.GetBytes(A).CopyTo(tempByte, 0);
                stream.Write(tempByte, 0, 1);
            }
        }

        public void Maxtrix5Write(Stream stream, int i)
        {
            byte[] tempByte;
            SSHColourTable colourTable = new SSHColourTable();
            colourTable.colorTable = new List<Color>();
            //colourTable.colorTable.Add(Color.FromArgb(0, 0, 0, 0));
            for (int y = 0; y < sshImages[i].bitmap.Height; y++)
            {
                for (int x = 0; x < sshImages[i].bitmap.Width; x++)
                {
                    Color color = sshImages[i].bitmap.GetPixel(x, y);
                    tempByte = new byte[4];
                    int R = color.R;
                    int G = color.G;
                    int B = color.B;
                    int A = (color.A + 1) / 2;
                    BitConverter.GetBytes(R).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 1);
                    tempByte = new byte[4];
                    BitConverter.GetBytes(G).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 1);
                    tempByte = new byte[4];
                    BitConverter.GetBytes(B).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 1);
                    tempByte = new byte[4];
                    BitConverter.GetBytes(A).CopyTo(tempByte, 0);
                    stream.Write(tempByte, 0, 1);
                }
            }
        }
    }


    struct SSHImage
    {
        public string shortname;
        public string longname;
        public int unknownEnd;
        public int offset;
        public SSHImageHeader sshHeader;
        public byte[] Matrix;
        public SSHColourTable sshTable;
        public Bitmap bitmap;
    }
    public struct SSHImageHeader
    {
        public byte MatrixFormat;
        public int Size;
        public int Width;
        public int Height;
        public int Xaxis;
        public int Yaxis;
        public int LXPos;
        public bool flag1;
        public bool flag2;
        public bool flag3;
        public bool flag4;
        public int TYPos;
        public int Mipmaps; //Unit4
    }

    struct SSHColourTable
    {
        public int Size;
        public int Width;
        public int Height;
        public int Total;
        public int Format;
        public List<Color> colorTable;
    }
}
