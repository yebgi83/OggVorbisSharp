using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace OggVorbisSharp
{
    // Native API
    static public unsafe partial class Vorbis
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static private extern void CopyMemory(void *dest, void *source, int length);
        
        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
        static private extern void ZeroMemory(void *dest, int length);
    }

    // Common
    static public unsafe partial class Vorbis
    {
        static public uint bitreverse(uint x)
        {
            x = ((x >> 16) & 0x0000ffff) | ((x << 16) & 0xffff0000);
            x = ((x >> 8) & 0x00ff00ff) | ((x << 8) & 0xff00ff00);
            x = ((x >> 4) & 0x0f0f0f0f) | ((x << 4) & 0xf0f0f0f0);
            x = ((x >> 2) & 0x33333333) | ((x << 2) & 0xcccccccc);
            return ((x >> 1) & 0x55555555) | ((x << 1) & 0xaaaaaaaa);
        }

        static public int ilog(uint v)
        {
            int ret = 0;

            while (v > 0)
            {
                ret++;
                v >>= 1;
            }

            return ret;
        }

        static int ilog2(uint v)
        {
            int ret = 0;

            if (v > 0)
            {
                --v;
            }

            while (v > 0)
            {
                ret++;
                v >>= 1;
            }

            return ret;
        }

        static int icount(uint v)
        {
            int ret = 0;

            while (v > 0)
            {
                ret += (int)(v & 1);
                v >>= 1;
            }

            return ret;
        }
        
        static void* _ogg_malloc(int bytes)
        {
            return Ogg._ogg_malloc(bytes);
        }

        static void* _ogg_calloc(int num, int size)
        {
            return Ogg._ogg_calloc(num, size);
        }
        
        static T[] _ogg_calloc_managed<T>(int num) where T : new()
        {
            return Ogg._ogg_calloc_managed<T>(num);
       }
        
        static void* _ogg_realloc(void* pv, int bytes)
        {
            return Ogg._ogg_realloc(pv, bytes);
        }

        static void _ogg_free(void* ptr)
        {
            Ogg._ogg_free(ptr);
        }
    }
}
