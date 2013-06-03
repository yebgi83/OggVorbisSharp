using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OggVorbisSharp
{
    [TestClass]    
    static public unsafe class OggTest_Bitwises
    {
        static readonly int testSize1 = 43;
  
        static readonly uint[] testBuffer1 = 
        {
            18, 12, 103948, 4325, 543, 76, 432, 52, 3, 65, 4, 56, 32, 42, 34, 21, 1, 23, 32, 546, 456, 7,
            567, 56, 8, 8, 55, 3, 52, 342, 341, 4, 265, 7, 67, 86, 2199, 21, 7, 1, 5, 1, 4
        };

        static readonly int testSize2 = 21;
            
        static readonly uint[] testBuffer2 = 
        {
            216531625, 1237861823, 56732452, 131, 3212421, 12325343, 34547562, 12313212,
            1233432, 534, 5,346435231, 14436467, 7869299, 76326614, 167548585,
            85525151, 0, 12321, 1, 349528352
        };
  
        static readonly int testSize3 = 56;
  
        static readonly uint[] testBuffer3 =
        {
            1, 0, 14, 0, 1, 0, 12, 0, 1, 0, 0, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 1,
            0, 1, 30, 1, 1, 1, 0, 0, 1, 0, 0, 0, 12, 0, 11, 0, 1, 0, 0, 1
        };
    
        static readonly uint[] large =
        {
            2136531625, 2137861823, 56732452, 131, 3212421, 12325343, 34547562, 12313212,
            1233432, 534, 5, 2146435231, 14436467, 7869299, 76326614, 167548585,
            85525151, 0, 12321, 1, 2146528352
        };
        
        static readonly int oneSize = 33;
        
        static readonly uint[] one = 
        { 
            146, 25, 44, 151, 195, 15, 153, 176, 233, 131, 196, 65, 85, 172, 47, 40,
            34, 242, 223, 136, 35, 222, 211, 86, 171, 50, 225, 135, 214, 75, 172, 223, 4
        };
        
        static readonly uint[] oneB = 
        {
            150, 101, 131, 33, 203, 15, 204, 216, 105, 193, 156, 65, 84, 85, 222, 
            8, 139, 145, 227, 126, 34, 55, 244, 171, 85, 100, 39, 195, 173, 18, 
            245, 251, 128
        };
        
        static readonly int twoSize = 6;
            
        static readonly uint[] two = 
        {
            61, 255, 255, 251, 231, 29
        };
        
        static readonly uint[] twoB =
        {
            247, 63, 255, 253, 249, 120
        };
            
        static readonly int threeSize = 54;
        
        static readonly uint[] three =
        {
            169, 2,  232, 252, 91, 132, 156, 36, 89, 13, 123, 176, 144, 32, 254,
            142, 224, 85, 59, 121, 144, 79, 124, 23, 67, 90, 90, 216, 79, 23, 83,
            58, 135, 196, 61, 55, 129, 183, 54, 101, 100, 170, 37, 127, 126, 10,
            100, 52, 4, 14, 18, 86, 77, 1
        };

        static readonly uint[] threeB = 
        {
            206, 128, 42, 153, 57, 8, 183, 251, 13, 89, 36, 30, 32, 144, 183, 
            130, 59, 240, 121, 59, 85, 223, 19, 228, 180, 134, 33, 107, 74, 98, 
            233, 253, 196, 135, 63, 2, 110, 114, 50, 155, 90, 127, 37, 170, 104, 
            200, 20, 254, 4, 58, 106, 176, 144, 0
        };
        
        static readonly int fourSize = 38;
        
        static readonly uint[] four = 
        {
            18, 6, 163, 252, 97, 194, 104, 131, 32, 1, 7, 82, 137, 42, 129, 11, 72, 
            132, 60, 220, 112, 8, 196, 109, 64, 179, 86, 9, 137, 195, 208, 122, 169, 
            28, 2, 133, 0, 1
        };
        
        static readonly uint[] fourB =
        {
            36, 48, 102, 83, 243, 24, 52, 7, 4, 35, 132, 10, 145, 21, 2, 93, 2, 41, 
            1, 219, 184, 16, 33, 184, 54, 149, 170, 132, 18, 30, 29, 98, 229, 67, 
            129, 10, 4, 32
        };
        
        static readonly int fiveSize = 45;
        
        static readonly uint[] five = 
        {
            169, 2, 126, 139, 144, 172, 30, 4, 80, 72, 240, 59, 130, 218, 73, 62, 
            241, 24, 210, 44, 4, 20, 0, 248, 116, 49, 135, 100, 110, 130, 181, 169, 
            84, 75, 159, 2, 1, 0, 132, 192, 8, 0, 0, 18, 22
        };
        
        static readonly uint[] fiveB = 
        {
            1, 84, 145, 111, 245, 100, 128, 8, 56, 36, 40, 71, 126, 78, 213, 226, 
            124, 105, 12, 0, 133, 128, 0, 162, 233, 242, 67, 152, 77, 205, 77, 
            172, 150, 169, 129, 79, 128, 0, 6, 4, 32, 0, 27, 9, 0
        };

        static readonly int sixSize = 7;
        
        static readonly uint[] six =
        {
            17, 177, 170, 242, 169, 19, 148
        };
        
        static readonly uint[] sixB = 
        {
            136, 141, 85, 79, 149, 200, 41
        };
        
        static uint[] mask = new uint[] 
        {
            0x00000000, 0x00000001, 0x00000003, 0x00000007, 0x0000000f,
            0x0000001f, 0x0000003f, 0x0000007f, 0x000000ff, 0x000001ff,
            0x000003ff, 0x000007ff, 0x00000fff, 0x00001fff, 0x00003fff,
            0x00007fff, 0x0000ffff, 0x0001ffff, 0x0003ffff, 0x0007ffff,
            0x000fffff, 0x001fffff, 0x003fffff, 0x007fffff, 0x00ffffff,
            0x01ffffff, 0x03ffffff, 0x07ffffff, 0x0fffffff, 0x1fffffff,
            0x3fffffff, 0x7fffffff, 0xffffffff 
        };
        
        static Ogg.oggpack_buffer o = new Ogg.oggpack_buffer();
        static Ogg.oggpack_buffer r = new Ogg.oggpack_buffer();
    
        [TestMethod]
        static public void Test()
        {
            byte *buffer;
            int bytes = 0;
            
            /* Test read/write together */
            /* Later we test against pregenerated bitstreams */
            Ogg.oggpack_writeinit(ref o);
        
            Console.Write("Small preclipped packing (LSb): ");
            ClipTest(testBuffer1, testSize1, 0, one, oneSize);
            Console.WriteLine("ok.");
            
            Console.Write("Null bit call (LSb): ");
            ClipTest(testBuffer3, testSize3, 0, two, twoSize);
            Console.WriteLine("ok.");
            
            Console.Write("Large preclipped packing (LSb): ");
            ClipTest(testBuffer2, testSize2, 0, three, threeSize);
            Console.WriteLine("ok.");
            
            Console.Write("32 bit precliiped packing (LSb): ");
            {
                Ogg.oggpack_reset(ref o);
                
                for (int i = 0; i < testSize2; i++) {
                    Ogg.oggpack_write(ref o, large[i], 32);
                }
                
                buffer = Ogg.oggpack_get_buffer(ref o);
                bytes = Ogg.oggpack_bytes(ref o);
                
                Ogg.oggpack_readinit(ref r, buffer, bytes);
                
                for (int i = 0; i < testSize2; i++) {
                    if (Ogg.oggpack_look(ref r, 32) == -1) {
                        throw new Exception("out of data. failed!");
                    }
                    
                    if (Ogg.oggpack_look(ref r, 32) != large[i]) {
                        throw new Exception("read incorrect value! " + Ogg.oggpack_look(ref r, 32) + " != " + large[1]);
                    }
                    
                    Ogg.oggpack_adv(ref r, 32);
                }
                
                if (Ogg.oggpack_bytes(ref r) != bytes) {
                    throw new Exception("leftover bytes after read!");
                }
                
                Console.WriteLine("ok.");
            }
            
            Console.Write("Small unclipped packing (LSb): ");
            ClipTest(testBuffer1, testSize1, 7, four, fourSize);
            Console.WriteLine("ok.");
            
            Console.Write("Large unclipped packing (LSb): ");
            ClipTest(testBuffer2, testSize2, 17, five, fiveSize);
            Console.WriteLine("ok.");
            
            Console.Write("Single bit unclipped packing (LSb): ");
            ClipTest(testBuffer3, testSize3, 1, six, sixSize);
            Console.WriteLine("ok.");
            
            Console.Write("Testing read past end (LSb): ");
            {
                byte *temp = (byte *)Ogg._ogg_malloc(8);
                
                try
                {
                    for (int i = 0; i < 8; i++) {
                        temp[i] = 0;
                    }
            
                    Ogg.oggpack_readinit(ref r, temp, 8);
                    
                    for (int i = 0; i < 64; i++) {
                        if (Ogg.oggpack_read(ref r, 1) != 0) {
                            throw new Exception("failed; got -1 prematurely.");
                        }
                    }
                    
                    if (Ogg.oggpack_look(ref r, 1) != -1 || Ogg.oggpack_read(ref r, 1) != -1) {
                        throw new Exception("failed; read past end without -1");
                    }
                    
                    for (int i = 0; i < 8; i++) {
                        temp[i] = 0;
                    }
                    
                    Ogg.oggpack_readinit(ref r, temp, 8);
                    
                    if (Ogg.oggpack_read(ref r, 30) != 0 || Ogg.oggpack_read(ref r, 16) != 0) {
                        throw new Exception("failed 2; got -1 prematurely.");
                    }
                    
                    if (Ogg.oggpack_look(ref r, 18) != 0 || Ogg.oggpack_look(ref r, 18) != 0) {
                        throw new Exception("failed 3; got -1 prematurely.");
                    }
                    
                    if (Ogg.oggpack_look(ref r, 19) != -1 || Ogg.oggpack_look(ref r, 19) != -1) {
                        throw new Exception("failed 3; got -1 prematurely.");
                    }

                    if (Ogg.oggpack_look(ref r, 32) != -1 || Ogg.oggpack_look(ref r, 32) != -1) {
                        throw new Exception("failed 3; got -1 prematurely.");
                    }

                    Ogg.oggpack_writeclear(ref o);
                    Console.WriteLine("ok.");
                }
                finally
                {
                    Ogg._ogg_free(temp);
                }
            }
            
            /********** lazy, cut-n-paste retest with MSb packing ***********/
            
            /* Test read/write together */
            /* Later we test against pregenerated bitstreams */
            
            Ogg.oggpackB_writeinit(ref o);
            
            Console.Write("Small preclipped packing (MSb): ");
            ClipTestB(testBuffer1, testSize1, 0, oneB, oneSize);
            Console.WriteLine("ok.");
            
            Console.Write("Null bit call (LSb): ");
            ClipTestB(testBuffer3, testSize3, 0, twoB, twoSize);
            Console.WriteLine("ok.");
            
            Console.Write("Large preclipped packing (LSb): ");
            ClipTestB(testBuffer2, testSize2, 0, threeB, threeSize);
            Console.WriteLine("ok.");
            
            Console.Write("32 bit precliiped packing (LSb): ");
            {
                Ogg.oggpackB_reset(ref o);
                
                for (int i = 0; i < testSize2; i++) {
                    Ogg.oggpackB_write(ref o, large[i], 32);
                }
                
                buffer = Ogg.oggpackB_get_buffer(ref o);
                bytes = Ogg.oggpackB_bytes(ref o);
                
                Ogg.oggpackB_readinit(ref r, buffer, bytes);
                
                for (int i = 0; i < testSize2; i++) {
                    if (Ogg.oggpackB_look(ref r, 32) == -1) {
                        throw new Exception("out of data. failed!");
                    }
                    
                    if (Ogg.oggpackB_look(ref r, 32) != large[i]) {
                        throw new Exception("read incorrect value! " + Ogg.oggpackB_look(ref r, 32) + " != " + large[1]);
                    }
                    
                    Ogg.oggpackB_adv(ref r, 32);
                }
                
                if (Ogg.oggpackB_bytes(ref r) != bytes) {
                    throw new Exception("leftover bytes after read!");
                }
                
                Console.WriteLine("ok.");
            }
            
            Console.Write("Small unclipped packing (LSb): ");
            ClipTestB(testBuffer1, testSize1, 7, fourB, fourSize);
            Console.WriteLine("ok.");
            
            Console.Write("Large unclipped packing (LSb): ");
            ClipTestB(testBuffer2, testSize2, 17, fiveB, fiveSize);
            Console.WriteLine("ok.");
            
            Console.Write("Single bit unclipped packing (LSb): ");
            ClipTestB(testBuffer3, testSize3, 1, sixB, sixSize);
            Console.WriteLine("ok.");
            
            Console.Write("Testing read past end (LSb): ");
            {
                byte *temp = (byte *)Ogg._ogg_malloc(8);
                
                try
                {
                    for (int i = 0; i < 8; i++) {
                        temp[i] = 0;
                    }
            
                    Ogg.oggpackB_readinit(ref r, temp, 8);
                    
                    for (int i = 0; i < 64; i++) {
                        if (Ogg.oggpackB_read(ref r, 1) != 0) {
                            throw new Exception("failed; got -1 prematurely.");
                        }
                    }
                    
                    if (Ogg.oggpackB_look(ref r, 1) != -1 || Ogg.oggpackB_read(ref r, 1) != -1) {
                        throw new Exception("failed; read past end without -1");
                    }
                    
                    for (int i = 0; i < 8; i++) {
                        temp[i] = 0;
                    }
                    
                    Ogg.oggpackB_readinit(ref r, temp, 8);
                    
                    if (Ogg.oggpackB_read(ref r, 30) != 0 || Ogg.oggpackB_read(ref r, 16) != 0) {
                        throw new Exception("failed 2; got -1 prematurely.");
                    }
                    
                    if (Ogg.oggpackB_look(ref r, 18) != 0 || Ogg.oggpackB_look(ref r, 18) != 0) {
                        throw new Exception("failed 3; got -1 prematurely.");
                    }
                    
                    if (Ogg.oggpackB_look(ref r, 19) != -1 || Ogg.oggpackB_look(ref r, 19) != -1) {
                        throw new Exception("failed 3; got -1 prematurely.");
                    }

                    if (Ogg.oggpackB_look(ref r, 32) != -1 || Ogg.oggpackB_look(ref r, 32) != -1) {
                        throw new Exception("failed 3; got -1 prematurely.");
                    }

                    Ogg.oggpackB_writeclear(ref o);
                    Console.WriteLine("ok.");
                }
                finally
                {
                    Ogg._ogg_free(temp);
                }
            }
        }
        
        static int iLog(uint v)
        {
            int ret = 0;
            
            while (v > 0) 
            {
                ret++;
                v >>= 1;
            }
            
            return ret;
        }
        
        static void ClipTest(uint[] b, int vals, int bits, uint[] comp, int compsize)
        {
            int bytes;
            byte *buffer; 
            
            Ogg.oggpack_reset(ref o);
            
            for(int i = 0; i < vals; i++) {
                Ogg.oggpack_write(ref o, b[i], bits > 0 ? bits : iLog(b[i]));
            }
            
            buffer = Ogg.oggpack_get_buffer(ref o);
            bytes = Ogg.oggpack_bytes(ref o);
            
            if (bytes != compsize) {
                throw new Exception("wrong number of bytes!");
            }
            
            for(int i = 0; i < bytes; i++) 
            {
                if (buffer[i] != comp[i]) 
                {
                    for(int j = 0; j < bytes; j++) {
                        Console.WriteLine(buffer[j] + " , " + comp[j]);
                    }
                    
                    throw new Exception("wrote incorrect value!");
                }
            }
            
            Ogg.oggpack_readinit(ref r, buffer, bytes);
            
            for(int i = 0; i < vals; i++)
            {
                int tbit = bits > 0 ? bits : iLog(b[i]);
                
                if (Ogg.oggpack_look(ref r, tbit) == -1) {
                    throw new Exception("out of data!");
                }
                
                if (Ogg.oggpack_look(ref r, tbit) != (b[i] & mask[tbit])) {
                    throw new Exception("looked at incorrect value!");
                }
                
                if (tbit == 1) {
                    if (Ogg.oggpack_look1(ref r) != (b[i] & mask[tbit])) {
                        throw new Exception("looked at single bit incorrect value!");
                    }
                }
                
                if (tbit == 1) {
                    if (Ogg.oggpack_read1(ref r) != (b[i] & mask[tbit])) {
                        throw new Exception("read incorrect single bit value!");
                    }
                }
                else if (Ogg.oggpack_read(ref r, tbit) != (b[i] & mask[tbit])) 
                {
                    throw new Exception("read incorrect value!");
                }
            }
            
            if (Ogg.oggpack_bytes(ref r) != bytes) {
                throw new Exception("leftover bytes after read!");
            }
        }
        
        static void ClipTestB(uint[] b, int vals, int bits, uint[] comp, int compsize)
        {
            int bytes;
            byte *buffer; 
            
            Ogg.oggpackB_reset(ref o);
            
            for(int i = 0; i < vals; i++) {
                Ogg.oggpackB_write(ref o, b[i], bits > 0 ? bits : iLog(b[i]));
            }
            
            buffer = Ogg.oggpackB_get_buffer(ref o);
            bytes = Ogg.oggpackB_bytes(ref o);
            
            if (bytes != compsize) {
                throw new Exception("wrong number of bytes!");
            }
            
            for(int i = 0; i < bytes; i++) 
            {
                if (buffer[i] != comp[i]) 
                {
                    for(int j = 0; j < bytes; j++) {
                        Console.WriteLine(buffer[j] + " , " + comp[j]);
                    }
                    
                    throw new Exception("wrote incorrect value!");
                }
            }
            
            Ogg.oggpackB_readinit(ref r, buffer, bytes);
            
            for(int i = 0; i < vals; i++)
            {
                int tbit = bits > 0 ? bits : iLog(b[i]);

                if (Ogg.oggpackB_look(ref r, tbit) == -1) {
                    throw new Exception("out of data!");
                }
                
                if (Ogg.oggpackB_look(ref r, tbit) != (b[i] & mask[tbit])) {
                    throw new Exception("looked at incorrect value!");
                }
                
                if (tbit == 1) {
                    if (Ogg.oggpackB_look1(ref r) != (b[i] & mask[tbit])) {
                        throw new Exception("looked at single bit incorrect value!");
                    }
                }
                
                if (tbit == 1) {
                    if (Ogg.oggpackB_read1(ref r) != (b[i] & mask[tbit])) {
                        throw new Exception("read incorrect single bit value!");
                    }
                }
                else if (Ogg.oggpackB_read(ref r, tbit) != (b[i] & mask[tbit])) 
                {
                    throw new Exception("read incorrect value!");
                }
            }
            
            if (Ogg.oggpackB_bytes(ref r) != bytes) {
                throw new Exception("leftover bytes after read!");
            }
        }        
    }
}
