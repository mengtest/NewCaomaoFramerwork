using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
namespace CaomaoFramework
{
    public struct CaomaoGifFrameJob : IJob
    {
        //需要外部传入的变量
        public int FrameCount;
        public int GifWidth;
        public int GifHeigth;





        public float delay;
        public int blockSize;
        public NativeArray<byte> imageData;
        public NativeArray<int> imageBigData;
        public NativeList<byte> gifData;
        public NativeArray<byte> blockData;

        public NativeArray<int> Position;//读取的位置
        public NativeList<CaomaoGifFrame> AlreadyFrames;//已经读取的帧的数据

        //需要传递给外部的数据
        public NativeArray<bool> allFrameFinished;//是否所有的帧已经完成
        public NativeArray<int> allFrameCount;//所有帧的数量

        public int imageX;
        public int imageY;
        public int imageWidth;
        public int imageHeight;


        public bool lctFlag;

        public bool interlace;
        public NativeArray<int> act;
        public NativeArray<int> lct;
        public NativeArray<int> gct;//全局的ColorTable

        public CaomaoGIFImageData gifImageData;

        public bool bError;//是否有错误

        public int bgIndex;
        public int transIndex;
        public bool transparency;
        public int bgColor;
        public int dispose;



        public int lastDispose;
        public int lastImageX;
        public int lastImageY;
        public int lastImageWidth;
        public int lastImageHeigth;
        public int lastBgColor;
        public int loopCount;




        public int lctSize;

        public void Execute()
        {

        }
        /// <summary>
        /// 分配image数据
        /// </summary>
        public void AlloctorImageData()
        {
            this.imageBigData = new NativeArray<int>(this.imageWidth * this.imageHeight, Allocator.TempJob);
            this.imageData = new NativeList<byte>(this.imageWidth * this.imageHeight * sizeof(int), Allocator.TempJob);
        }
        /// <summary>
        /// 应用的扩展部分
        /// </summary>
        private void ReadApplicationExtension()
        {
            this.ReadBlock();
            var app = "";
            for (var i = 0; i < 11; i++)
            {
                app += (char)this.blockData[i];
            }
            if (app.Equals("NETSCAPE2.0"))
            {
                ReadNetscapeExt();
            }
            else
            {
                Skip();
            }              
        }
        /// <summary>
        ///  Reads Netscape extenstion to obtain iteration count
        /// </summary>
        private void ReadNetscapeExt()
        {
            do
            {
                ReadBlock();
                if (this.blockData[0] != 1) continue;
                // loop count sub-block
                var b1 = this.blockData[1] & 0xff;
                var b2 = this.blockData[2] & 0xff;
                this.loopCount = (b2 << 8) | b1;
            } while ((this.blockSize > 0) && !this.bError);
        }

        private void ReadNextFrame(int position)
        {
            //this.position++;
            //var code = this.gifData[this.position];
            var code = this.ReadByteToInt();
            switch (code)
            {
                case 0x2C://图像数据部分
                    this.ReadImage();
                    break;
                case 0x21://扩展部分
                    code = this.ReadByteToInt();
                    switch (code)
                    {
                        case 0xf9:
                            this.ReadGraphicControllExt();
                            break;
                        case 0xff:
                            this.ReadApplicationExtension();
                            break;
                        default:
                            this.Skip();
                            break;
                    }
                    break;
                case 0x3b://终止的标志

                    break;
                case 0x00:
                    break;
                default:
                    //表示出错
                    break;
            }
        }

        //扩展块标识   8bit
        //图形控制扩展标签   8bit
        //块大小  8bit
        //保留 3 处置方法 3 用户输入标志 i 1 透明色标志 t 1  ==> 8bit
        //延时时间 8bit
        //透明色索引 8bit
        //块终结器 8bit
        private void ReadGraphicControllExt()
        {
            this.ReadByteToInt();//BlockSize
            var packed = this.ReadByteToInt();
            this.dispose = (packed & 0x1c) >> 2;
            if (this.dispose == 1)
            {
                this.dispose = 0;
            }
            this.transparency = (packed & 1) != 0;
            this.delay = this.ReadShort() / 100f;//延迟多少秒进行下一帧
            this.transIndex = this.ReadByteToInt();
            this.ReadByteToInt();//块终结器
        }

        private int ReadByteToInt()
        {
            this.Position[0] = this.Position[0] + 1;
            var data = this.gifData[this.Position[0]];
            return data;
        }
        private byte ReadByte()
        {
            this.Position[0] = this.Position[0] + 1;
            var data = this.gifData[this.Position[0]];
            return data;
        }


        private int ReadBlock()
        {
            this.blockSize = this.ReadByteToInt();
            var n = 0;
            if (this.blockSize <= 0)
            {
                return n;
            }
            while (n < this.blockSize)
            {
                var count = this.ReadLength(n, this.blockSize - n, out this.blockData);
                if (count == -1)
                {
                    break;
                }
                n += count;
            }
            if (n < this.blockSize)
            {
                Debug.LogError("解析GIF数据出错");
                this.bError = true;
                return int.MinValue;
            }
            return n;
        }


        /// <summary>
        /// 读取图像标识符(Image Descriptor)
        /// 8bit -图像标识符
        /// 16bit - 图像X
        /// 16bit - 图像Y
        /// 16bit - 图像Width
        /// 16bit - 图像Height
        /// 1bit Local Color Table Flag 
        /// 1bit interlace 
        /// 1bit sortFlag 
        /// 2bit r保留 
        /// 3bit pixel
        /// </summary>
        private void ReadImage()
        {
            this.imageX = this.ReadShort();
            this.imageY = this.ReadShort();
            this.imageWidth = this.ReadShort();
            this.imageHeight = this.ReadShort();

            var packed = this.ReadByteToInt();
            //1000 0000
            //this.lctFlag = (packed & 0x80) != 0;//也可以直接右移7位
            this.lctFlag = (packed >> 7) > 0;//如果大于0表示是true
            this.interlace = (packed & 0x40) != 0;
            //0000 0111
            //packed
            this.lctSize = 2 << (packed & 7);
            if (this.lctFlag)
            {
                this.ReadColorTable(this.lctSize, out this.lct);
                this.act = this.lct;
            }
            else
            {
                this.act = this.gct;
                if (this.bgIndex == this.transIndex)
                {
                    this.bgColor = 0;
                }
            }
            var save = 0;
            if (this.transparency)
            {
                save = this.act[this.transIndex];
                this.act[this.transIndex] = 0;//设置为透明的颜色
            }
            if (this.act == default(NativeArray<int>))
            {
                Debug.LogError("解析GiF的格式出现错误");
                return;
            }
            this.ReadImageData();
            this.Skip();
            if (this.bError)
            {
                //有错误直接跳过
                return;
            }
            this.AlloctorImageData();
            this.SetPixels();

            this.imageData = NativeArrayExtensions.Reinterpret<int, byte>(this.imageBigData);

        }

        private void SetPixels()
        {
            if (this.lastDispose > 0)
            {
                //说明上一帧已经有图像了
                var n = this.FrameCount - 1;
                if (n > 0)
                {
                    //说明还有图像帧
                    if (this.lastDispose == 2)
                    {
                        var fillcolor = this.transparency ? 0 : this.lastBgColor;
                        for (var i = 0; i < this.lastImageHeigth; i++)
                        {
                            var line = i;
                            line += this.lastImageY;
                            if (line >= this.GifHeigth) continue;
                            var linein = this.GifHeigth - line - 1;
                            var dx = linein * this.GifWidth + this.lastImageX;
                            var endx = dx + this.lastImageWidth;
                            while (dx < endx)
                            {
                                this.imageBigData[dx++] = fillcolor;
                            }
                        }
                        //for (var i = 0; i < _lih; i++)
                        //{
                        //    var line = i;
                        //    line += _liy;
                        //    if (line >= _height) continue;
                        //    var linein = _height - line - 1;
                        //    var dx = linein * _width + _lix;
                        //    var endx = dx + _liw;
                        //    while (dx < endx)
                        //    {
                        //        _image[dx++] = fillcolor;
                        //    }
                        //}
                    }               
                }
            }
        }

        private void ResetFrame()
        {
            this.lastDispose = dispose;
            this.lastImageX = this.imageX;
            this.lastImageY = this.imageY;
            this.lastImageWidth = this.imageWidth;
            this.lastImageHeigth = this.imageHeight;
            this.lastBgColor = this.bgColor;
            this.lct.Dispose();//销毁集合
        }

        /// <summary>
        /// 跳过Variable Length Block
        /// </summary>
        private void Skip()
        {
            do
            {
                this.ReadBlock();
            }
            while (this.blockSize > 0 && this.bError == false);
        }

        private int ReadShort()
        {
            var first = this.ReadByteToInt();
            var second = this.ReadByteToInt();
            return first | (second << 8);
        }


        private int ReadLength(int offset, int length, out NativeArray<byte> result)
        {
            var leftLen = this.gifData.Length - this.Position[0];
            Debug.Log("This.GifData.Length:" + leftLen);
            if (leftLen > length)
            {
                result = new NativeArray<byte>(length, Allocator.TempJob);
                if (result != null && this.gifData.Length > 0)
                {
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = this.ReadByte();
                    }
                }
                return result.Length;
            }
            else
            {
                //说明长度不够，需要缩小
                result = new NativeArray<byte>(leftLen, Allocator.TempJob);
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = this.ReadByte();
                }
                return result.Length;
            }
        }

        private bool ReadColorTable(int size, out NativeArray<int> tabArray)
        {
            tabArray = new NativeArray<int>(256, Allocator.TempJob);
            var nBytesCount = 3 * size;
            var readLen = this.ReadLength(0, nBytesCount, out var readArray);
            if (readLen < nBytesCount)
            {
                Debug.LogError("解析GiF的格式出现错误");
                this.bError = true;
                return false;
            }
            else
            {
                var i = 0;
                var j = 0;
                while (i < nBytesCount)
                {
                    uint r = (uint)(tabArray[j++]);
                    var g = (tabArray[j++]) & (uint)0xff;
                    var b = (tabArray[j++]) & (uint)0xff;
                    tabArray[i++] = (int)(0xff000000 | (b << 16) | (g << 8) | r);
                }
            }
            return true;
        }

        private void ReadImageData()
        {
            const int nullCode = -1;
            int code = 0;
            int count = 0;
            int i = 0;
            int top = 0, bits = 0;
            int bi = 0;
            int datum = 0;
            int first = 0;


            var npix = this.imageWidth * this.imageHeight;
            this.gifImageData.Alloctor(npix);//分配内存数据


            //初始化GIF data 的steamer数据

            var dataSize = this.ReadByteToInt();
            var clear = 1 << dataSize;
            var endOfinformation = clear + 1;
            var available = clear + 2;
            var oldCode = nullCode;
            var codeSize = dataSize + 1;
            var codeMask = (1 << codeSize) - 1;
            for (code = 0; code < clear; code++)
            {
                this.gifImageData.m_prefix[code] = 0;
                this.gifImageData.m_suffix[code] = (byte)code;
            }
            //解析GIF的Pixel的Stream的数据
            for (i = 0; i < npix;)
            {
                if (top == 0)
                {
                    for (; bits < codeSize; bits += 8)
                    {
                        if (count == 0)
                        {
                            count = this.ReadBlock();
                            bi = 0;
                        }
                        datum += (this.blockData[bi++] & 0xff) << bits;
                        count--;
                    }
                    //Get the next code
                    code = datum & codeMask;
                    datum >>= codeSize;
                    bits -= codeSize;
                    //Interpret the code
                    if ((code > available) || (code == endOfinformation))
                    {
                        break;
                    }
                    if (code == clear)
                    {
                        codeSize = dataSize + 1;
                        codeMask = (1 << codeSize) - 1;
                        available = clear + 2;
                        oldCode = nullCode;
                        continue;
                    }
                    if (oldCode == nullCode)
                    {
                        this.gifImageData.m_pixelStack[top++] = this.gifImageData.m_suffix[code];
                        oldCode = code;
                        first = code;
                        continue;
                    }
                    var inCode = code;
                    if (code == available)
                    {
                        this.gifImageData.m_prefix[top++] = (byte)first;
                        code = oldCode;
                    }

                    for (; code > clear; code = this.gifImageData.m_prefix[code])
                    {
                        this.gifImageData.m_prefix[top++] = this.gifImageData.m_suffix[code];
                    }
                    first = (this.gifImageData.m_suffix[code]) & 0xff;



                    //Add a new string to the string table,

                    if (available >= CaomaoGIFImageData.MaxSize)
                    {
                        break;
                    }
                    this.gifImageData.m_pixelStack[top++] = (byte)first;
                    this.gifImageData.m_prefix[available] = (short)oldCode;
                    this.gifImageData.m_suffix[available] = (byte)first;
                    available++;
                    if ((available & codeMask) == 0 && (available < CaomaoGIFImageData.MaxSize))
                    {
                        codeSize++;
                        codeMask += available;
                    }
                    oldCode = inCode;
                }
                top--;
                this.gifImageData.m_pixels[i++] = this.gifImageData.m_pixelStack[top];
            }
            for (; i < npix; i++)
            {
                this.gifImageData.m_pixels[i] = 0;
            }
        }
      
    }
}




