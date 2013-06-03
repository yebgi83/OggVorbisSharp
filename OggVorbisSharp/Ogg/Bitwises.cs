/********************************************************************
 *                                                                  *
 * THIS FILE IS PART OF THE Ogg CONTAINER SOURCE CODE.              *
 * USE, DISTRIBUTION AND REPRODUCTION OF THIS LIBRARY SOURCE IS     *
 * GOVERNED BY A BSD-STYLE SOURCE LICENSE INCLUDED WITH THIS SOURCE *
 * IN 'COPYING'. PLEASE READ THESE TERMS BEFORE DISTRIBUTING.       *
 *                                                                  *
 * THE OggVorbis SOURCE CODE IS (C) COPYRIGHT 1994-2010             *
 * by the Xiph.Org Foundation http://www.xiph.org/                  *
 *                                                                  *
 ********************************************************************

  function: packing variable sized words into an octet stream
  last mod: $Id: bitwise.c 18051 2011-08-04 17:56:39Z giles $

 ********************************************************************/

/* We're 'LSb' endian; if we write a word but read individual bits,
   then we'll read the lsb first */

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace OggVorbisSharp
{
    // Bitwise
    static public unsafe partial class Ogg
    {
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
        
        static uint[] mask8B = new uint[]
        {
            0x00, 0x80, 0xc0, 0xe0, 0xf0, 0xf8, 0xfc, 0xfe, 0xff
        };
        
        static public void oggpack_writeinit(ref oggpack_buffer b)
        {
            if (b != null)
            {
                oggpack_writeclear(ref b);
            
                b.buffer = (byte *)_ogg_malloc(BUFFER_INCREMENT);
                b.ptr = b.buffer;
                
                ZeroMemory((IntPtr)b.ptr, BUFFER_INCREMENT);
                
                b.storage = BUFFER_INCREMENT;
            }
        }
        
        static public void oggpackB_writeinit(ref oggpack_buffer b)
        {
            oggpack_writeinit(ref b);
        }
        
        static public int oggpack_writecheck(ref oggpack_buffer b)
        {
            if (b == null) {
                return -1;
            }
            
            if (b.ptr == null || b.storage == 0) {
                return -1;
            }
            else {
                return 0;
            }
        }
        
        static public int oggpackB_writecheck(ref oggpack_buffer b)
        {
            return oggpack_writecheck(ref b);
        }
        
        static public void oggpack_writetrunc(ref oggpack_buffer b, int bits)
        {
            int bytes = bits >> 3;
            
            if (b.ptr != null)
            {
                bits -= bytes * 8;
                b.ptr = b.buffer + bytes;
                b.endbit = bits;
                b.endbyte = bytes;
                *b.ptr &= (byte)mask[bits];
            }
        }
        
        static public void oggpackB_writetrunc(ref oggpack_buffer b, int bits)
        {
            int bytes = bits >> 3;
            
            if (b.ptr != null)
            {
                bits -= bytes * 8;
                b.ptr = b.buffer + bytes;
                b.endbit = bits;
                b.endbyte = bytes;
                *b.ptr &= (byte)mask8B[bits];
            }
        }
        
        static public void oggpack_write(ref oggpack_buffer b, uint value, int bits)
        {
            if (bits < 0 || bits > 32) 
            {
                goto err;
            }   
            
            if (b.endbyte >= b.storage - 4)
            {
                if (b.ptr == null) {
                    return;
                }
                
                if (b.storage > Int32.MaxValue - BUFFER_INCREMENT) {
                    goto err;
                }
                
                void *ret = _ogg_realloc((IntPtr)b.buffer, b.storage + BUFFER_INCREMENT);
                
                if (ret == null) {
                    goto err;
                }
                
                b.buffer = (byte *)ret;
                b.storage += BUFFER_INCREMENT;
                b.ptr = b.buffer + b.endbyte;
            }
            
            value &= mask[bits];
            bits += b.endbit;
            
            b.ptr[0] |= (byte)(value << b.endbit);
                
            if (bits >= 8) {
                b.ptr[1] = (byte)(value >> (8 - b.endbit));
            } 
            
            if (bits >= 16) {
                b.ptr[2] = (byte)(value >> (16 - b.endbit));
            } 
            
            if (bits >= 24) {
                b.ptr[3] = (byte)(value >> (24 - b.endbit));
            } 
            
            if (bits >= 32) 
            {
                if (b.endbit > 0) {
                    b.ptr[4] = (byte)(value >> (32 - b.endbit));
                }
                else {
                    b.ptr[4] = 0;
                }
            }
            
            b.endbyte += bits / 8;
            b.ptr += bits / 8;
            b.endbit = bits & 7;
            return;
            
        err:
            oggpack_writeclear(ref b);
        }
        
        static public void oggpackB_write(ref oggpack_buffer b, uint value, int bits) 
        {
            bool isError = false;
        
            try
            {
                if (bits < 0 || bits > 32) 
                {
                    isError = true;
                    return;
                }   
                
                if (b.endbyte >= b.storage - 4)
                {
                    if (b.ptr == null) {
                        return;
                    }
                    
                    if (b.storage > Int32.MaxValue - BUFFER_INCREMENT) {
                        isError = true;
                        return;
                    }
                    
                    void *ret = _ogg_realloc((IntPtr)b.buffer, b.storage + BUFFER_INCREMENT);
                    
                    if (ret == null) {
                        isError = true;
                        return;
                    }
                    
                    b.buffer = (byte *)ret;
                    b.storage += BUFFER_INCREMENT;
                    b.ptr = b.buffer + b.endbyte;
                }
                
                value = (value & mask[bits]) << (32 - bits);
                bits += b.endbit;
                
                b.ptr[0] |= (byte)(value >> (24 + b.endbit));
                    
                if (bits >= 8) {
                    b.ptr[1] = (byte)(value >> (16 + b.endbit));
                } 
                
                if (bits >= 16) {
                    b.ptr[2] = (byte)(value >> (8 + b.endbit));
                } 
                
                if (bits >= 24) {
                    b.ptr[3] = (byte)(value >> b.endbit);
                } 
                
                if (bits >= 32) 
                {
                    if (b.endbit > 0) {
                        b.ptr[4] = (byte)(value << (8 - b.endbit));
                    }
                    else {
                        b.ptr[4] = 0;
                    }
                }

                b.endbyte += bits / 8;
                b.ptr += bits / 8;
                b.endbit = bits & 7;
            }
            finally
            {
                if (isError == true) {
                    oggpack_writeclear(ref b);
                }
            }
        }
        
        static public void oggpack_writealign(ref oggpack_buffer b) 
        {
            int bits = 8 - b.endbit;
            
            if (bits < 8) {
                oggpack_write(ref b, 0, bits);
            }
        }
        
        static public void oggpackB_writealign(ref oggpack_buffer b)
        {
            int bits = 8 - b.endbit;
            
            if (bits < 8) {
                oggpackB_write(ref b, 0, bits);
            }
        }
        
        static public void oggpack_writecopy_helper(ref oggpack_buffer b, void *source, int bits, ogg_write_delegate w, int msb)
        {
            byte *ptr = (byte *)source;
            
            int bytes = bits / 8;
            bits -= bytes * 8;
             
            if (b.endbit > 0) 
            {
                /* unaligned copy. Do it the hard way */
                for (int i = 0; i < bytes; i++)
                {
                    w(ref b, (uint)ptr[i], 8);
                }
            }
            else 
            {
                /* aligned block copy */
                if (b.endbyte + bytes + 1 >= b.storage)
                {
                    if (b.ptr == null) 
                    {
                        goto err;
                    }
                    
                    if (b.endbyte + bytes + BUFFER_INCREMENT > b.storage) 
                    {
                        goto err;
                    }
                    
                    void *ret = _ogg_realloc((IntPtr)b.buffer, b.storage);
                    
                    if (ret == null) {
                        goto err;
                    }
                    
                    b.buffer = (byte *)ret;
                    b.ptr = b.buffer + b.endbyte;
                }
                
                CopyMemory((IntPtr)b.ptr, (IntPtr)source, bytes);
                
                b.ptr += bytes;
                b.endbyte += bytes;
                *b.ptr = 0;
            }
            
            if (bits != 0) 
            {
                if (msb != 0) {
                    w(ref b, (uint)(ptr[bytes] >> (8 - bits)), bits);
                } 
                else {
                    w(ref b, (uint)ptr[bytes], bits);
                }
            }
            
        err:
            oggpack_writeclear(ref b);
        }
        
        static public void oggpack_writecopy(ref oggpack_buffer b, void *source, int bits)
        {
            oggpack_writecopy_helper(ref b, source, bits, oggpack_write, 0);
        }
        
        static public void oggpackB_writecopy(ref oggpack_buffer b, void *source, int bits)
        {
            oggpack_writecopy_helper(ref b, source, bits, oggpackB_write, 1);
        }
        
        static public void oggpack_reset(ref oggpack_buffer b)
        {
            if (b.ptr == null) {
                return;
            }

            b.ptr = b.buffer;
            b.buffer[0] = 0;
            b.endbit = b.endbyte = 0;
        }
        
        static public void oggpackB_reset(ref oggpack_buffer b)
        {
            oggpack_reset(ref b);
        }
        
        static public void oggpack_writeclear(ref oggpack_buffer b)
        {
            if (b != null)
            {
                if (b.buffer != null) {
                    _ogg_free(b.buffer);
                }
                
                b.endbyte = 0;
                b.endbit = 0;
                
                b.buffer = null;
                b.ptr = null;
                
                b.storage = 0;
            }
        }
        
        static public void oggpackB_writeclear(ref oggpack_buffer b)
        {
            oggpack_writeclear(ref b);
        }
        
        static public void oggpack_readinit(ref oggpack_buffer b, byte *buf, int bytes)
        {
            if (b != null)
            {
                b.endbyte = 0;
                b.endbit = 0;
                
                b.buffer = b.ptr = buf;
                b.storage = bytes;
            }
        }
        
        static public void oggpackB_readinit(ref oggpack_buffer b, byte *buf, int bytes)
        {
            oggpack_readinit(ref b, buf, bytes);
        }
        
        /* Read in bits without advancing the bitptr; bits <= 32 */
        static public int oggpack_look(ref oggpack_buffer b, int bits)
        {
            uint ret;
            uint m;
            
            if (bits < 0 || bits > 32) 
            {
                return -1;
            }
            
            m = mask[bits];
            bits += b.endbit;

            if (b.endbyte >= b.storage - 4)
            {
                /* not the main path */
                if (b.endbyte > b.storage - ((bits + 7) >> 3))
                {
                    return -1;
                }
                else if (bits == 0)
                {
                    /* special case to avoid reading b->ptr[0], which might be past the end of the buffer; also skips some useless accounting */
                    return 0;
                }
            }
             
            ret = (uint)(b.ptr[0] >> b.endbit);
            
            if (bits > 8) {
                ret |= (uint)(b.ptr[1] << (8 - b.endbit));
            }
            
            if (bits > 16) {
                ret |= (uint)(b.ptr[2] << (16 - b.endbit));
            }
            
            if (bits > 24) {
                ret |= (uint)(b.ptr[3] << (24 - b.endbit));
            }
            
            if (bits > 32 && b.endbit > 0) {
                ret |= (uint)(b.ptr[4] << (32 - b.endbit));
            }
        
            return (int)(m & ret);
        }
        
        /* Read in bits without advancing the bitptr; bits <= 32 */
        static public int oggpackB_look(ref oggpack_buffer b, int bits)
        {
            uint ret;
            int m = 32 - bits;
            
            if (m < 0 || m > 32)
            {
                return -1;
            }
            
            bits += b.endbit;
            
            if (b.endbyte >= b.storage - 4) 
            {
                /* not the main path */
                if (b.endbyte > b.storage - ((bits + 7) >> 3)) 
                {
                    return -1;
                }
                else if (bits == 0) /* special case to avoid reading b->ptr[0], which might be past the end of the buffer; also skips some useless accounting */
                {
                    return 0;
                }
            }
            
            ret = (uint)(b.ptr[0] << (24 + b.endbit));
            
            if (bits > 8) {
                ret |= (uint)(b.ptr[1] << (16 + b.endbit));
            }
            
            if (bits > 16) {
                ret |= (uint)(b.ptr[2] << (8 + b.endbit));
            }
            
            if (bits > 24) {
                ret |= (uint)(b.ptr[3] << b.endbit);
            }
            
            if (bits > 32 && b.endbit > 0) {
                ret |= (uint)(b.ptr[4] >> (8 - b.endbit));
            }
        
            return (int)((ret >> (m >> 1)) >> ((m + 1) >> 1));
        }
        
        static public int oggpack_look1(ref oggpack_buffer b)
        {
            if (b.endbyte >= b.storage) {
                return -1;
            } 
            else {
                return (b.ptr[0] >> b.endbit) & 1;
            }
        }
        
        static public int oggpackB_look1(ref oggpack_buffer b)
        {
            if (b.endbyte >= b.storage) {
                return -1;
            } 
            else {
                return (b.ptr[0] >> (7 - b.endbit)) & 1;
            }
        }
        
        static public void oggpack_adv(ref oggpack_buffer b, int bits)
        {
            bits += b.endbit;
            
            if (b.endbyte > b.storage - ((bits + 7) >> 3))
            {
                goto overflow;
            }
            
            b.ptr += bits / 8;
            b.endbyte += bits / 8;
            b.endbit = bits & 7;
            return;
            
        overflow:
            b.ptr = null;
            b.endbyte = b.storage;
            b.endbit = 1;
        }
        
        static public void oggpackB_adv(ref oggpack_buffer b, int bits)
        {
            oggpack_adv(ref b, bits);
        }
        
        static public void oggpack_adv1(ref oggpack_buffer b)
        {
            if (++b.endbit > 7)
            {
                b.endbit = 0;
                b.ptr++;
                b.endbyte++;
            }
        }
        
        static public void oggpackB_adv1(ref oggpack_buffer b)
        {
            oggpack_adv1(ref b);
        }
        
        /* bits <= 32 */
        static public int oggpack_read(ref oggpack_buffer b, int bits)
        {
            uint ret;
            uint m;

            if (bits < 0 || bits > 32) 
            {
                goto err;
            }
            
            m = mask[bits];
            bits += b.endbit;
            
            if (b.endbyte >= b.storage - 4) 
            {
                /* not the main path */
                if (b.endbyte > b.storage - ((bits + 7) >> 3)) 
                {
                    goto overflow;
                }
                else if (bits <= 0) 
                {
                    /* special case to avoid reading b->ptr[0], which might be past the end of the buffer; also skips some useless accounting */
                    return 0;
                }
            }
            
            ret = (uint)(b.ptr[0] >> b.endbit);
            
            if (bits > 8) {
                ret |= (uint)(b.ptr[1] << (8 - b.endbit));
            } 
            
            if (bits > 16) {
                ret |= (uint)(b.ptr[2] << (16 - b.endbit));
            } 
            
            if (bits > 24) {
                ret |= (uint)(b.ptr[3] << (24 - b.endbit));
            } 
            
            if (bits > 32 && b.endbit > 0) {
                ret |= (uint)(b.ptr[4] << (32 - b.endbit));
            }    
            
            ret &= m;
            
            b.ptr += bits / 8;
            b.endbyte += bits / 8;
            b.endbit = bits & 7;
            return (int)ret;
        
        overflow: 
        err:
            b.ptr = null;
            b.endbyte = b.storage;
            b.endbit = 1;
            return -1;
        }
        
        /* bits <= 32 */
        static public int oggpackB_read(ref oggpack_buffer b, int bits)
        {
            uint ret;
            uint m = (uint)(32 - bits);
                
            if (m < 0 || m > 32) 
            {
                goto err;
            }
        
            bits += b.endbit;
            
            if (b.endbyte + 4 >= b.storage) 
            {
                /* not the main path */
                if (b.endbyte > b.storage - ((bits + 7) >> 3)) 
                {
                    goto overflow;
                }
                else if (bits == 0) 
                {
                    /* special case to avoid reading b->ptr[0], which might be past the end of the buffer; also skips some useless accounting */
                    return 0;
                }
            }
                
            ret = (uint)(b.ptr[0] << (24 + b.endbit));
            
            if (bits > 8) {
                ret |= (uint)(b.ptr[1] << (16 + b.endbit));
            } 
            
            if (bits > 16) {
                ret |= (uint)(b.ptr[2] << (8 + b.endbit));
            } 
            
            if (bits > 24) {
                ret |= (uint)(b.ptr[3] << b.endbit);
            } 
            
            if (bits > 32 && b.endbit > 0) {
                ret |= (uint)(b.ptr[4] >> (8 - b.endbit));
            }    
                
            ret = ((ret & 0xffffffff) >> (int)(m >> 1)) >> (int)((m + 1) >> 1);
                
            b.ptr += bits / 8;
            b.endbyte += bits / 8;
            b.endbit = bits & 7;
            return (int)ret;
        
        overflow:
        err:
            b.ptr = null;
            b.endbyte = b.storage;
            b.endbit = 1;
            return -1;
        }
        
        static public int oggpack_read1(ref oggpack_buffer b) 
        {
            uint ret;
        
            if (b.endbyte >= b.storage) 
            {
                goto overflow;
            }
            
            ret = (uint)((b.ptr[0] >> b.endbit) & 1);
            
            b.endbit++;
            
            if (b.endbit > 7)
            {
                b.endbit = 0;
                b.ptr++;
                b.endbyte++;
            }
            
            return (int)ret;
            
    overflow:
            b.ptr = null;
            b.endbyte = b.storage;
            b.endbit = 1;
            return -1;
        }
        
        static public int oggpackB_read1(ref oggpack_buffer b)
        {
            uint ret;
        
            if (b.endbyte >= b.storage) 
            {
                goto overflow;
            }
            
            ret = (uint)((b.ptr[0] >> (7 - b.endbit)) & 1);
            
            b.endbit++;
            
            if (b.endbit > 7)
            {
                b.endbit = 0;
                b.ptr++;
                b.endbyte++;
            }
            
            return (int)ret;
            
    overflow:
            b.ptr = null;
            b.endbyte = b.storage;
            b.endbit = 1;
            return -1;
        }
        
        static public int oggpack_bytes(ref oggpack_buffer b)
        {
            return b.endbyte + (b.endbit + 7) / 8;
        }
        
        static public int oggpack_bits(ref oggpack_buffer b)
        {
            return b.endbyte * 8 + b.endbit;
        }
        
        static public int oggpackB_bytes(ref oggpack_buffer b)
        {
            return oggpack_bytes(ref b);
        }
        
        static public int oggpackB_bits(ref oggpack_buffer b)
        {
            return oggpack_bits(ref b);
        }
        
        static public byte *oggpack_get_buffer(ref oggpack_buffer b)
        {
            return b.buffer;
        }
        
        static public byte *oggpackB_get_buffer(ref oggpack_buffer b)
        {
            return oggpack_get_buffer(ref b);
        }
    }
}