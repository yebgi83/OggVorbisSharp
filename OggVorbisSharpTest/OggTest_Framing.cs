using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using OggVorbisSharp;

namespace OggVorbisSharpTest
{
    static public unsafe class OggTest_Flaming
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static private extern void CopyMemory(void* dest, void* source, int length);

        static Ogg.ogg_stream_state os_en = new Ogg.ogg_stream_state();
        static Ogg.ogg_stream_state os_de = new Ogg.ogg_stream_state();
        static Ogg.ogg_sync_state oy = new Ogg.ogg_sync_state();

        static int sequence = 0;
        static int lastno = 0;

        /* 17 only */
        static readonly int[] head1_0 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x06,
            0x00, 0x00, 0x00, 0x00, 0x00 ,0x00, 0x00, 0x00,
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0,
            0x15, 0xed, 0xec, 0x91,
            1, 17
        };

        /* 17, 254, 255, 256, 500, 510, 600 byte, pad */
        static readonly int[] head1_1 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0,
            0x59, 0x10, 0x6c, 0x2c,
            1, 
            17
        };

        static readonly int[] head2_1 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x04,
            0x07, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0,
            0x89, 0x33, 0x85, 0xce,
            13, 
            254, 255, 0, 255, 1, 255, 245, 255, 255, 0, 255, 255, 90
        };

        /* nil packets; beginning, middle, end */
        static readonly int[] head1_2 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0,
            0xff, 0x7b, 0x23, 0x17,
            1, 
            0
        };

        static readonly int[] head2_2 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x04,
            0x07, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0,
            0x5c, 0x3f, 0x66, 0xcb,
            17, 
            17, 254, 255, 0, 0, 255, 1, 0, 255, 245, 255, 255, 0, 255, 255, 90, 0
        };

        /* large initial packet */
        static readonly int[] head1_3 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0, 
            0x01, 0x27, 0x31, 0xaa, 
            18, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 10
        };

        static readonly int[] head2_3 = 
        {
            0x4f,  0x67,  0x67,  0x53,  0,  0x04,  
            0x07,  0x08,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  
            0x01,  0x02,  0x03,  0x04,  1,  0,  0,  0,  
            0x7f,  0x4e,  0x8a,  0xd2,  
            4,  
            255,  4,  255,  0
        };

        /* continuing packet test */
        static readonly int[] head1_4 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0, 
            0xff, 0x7b, 0x23, 0x17, 
            1, 
            0
        };

        static readonly int[] head2_4 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x00, 
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0, 
            0xf8, 0x3c, 0x19, 0x79, 
            255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255
        };

        static readonly int[] head3_4 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x05, 
            0x07, 0x0c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 2, 0, 0, 0, 
            0x38, 0xe6, 0xb6, 0x28, 
            6, 
            255, 220, 255, 4, 255, 0
        };


        /* spill expansion test */
        static readonly int[] head1_4b = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0, 
            0xff, 0x7b, 0x23, 0x17, 
            1, 
            0
        };

        static readonly int[] head2_4b = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x00, 
            0x07, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0, 
            0xce, 0x8f, 0x17, 0x1a, 
            23, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 10, 255, 4, 255, 0, 0
        };

        static readonly int[] head3_4b = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x04, 
            0x07, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 2, 0, 0, 0, 
            0x9b, 0xb2, 0x50, 0xa1, 
            1, 
            0
        };

        /* page with the 255 segment limit */
        static readonly int[] head1_5 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0, 
            0xff, 0x7b, 0x23, 0x17, 
            1, 
            0
        };

        static readonly int[] head2_5 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x00, 
            0x07, 0xfc, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0, 
            0xed, 0x2a, 0x2e, 0xa7, 
            255, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10
        };

        static readonly int[] head3_5 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x04, 
            0x07, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 2, 0, 0, 0, 
            0x6c, 0x3b, 0x82, 0x3d, 
            1, 
            50
        };

        /* packet that overspans over an entire page */
        static readonly int[] head1_6 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0, 
            0xff, 0x7b, 0x23, 0x17, 
            1, 
            0
        };

        static readonly int[] head2_6 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x00, 
            0x07, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0, 
            0x68, 0x22, 0x7c, 0x3d, 
            255, 
            100, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255
        };

        static readonly int[] head3_6 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x01, 
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 
            0x01, 0x02, 0x03, 0x04, 2, 0, 0, 0, 
            0xf4, 0x87, 0xba, 0xf3, 
            255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255
        };

        static readonly int[] head4_6 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x05, 
            0x07, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 3, 0, 0, 0, 
            0xf7, 0x2f, 0x6c, 0x60, 
            5, 
            254, 255, 4, 255, 0
        };

        /* packet that overspans over an entire page */
        static readonly int[] head1_7 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x02, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 0, 0, 0, 0, 
            0xff, 0x7b, 0x23, 0x17, 
            1, 
            0
        };

        static readonly int[] head2_7 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x00, 
            0x07, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 1, 0, 0, 0, 
            0x68, 0x22, 0x7c, 0x3d, 
            255, 
            100, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255
        };

        static readonly int[] head3_7 = 
        {
            0x4f, 0x67, 0x67, 0x53, 0, 0x05, 
            0x07, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x01, 0x02, 0x03, 0x04, 2, 0, 0, 0, 
            0xd4, 0xe0, 0x60, 0xe5, 
            1, 
            0
        };

        static public void Test()
        {
            Ogg.ogg_stream_init(ref os_en, 0x04030201);
            Ogg.ogg_stream_init(ref os_de, 0x04030201);
            Ogg.ogg_sync_init(ref oy);

            /* Exercise each code path in the framing code.  Also verify that the checksums are working.  */
            {
                /* 17 only */
                int[] packets = { 17, -1 };
                int[][] headret = { head1_0, null };

                Console.Write("testing single page encoding... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* 17, 254, 255, 256, 500, 510, 600 byte, pad */
                int[] packets = { 17, 254, 255, 256, 500, 510, 600, -1 };
                int[][] headret = { head1_1, head2_1, null };

                Console.Write("testing basic page encoding... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* nil packets; beginning,middle,end */
                int[] packets = { 0, 17, 254, 255, 0, 256, 0, 500, 510, 600, 0, -1 };
                int[][] headret = { head1_2, head2_2, null };

                Console.Write("testing basic nil packets... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* large initial packet */
                int[] packets = { 4345, 259, 255, -1 };
                int[][] headret = { head1_3, head2_3, null };

                Console.Write("testing initial-packet lacing > 4k... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* continuing packet test; with page spill expansion, we have to overflow the lacing table. */
                int[] packets = { 0, 65500, 259, 255, -1 };
                int[][] headret = { head1_4, head2_4, head3_4, null };

                Console.Write("testing single packet page span... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* spill expand packet test */
                int[] packets = { 0, 4345, 259, 255, 0, 0, -1 };
                int[][] headret = { head1_4b, head2_4b, head3_4b, null };

                Console.Write("testing page spill expansion... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            /* page with the 255 segment limit */
            {
                int[] packets = 
                {
                    0, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
                    10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 50, -1
                };

                int[][] headret = { head1_5, head2_5, head3_5, null };

                Console.Write("testing max packet segments... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* packet that overspans over an entire page */
                int[] packets = { 0, 100, 130049, 259, 255, -1 };
                int[][] headret = { head1_6, head2_6, head3_6, head4_6, null };

                Console.Write("testing very large packets... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* test for the libogg 1.1.1 resync in large continuation bug found by Josh Coalson)  */
                int[] packets = { 0, 100, 130049, 259, 255, -1 };
                int[][] headret = { head1_6, head2_6, head3_6, head4_6, null };

                Console.Write("testing continuation resync in very large packets... ");
                test_pack(packets, headret, 100, 2, 3);
            }

            {
                /* term only page.  why not? */
                int[] packets = { 0, 100, 64770, -1 };
                int[][] headret = { head1_7, head2_7, head3_7, null };

                Console.Write("testing zero data page (1 nil packet)... ");
                test_pack(packets, headret, 0, 0, 0);
            }

            {
                /* build a bunch of pages for testing */
                byte* data = (byte*)Ogg._ogg_malloc(1024 * 1024);

                int[] pl = { 0, 1, 1, 98, 4079, 1, 1, 2954, 2057, 76, 34, 912, 0, 234, 1000, 1000, 1000, 300, -1 };
                int inptr = 0;

                Ogg.ogg_page[] og = new Ogg.ogg_page[5];
                
                for (int i = 0; i < 5; i++)
                {
                    og[i] = new Ogg.ogg_page();
                }
                
                Ogg.ogg_stream_reset(ref os_en);
                
                for (int i = 0; pl[i] != -1; i++)
                {
                    int len = pl[i];

                    Ogg.ogg_packet op = new Ogg.ogg_packet();

                    op.packet = data + inptr;
                    op.bytes = len;
                    op.e_o_s = (pl[i + 1] < 0) ? 1 : 0;
                    op.granulepos = (i + 1) * 1000;

                    for (int j = 0; j < len; j++)
                    {
                        data[inptr++] = (byte)(i + j);
                    }

                    Ogg.ogg_stream_packetin(ref os_en, ref op);
                }

                Ogg._ogg_free(data);

                /* retrieve finished pages */
                for (int i = 0; i < 5; i++)
                {
                    if (Ogg.ogg_stream_pageout(ref os_en, ref og[i]) == 0)
                    {
                        throw new Exception("Too few pages output building sync tests!");
                    }

                    copy_page(ref og[i]);
                }

                /* Test lost pages on pagein/packeetout: no rollback */
                {
                    Ogg.ogg_page temp = new Ogg.ogg_page();
                    Ogg.ogg_packet test = new Ogg.ogg_packet();

                    Console.Write("Testing loss of pages... ");

                    Ogg.ogg_sync_reset(ref oy);
                    Ogg.ogg_stream_reset(ref os_de);

                    for (int i = 0; i < 5; i++)
                    {
                        CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[i].header_len), og[i].header, og[i].header_len);
                        Ogg.ogg_sync_wrote(ref oy, og[i].header_len);

                        CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[i].body_len), og[i].body, og[i].body_len);
                        Ogg.ogg_sync_wrote(ref oy, og[i].body_len);
                    }

                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);
                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);
                    Ogg.ogg_sync_pageout(ref oy, ref temp);

                    /* skip */
                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);

                    /* do we get the expected results/packets? */
                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 0, 0, 0);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 1, 1, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 1, 2, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 98, 3, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 4079, 4, 5000);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != -1)
                    {
                        throw new Exception("Error : loss of page did not return error");
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 76, 9, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 34, 10, -1);
                    }

                    Console.WriteLine("ok.");
                }

                /* Test lost pages on pagein/packetout: rollback with continuation */
                {
                    Ogg.ogg_page temp = new Ogg.ogg_page();
                    Ogg.ogg_packet test = new Ogg.ogg_packet();

                    Console.Write("Testing loss of pages (rollback required)... ");

                    Ogg.ogg_sync_reset(ref oy);
                    Ogg.ogg_stream_reset(ref os_de);

                    for (int i = 0; i < 5; i++)
                    {
                        CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[i].header_len), og[i].header, og[i].header_len);
                        Ogg.ogg_sync_wrote(ref oy, og[i].header_len);

                        CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[i].body_len), og[i].body, og[i].body_len);
                        Ogg.ogg_sync_wrote(ref oy, og[i].body_len);
                    }

                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);
                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);
                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);
                    Ogg.ogg_sync_pageout(ref oy, ref temp);

                    /* skip */
                    Ogg.ogg_sync_pageout(ref oy, ref temp);
                    Ogg.ogg_stream_pagein(ref os_de, ref temp);

                    /* do we get the expected results/packets? */
                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 0, 0, 0);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 1, 1, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 1, 2, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 98, 3, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 4079, 4, 5000);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 1, 5, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 1, 6, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 2954, 7, -1);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 2057, 8, 9000);
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != -1)
                    {
                        throw new Exception("Error: loss of page did not return error");
                    }

                    if (Ogg.ogg_stream_packetout(ref os_de, ref test) != 1)
                    {
                        error();
                    }
                    else
                    {
                        check_packet(ref test, 300, 17, 18000);
                    }

                    Console.WriteLine("ok.");
                }

                /* the rest only test sync */
                {
                    Ogg.ogg_page og_de = new Ogg.ogg_page();

                    /* Test fractional page inputs: incomplete capture */
                    Console.Write("Testing sync on partial inputs... ");
                    Ogg.ogg_sync_reset(ref oy);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header, 3);
                    Ogg.ogg_sync_wrote(ref oy, 3);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    /* Test fracional page inputs: incomplete fixed header */
                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header + 3, 20);
                    Ogg.ogg_sync_wrote(ref oy, 20);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    /* Test fractional page inputs: incomplete header */
                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header + 23, 5);
                    Ogg.ogg_sync_wrote(ref oy, 5);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    /* Test fractional page inputs: incomplete body */
                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header + 28, og[1].header_len - 28);
                    Ogg.ogg_sync_wrote(ref oy, og[1].header_len - 28);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), og[1].body, 1000);
                    Ogg.ogg_sync_wrote(ref oy, 1000);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), (og[1].body + 1000), og[1].body_len - 1000);
                    Ogg.ogg_sync_wrote(ref oy, og[1].body_len - 1000);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    Console.WriteLine("ok.");
                }

                /* Test fractional page inputs: page + incomplete capture */
                {
                    Ogg.ogg_page og_de = new Ogg.ogg_page();

                    Console.Write("Testing sync on 1+partial inputs... ");
                    Ogg.ogg_sync_reset(ref oy);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header, og[1].header_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].header_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), og[1].body, og[1].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].body_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header, 20);
                    Ogg.ogg_sync_wrote(ref oy, 20);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header + 20, og[1].header_len - 20);
                    Ogg.ogg_sync_wrote(ref oy, og[1].header_len - 20);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), og[1].body, og[1].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].body_len);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    Console.WriteLine("ok.");
                }

                /* Test recapture : garbage + page */
                {
                    Ogg.ogg_page og_de = new Ogg.ogg_page();

                    Console.Write("Testing search for capture... ");
                    Ogg.ogg_sync_reset(ref oy);

                    /* 'garbage' */
                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), og[1].body, og[1].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].body_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header, og[1].header_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].header_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), og[1].body, og[1].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].body_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[2].header_len), og[2].header, 20);
                    Ogg.ogg_sync_wrote(ref oy, 20);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[2].header_len), og[2].header + 20, og[2].header_len - 20);
                    Ogg.ogg_sync_wrote(ref oy, og[2].header_len - 20);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[2].body_len), og[2].body, og[2].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[2].body_len);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    Console.WriteLine("ok.");
                }

                /* Test recapture: page + garbage + page */
                {
                    Ogg.ogg_page og_de = new Ogg.ogg_page();

                    Console.Write("Testing recapture... ");
                    Ogg.ogg_sync_reset(ref oy);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].header_len), og[1].header, og[1].header_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].header_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[1].body_len), og[1].body, og[1].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[1].body_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[2].header_len), og[2].header, og[2].header_len);
                    Ogg.ogg_sync_wrote(ref oy, og[2].header_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[2].header_len), og[2].header, og[2].header_len);
                    Ogg.ogg_sync_wrote(ref oy, og[2].header_len);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[2].body_len), og[2].body, og[2].body_len - 5);
                    Ogg.ogg_sync_wrote(ref oy, og[2].body_len - 5);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[3].header_len), og[3].header, og[3].header_len);
                    Ogg.ogg_sync_wrote(ref oy, og[3].header_len);

                    CopyMemory(Ogg.ogg_sync_buffer(ref oy, og[3].body_len), og[3].body, og[3].body_len);
                    Ogg.ogg_sync_wrote(ref oy, og[3].body_len);

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) > 0)
                    {
                        error();
                    }

                    if (Ogg.ogg_sync_pageout(ref oy, ref og_de) <= 0)
                    {
                        error();
                    }

                    Console.WriteLine("ok.");
                }

                for (int i = 0; i < 5; i++)
                {
                    free_page(ref og[i]);
                }
            }
        }

        static void check_packet(ref Ogg.ogg_packet op, int len, int no, int pos)
        {
            if (op.bytes != len)
            {
                throw new Exception("Incorrect packet length (" + op.bytes + " != " + len + ")");
            }

            if (op.granulepos != pos)
            {
                throw new Exception("Incorrect packet granpos (" + op.granulepos + " != " + pos + ")");
            }

            /* packet number just follows sequence/gap; adjust the input number for that */
            if (no == 0)
            {
                sequence = 0;
            }
            else
            {
                sequence++;

                if (no > lastno + 1)
                {
                    sequence++;
                }
            }

            lastno = no;

            if (op.packetno != sequence)
            {
                throw new Exception("Incorrect packet sequence " + op.packetno + " != " + sequence);
            }

            /* test data */
            for (int j = 0; j < op.bytes; j++)
            {
                if (op.packet[j] != (byte)(j + no))
                {
                    throw new Exception("Body data mismatch (1) at pos " + j + ": " + op.packet[j] + "=" + (byte)(j + no));
                }
            }
        }

        static void check_page(byte* data, ref int[] header, ref Ogg.ogg_page og)
        {
            /* test data */
            for (int j = 0; j < og.body_len; j++)
            {
                if (og.body[j] != data[j])
                {
                    throw new Exception("Body data mismatch (2) at pos " + j + ": " + og.body[j] + "=" + data[j]);
                }
            }

            /* test header */
            for (int j = 0; j < og.header_len; j++)
            {
                if (og.header[j] != header[j])
                {
                    throw new Exception("Header content mismatch at pos " + j + ": " + og.header[j] + "=" + header[j]);
                }
            }

            if (og.header_len != header[26] + 27)
            {
                throw new Exception("Header length incorrect! (" + og.header_len + " != " + header[26] + 27 + ")");
            }
        }

        static void print_header(ref Ogg.ogg_page og)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("HEADER:");
            stringBuilder.Append(" capture: ").Append(og.header[0]).Append(og.header[1]).Append(og.header[2]).Append(og.header[3]);
            stringBuilder.Append(" version: ").Append(og.header[4]);
            stringBuilder.Append(" flags: ").Append(og.header[5]).AppendLine();
            stringBuilder.Append(" granulepos: ").Append((og.header[9] << 24) | (og.header[8] << 16) | (og.header[7] << 8) | og.header[6]);
            stringBuilder.Append(" serialno: ").Append((og.header[17] << 24) | (og.header[16] << 16) | (og.header[15] << 8) | og.header[14]);
            stringBuilder.Append(" pageno: ").Append((og.header[21] << 24) | (og.header[20] << 16) | (og.header[19] << 8) | og.header[18]).AppendLine();
            stringBuilder.Append(" checksum: ").Append(og.header[22]).Append(og.header[23]).Append(og.header[24]).Append(og.header[25]).Append(og.header[26]);

            for (int j = 27; j < og.header_len; j++)
            {
                stringBuilder.Append(og.header[j]);
            }

            stringBuilder.AppendLine().AppendLine();

            Console.WriteLine(stringBuilder.ToString());
        }

        static void copy_page(ref Ogg.ogg_page og)
        {
            IntPtr temp = Marshal.AllocHGlobal(og.header_len);
            CopyMemory((void *)temp, og.header, og.header_len);
            og.header = (byte*)temp.ToPointer();

            temp = Marshal.AllocHGlobal(og.body_len);
            CopyMemory((void *)temp, og.body, og.body_len);
            og.body = (byte*)temp.ToPointer();
        }

        static void free_page(ref Ogg.ogg_page og)
        {
            Ogg._ogg_free(og.header);
            Ogg._ogg_free(og.body);
        }

        static void error()
        {
            throw new Exception("Error!");
        }

        static void test_pack(int[] pl, int[][] headers, int byteskip, int pageskip, int packetskip)
        {
            byte* data = (byte*)Ogg._ogg_malloc(1024 * 1024); /* for scripted test cases only */

            int inptr = 0;
            int outptr = 0;
            int deptr = 0;
            int depacket = 0;
            int granule_pos = 7;
            int pageno = 0;
            int packets = 0;
            int pageout = pageskip;
            int eosflag = 0;
            int bosflag = 0;
            int byteskipcount = 0;

            Ogg.ogg_stream_reset(ref os_en);
            Ogg.ogg_stream_reset(ref os_de);
            Ogg.ogg_sync_reset(ref oy);

            for (packets = 0; packets < packetskip; packets++)
            {
                depacket += pl[packets];
            }

            for (packets = 0; ; packets++)
            {
                if (pl[packets] == -1) break;
            }

            for (int i = 0; i < packets; i++)
            {
                /* construct a test packet */
                Ogg.ogg_packet op = new Ogg.ogg_packet();
                int len = pl[i];

                op.packet = data + inptr;
                op.bytes = len;
                op.e_o_s = (pl[i + 1] < 0) ? 1 : 0;
                op.granulepos = granule_pos;

                granule_pos += 1024;

                for (int j = 0; j < len; j++)
                {
                    data[inptr++] = (byte)(i + j);
                }

                /* submit the test packet */
                Ogg.ogg_stream_packetin(ref os_en, ref op);

                /* retrive any finished packet */
                {
                    Ogg.ogg_page og = new Ogg.ogg_page();

                    while (Ogg.ogg_stream_pageout(ref os_en, ref og) != 0)
                    {
                        /* We have a page. Check it carefully */
                        Console.Write(pageno + ", ");

                        if (headers[pageno] == null)
                        {
                            throw new Exception("coded too many pages.");
                        }

                        check_page(data + outptr, ref headers[pageno], ref og);

                        outptr += og.body_len;
                        pageno++;

                        if (pageskip != 0)
                        {
                            bosflag = 1;
                            pageskip--;
                            deptr += og.body_len;
                        }

                        /* have a complete page; submit it to sync/decode */
                        {
                            Ogg.ogg_page og_de = new Ogg.ogg_page();
                            Ogg.ogg_packet op_de = new Ogg.ogg_packet();
                            Ogg.ogg_packet op_de2 = new Ogg.ogg_packet();

                            byte *buf = Ogg.ogg_sync_buffer(ref oy, og.header_len + og.body_len);
                            byte *next = buf;

                            byteskipcount += og.header_len;

                            if (byteskipcount > byteskip)
                            {
                                CopyMemory(next, og.header, byteskipcount - byteskip);
                                next = next + (byteskipcount - byteskip);
                                byteskipcount = byteskip;
                            }

                            byteskipcount += og.body_len;

                            if (byteskipcount > byteskip)
                            {
                                CopyMemory(next, og.body, byteskipcount - byteskip);
                                next = next + (byteskipcount - byteskip);
                                byteskipcount = byteskip;
                            }

                            Ogg.ogg_sync_wrote(ref oy, (int)(next - buf));

                            while (true)
                            {
                                int ret = Ogg.ogg_sync_pageout(ref oy, ref og_de);

                                if (ret == 0)
                                {
                                    break;
                                }

                                if (ret < 0)
                                {
                                    continue;
                                }

                                /* got a page. Happy happy. Verify that it's good. */
                                Console.Write("(" + pageout + "), ");

                                check_page(data + deptr, ref headers[pageout], ref og_de);
                                deptr += og_de.body_len;
                                pageout++;

                                /* submit it to deconstitution */
                                Ogg.ogg_stream_pagein(ref os_de, ref og_de);

                                /* packets out? */
                                while (Ogg.ogg_stream_packetpeek(ref os_de, ref op_de2) > 0)
                                {
                                    Ogg.ogg_stream_packetpeek(ref os_de);
                                    Ogg.ogg_stream_packetout(ref os_de, ref op_de); /* just catching them all */

                                    /* verify peek and out match */
                                    if
                                    (
                                        op_de.b_o_s != op_de2.b_o_s ||
                                        op_de.bytes != op_de2.bytes ||
                                        op_de.e_o_s != op_de2.e_o_s ||
                                        op_de.granulepos != op_de2.granulepos ||
                                        op_de.packet != op_de2.packet ||
                                        op_de.packetno != op_de2.packetno
                                    )
                                    {
                                        throw new Exception("packetout != packetpeek! pos=" + depacket);
                                    }

                                    /* verify the packet! */
                                    /* check data */
                                    for (int j = 0; j < op_de.bytes; j++)
                                    {
                                        if ((data + depacket)[j] != op_de.packet[j])
                                        {
                                            throw new Exception("packet data mismatch in decode! pos=" + depacket);
                                        }
                                    }

                                    /* check bos flag */
                                    if (bosflag == 0 && op_de.b_o_s == 0)
                                    {
                                        throw new Exception("b_o_s flag incorrectly set on packet!");
                                    }

                                    bosflag = 1;
                                    depacket += op_de.bytes;

                                    /* check eos flag */
                                    if (eosflag != 0)
                                    {
                                        throw new Exception("Multiple decoded packets with eos flag!");
                                    }

                                    if (op_de.e_o_s != 0)
                                    {
                                        eosflag = 1;
                                    }

                                    /* check granulepos flag */
                                    if (op_de.granulepos != -1)
                                    {
                                        Console.Write(" granule:" + op_de.granulepos + " ");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Ogg._ogg_free(data);

            if (headers[pageno] != null)
            {
                throw new Exception("did not write last page!");
            }

            if (headers[pageout] != null)
            {
                throw new Exception("did not decode last page!");
            }

            if (inptr != outptr)
            {
                throw new Exception("encoded page data incomplete");
            }

            if (inptr != deptr)
            {
                throw new Exception("decoded page data incomplete");
            }

            if (inptr != depacket)
            {
                throw new Exception("decoded packet data incomplete");
            }

            if (eosflag == 0)
            {
                throw new Exception("Never got a packet with EOS set!");
            }

            Console.WriteLine("ok.");
        }
    }
}
