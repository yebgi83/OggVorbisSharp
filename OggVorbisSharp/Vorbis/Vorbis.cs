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
        static private extern void CopyMemory(IntPtr dest, IntPtr source, int length);
        
        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
        static private extern void ZeroMemory(IntPtr dest, int length);
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
            
            if (v > 0) {
                --v;
            }
            
            while(v > 0) {
                ret++;
                v >>= 1;
            }
            
            return ret;
        }

        static int icount(uint v)
        {
            int ret = 0;
            
            while(v > 0) {
                ret += (int)(v & 1);
                v >>= 1;
            }
            
            return ret;
        }

        static public void *_ogg_malloc(int bytes)
        {
            return Marshal.AllocHGlobal(bytes).ToPointer();
        }
    
        static public void *_ogg_calloc(int num, int size)
        {
            void *ret = Marshal.AllocHGlobal(num * size).ToPointer();

            ZeroMemory((IntPtr)ret, num * size);
            return ret;
        }
        
        static public void *_ogg_realloc(IntPtr pv, int bytes)
        {
            return Marshal.ReAllocHGlobal(pv, (IntPtr)bytes).ToPointer();
        }

        static public void _ogg_free(void* ptr)
        {
            if (ptr != null)
            {
                Marshal.FreeHGlobal((IntPtr)ptr);
            }
        }
    }
}
