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
    // Framing
    static public unsafe partial class Ogg
    {
        static public int ogg_page_version(ref ogg_page og)
        {
            return (int)og.header[4];
        }
        
        static public int ogg_page_continued(ref ogg_page og)
        {
            return (int)og.header[5] & 0x01;
        }            
        
        static public int ogg_page_bos(ref ogg_page og)
        {
            return (int)og.header[5] & 0x02;
        }
        
        static public int ogg_page_eos(ref ogg_page og)
        {
            return (int)og.header[5] & 0x04;
        }
        
        static public long ogg_page_granulepos(ref ogg_page og)
        {
            return Marshal.ReadInt64((IntPtr)og.header, 6);
        }
        
        static public int ogg_page_serialno(ref ogg_page og) 
        {
            return Marshal.ReadInt32((IntPtr)og.header, 14);
        }
        
        static public int ogg_page_pageno(ref ogg_page og)
        {
            return Marshal.ReadInt32((IntPtr)og.header, 18);
        }
        
        static public int ogg_page_packets(ref ogg_page og)
        {
            int n = og.header[26];
            int count = 0;
            
            for(int i = 0; i < n; i++)
            {
                if (og.header[27 + i] < 255) {
                    count++;
                }
            }
            
            return count;
        }
        
        static public int ogg_stream_init(ref ogg_stream_state os, int serialno)
        {
            if (ogg_stream_clear(ref os) != 0)
            {
                return 1;
            }

            os.body_storage = 16 * 1024;
            os.lacing_storage = 1024;
            os.header = (byte *)_ogg_malloc(ogg_stream_state.HEADER_SIZE);
            os.body_data = (byte *)_ogg_malloc(os.body_storage * sizeof(byte));
            os.lacing_vals = (int *)_ogg_malloc(os.lacing_storage * sizeof(int));
            os.granule_vals = (long *)_ogg_malloc(os.lacing_storage * sizeof(long));
            
            if (os.header == null || os.body_data == null || os.lacing_vals == null || os.granule_vals == null) 
            {
                return -1;
            }
            
            os.serailno = serialno;
            return 0;
        }
        
        static public int ogg_stream_check(ref ogg_stream_state os)
        {
            if (os == null)
            {
                return -1;
            }
            else if (os.body_data == null)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }
        
        static public int ogg_stream_clear(ref ogg_stream_state os)
        {
            if (os == null)
            {
                return -1;
            }
            
            if (os.header != null) {
                _ogg_free(os.header);
            }
        
            if (os.body_data != null) {
                _ogg_free(os.body_data);
            }
            
            if (os.lacing_vals != null) {
                _ogg_free(os.lacing_vals);
            }
            
            if (os.granule_vals != null) {
                _ogg_free(os.granule_vals);
            }
            
            os.body_data = null;
            os.body_storage = 0;
            os.body_fill = 0;
            os.body_returned = 0;
        
            os.lacing_vals = null;
            os.granule_vals = null;
            
            os.lacing_storage = 1024;
            os.lacing_fill = 0;
            os.lacing_packet = 0;
            os.lacing_returned = 0;
            
            os.header = null;
            os.header_fill = 0;
            
            os.b_o_s = 0;
            os.e_o_s = 0;
            
            os.serailno = 0;
            os.pageno = 0;
            
            os.packetno = 0;
            os.granulepos = 0;
            return 0;
        }
        
        static public int ogg_stream_destroy(ref ogg_stream_state os)
        {   
            ogg_stream_clear(ref os);
            return 0;
        }
        
        /* Helpers for ogg_stream_encode; this keeps the structure and what's happening fairly clear */
        
        static int _os_body_expand(ref ogg_stream_state os, int needed)
        {
            if (os.body_storage <= os.body_fill + needed)
            {
                void *ret = _ogg_realloc((IntPtr)os.body_data, (os.body_storage + needed + 1024) * sizeof(byte));
                
                if (ret == null) {
                    ogg_stream_clear(ref os);
                    return -1;
                }
                
                os.body_storage += (needed + 1024);
                os.body_data = (byte *)ret;
            }
            
            return 0;
        }
        
        static int _os_lacing_expand(ref ogg_stream_state os, int needed)
        {
            if (os.lacing_storage <= os.lacing_fill + needed)
            {
                void *ret = _ogg_realloc((IntPtr)os.lacing_vals, (os.lacing_storage + needed + 32) * sizeof(int));

                if (ret == null) {
                    ogg_stream_clear(ref os);
                    return -1;
                }
                
                os.lacing_vals = (int *)ret;
                ret = _ogg_realloc((IntPtr)os.granule_vals, (os.lacing_storage + needed + 32) * sizeof(long));
                                
                if (ret == null) {
                    ogg_stream_clear(ref os);
                    return -1;
                }
                
                os.granule_vals = (long *)ret;
                os.lacing_storage += (needed + 32);
            }
            
            return 0;
        }
        
        /* checksum the page */
        /* Direct table CRC; note that this will be faster in the future if we perform the checksum simultaneously with other copies */
        static public void ogg_page_checksum_set(ref ogg_page og)
        {
            uint crc_reg = 0;
            
            /* safety; needed for API behavior, but not framing code */
            og.header[22] = 0;
            og.header[23] = 0;
            og.header[24] = 0;
            og.header[25] = 0;
            
            for(int i = 0; i < og.header_len; i++) {
                crc_reg = (crc_reg << 8) ^ crc_lookup[((crc_reg >> 24) & 0xff) ^ og.header[i]];
            }
            
            for(int i = 0; i < og.body_len; i++) {
                crc_reg = (crc_reg << 8) ^ crc_lookup[((crc_reg >> 24) & 0xff) ^ og.body[i]];
            }
            
            og.header[22] = (byte)(crc_reg & 0xff);
            og.header[23] = (byte)((crc_reg >> 8) & 0xff);
            og.header[24] = (byte)((crc_reg >> 16) & 0xff);
            og.header[25] = (byte)((crc_reg >> 24) & 0xff);
        }

        /* submit data to the internal buffer of the framing engine */
        static public int ogg_stream_iovecin(ref ogg_stream_state os, ref ogg_iovec_t[] iov, int count, int e_o_s, long granulepos)
        {
            int bytes = 0;
            int lacing_vals; 
            int i;
            
            if (ogg_stream_check(ref os) != 0) {
                return -1;
            }
            
            for (i = 0; i < count; ++i) {
                bytes += iov[i].iov_len;
            }
             
            lacing_vals = bytes / 255 + 1;
             
            if (os.body_returned > 0) 
            { 
                /* advance packet data according to the body_returned pointer. We had to keep it around to return a pointer into the buffer last call */
                os.body_fill -= os.body_returned;
                
                if (os.body_fill > 0) {
                    CopyMemory((IntPtr)os.body_data, (IntPtr)(os.body_data + os.body_returned), os.body_fill);
                }
                
                os.body_returned = 0;
            }
            
            /* make sure we have the buffer storage */
            if (_os_body_expand(ref os, bytes) != 0 || _os_lacing_expand(ref os, lacing_vals) != 0) {
                return -1;
            }
            
            /* Copy in the submitted packet.  Yes, the copy is a waste; this is the liability of overly clean abstraction for the time being. It will actually be fairly easy to eliminate the extra copy in the future */
            for (i = 0; i < count; i++) {
                CopyMemory((IntPtr)(os.body_data + os.body_fill), (IntPtr)iov[i].iov_base, iov[i].iov_len);
                os.body_fill += (int)iov[i].iov_len;
            }
            
            /* Store lacing vals for this packet */
            for (i = 0; i < lacing_vals - 1; i++) {
                os.lacing_vals[os.lacing_fill + i] = 255;
                os.granule_vals[os.lacing_fill + i] = os.granulepos;
            }
            
            os.lacing_vals[os.lacing_fill + i] = bytes % 255;
            os.granulepos = granulepos;
            os.granule_vals[os.lacing_fill + i] = granulepos;
            
            /* flag the first segment as the beginning of the packet */
            os.lacing_vals[os.lacing_fill] |= 0x100;
            os.lacing_fill += lacing_vals;
            
            /* for the sake of completeness */
            os.packetno++;
            
            if (e_o_s != 0) {
                os.e_o_s = 1;
            }
            
            return 0;
        }
        
        static public int ogg_stream_packetin(ref ogg_stream_state os, ref ogg_packet op) 
        {
            ogg_iovec_t[] iov = new ogg_iovec_t[1];
            
            iov[0].iov_base = op.packet;
            iov[0].iov_len = op.bytes;
            
            return ogg_stream_iovecin(ref os, ref iov, 1, op.e_o_s, op.granulepos);
        }
        
        /* Conditionally flush a page; force==0 will only flush nominal-size pages, force==1 forces us to flush a page regardless of page size so long as there's any data available at all. */
        static public int ogg_stream_flush_i(ref ogg_stream_state os, ref ogg_page og, int force, int nfill)
        {
            int i;
            int vals = 0;
            int maxvals = (os.lacing_fill > 255 ? 255 : os.lacing_fill);
            int bytes = 0;
            int acc = 0 ;
            
            long granule_pos = -1;
            
            if (ogg_stream_check(ref os) != 0) {
                return 0;
            }
            
            if (maxvals == 0) {
                return 0;
            }
            
            /* construct a page */
            /* decide how many segments to include */

            /* If this is the initial header case, the first page must only include the initial header packet */
            if (os.b_o_s == 0) /* 'initial header page' case */
            {
                granule_pos = 0;
                
                for (vals = 0; vals < maxvals; vals++)
                {
                    if ((os.lacing_vals[vals] & 0xff) < 255) {
                        vals++;
                        break;
                    }
                }
            }
            else 
            {
                int packets_done = 0;
                int packet_just_done = 0;
                
                for (vals = 0; vals < maxvals; vals++)
                {
                    if (acc > nfill && packet_just_done >= 4) {
                        force = 1;
                        break;
                    }
                    
                    acc += os.lacing_vals[vals] & 0xff;
                    
                    if ((os.lacing_vals[vals] & 0xff) < 255) {
                        granule_pos = os.granule_vals[vals];
                        packet_just_done = ++packets_done;
                    } else {
                        packet_just_done = 0;
                    }
                    
                    if (vals == 255) {
                        force = 1;
                    }
                }
            }
             
            if (force == 0) {
                return 0;
            }
            
            /* construct the header in temp storage */
            os.header[0] = (byte)'O';
            os.header[1] = (byte)'g';
            os.header[2] = (byte)'g';
            os.header[3] = (byte)'S';
            
            /* stream structure version */
            os.header[4] = 0;
            
            /* continued packet flag? */
            os.header[5] = 0;

            if ((os.lacing_vals[0] & 0x100) == 0) {
                os.header[5] += 0x01;
            }
            
            /* first page flag? */
            if (os.b_o_s == 0) {
                os.header[5] += 0x02;
            }
            
            /* last page flag? */
            if (os.e_o_s != 0 && os.lacing_fill == vals) {
                os.header[5] += 0x04;
            }
            
            os.b_o_s = 1;
            
            /* 64 bits of PCM position */
            for (i = 6; i < 14; i++) {
                os.header[i] = (byte)(granule_pos & 0xff);
                granule_pos >>= 8;
            }
            
            /* 32 bits of stream serial number */
            {
                long serialno = os.serailno;
                
                for (i = 14; i < 18; i++) {
                    os.header[i] = (byte)(serialno & 0xff);
                    serialno >>= 8;
                }
            }
        
            /* 32 bits of page counter (we have both counter and page header because this val can roll over) */
            if (os.pageno == -1) {
                /* because someone called stream_reset; this would be a strange thing to do in an encode stream, but it has plausible uses */
                os.pageno = 0;
            }
            
            long pageno = os.pageno++;
            
            for (i = 18; i < 22; i++) {
                os.header[i] = (byte)(pageno & 0xff);
                pageno >>= 8;
            }
            
            /* zero for computation; filled in later */
            os.header[22] = 0;
            os.header[23] = 0;
            os.header[24] = 0;
            os.header[25] = 0;
            
            /* segment table */ 
            os.header[26] = (byte)(vals & 0xff);
            
            for (i = 0; i < vals; i++) {
                os.header[i + 27] = (byte)(os.lacing_vals[i] & 0xff);
                bytes += os.header[i + 27];
            }
            
            /* set pointers in the ogg_page struct */
            og.header = os.header;
            og.header_len = vals + 27;
            os.header_fill = vals + 27;
            og.body = os.body_data + os.body_returned;
            og.body_len = bytes;
            
            /* advance the lacing data and set the body_returned pointer */
            os.lacing_fill -= vals;
            CopyMemory((IntPtr)os.lacing_vals, (IntPtr)(os.lacing_vals + vals), os.lacing_fill * sizeof(int));
            CopyMemory((IntPtr)os.granule_vals, (IntPtr)(os.granule_vals + vals), os.lacing_fill * sizeof(long));
            os.body_returned += bytes;
            
            /* calculate the checksum */
            ogg_page_checksum_set(ref og);

            /* done */                
            return 1;
        }
        
        /* This will flush remaining packets into a page (returning nonzero), even if there is not enough data to trigger a flush normally
          (undersized page). If there are no packets or partial packets to flush, ogg_stream_flush returns 0.  Note that ogg_stream_flush will
          try to flush a normal sized page like ogg_stream_pageout; a call to ogg_stream_flush does not guarantee that all packets have flushed.
          Only a return value of 0 from ogg_stream_flush indicates all packet data is flushed into pages.

          since ogg_stream_flush will flush the last page in a stream even if it's undersized, you almost certainly want to use ogg_stream_pageout
          (and *not* ogg_stream_flush) unless you specifically need to flush a page regardless of size in the middle of a stream. */
        
        static public int ogg_stream_flush(ref ogg_stream_state os, ref ogg_page og) 
        {
            return ogg_stream_flush_i(ref os, ref og, 1, 4096);
        }
        
        /* Like the above, but an argument is provided to adjust the nominal page size for applications which are smart enough to provide their
          own delay based flushing */
        
        static public int ogg_stream_flush_fill(ref ogg_stream_state os, ref ogg_page og, int nfill) 
        {
            return ogg_stream_flush_i(ref os, ref og, 1, nfill);
        }
        
        /* This constructs pages from buffered packet segments.  The pointers returned are to static buffers; do not free. The returned buffers are
          good only until the next call (using the same ogg_stream_state) */
        
        static public int ogg_stream_pageout(ref ogg_stream_state os, ref ogg_page og) 
        {
            int force = 0;
            
            if (ogg_stream_check(ref os) != 0) {
                return 0;
            }
            
            if ((os.e_o_s != 0 && os.lacing_fill != 0) || (os.lacing_fill != 0 && os.b_o_s == 0)) {
                force = 1;
            }
            
            return ogg_stream_flush_i(ref os, ref og, force, 4096);
        }
        
        /* Like the above, but an argument is provided to adjust the nominal page size for applications which are smart enough to provide their
          own delay based flushing */
        
        static public int ogg_stream_pageout_fill(ref ogg_stream_state os, ref ogg_page og, int nfill)
        {
            int force = 0;
            
            if (ogg_stream_check(ref os) != 0) {
                return 0;
            }
            
            if ((os.e_o_s != 0 && os.lacing_fill != 0) || (os.lacing_fill != 0 && os.b_o_s == 0)) {
                force = 1;
            }
            
            return ogg_stream_flush_i(ref os, ref og, force, nfill);            
        }
        
        static public int ogg_stream_eos(ref ogg_stream_state os) 
        {
            if (ogg_stream_check(ref os) != 0) {
                return 1;
            } else {
                return os.e_o_s;
            }
        }            
        
        /* DECODING PRIMITIVES: packet streaming layer */

        /* This has two layers to place more of the multi-serialno and paging control in the application's hands.  First, we expose a data buffer
          using ogg_sync_buffer().  The app either copies into the buffer, or passes it directly to read(), etc.  We then call
          ogg_sync_wrote() to tell how many bytes we just added.

          Pages are returned (pointers into the buffer in ogg_sync_state) by ogg_sync_pageout().  The page is then submitted to
          ogg_stream_pagein() along with the appropriate ogg_stream_state* (ie, matching serialno).  We then get raw packets out calling ogg_stream_packetout() with a
          ogg_stream_state. */

        /* initialize the struct to a known state */
        
        static public int ogg_sync_init(ref ogg_sync_state oy)
        {
            return ogg_sync_clear(ref oy);
        }
        
        static public int ogg_sync_clear(ref ogg_sync_state oy)
        {
            if (oy == null) 
            {
                return 1;
            }
        
            if (oy.data != null) {
                _ogg_free(oy.data);
            }
            
            oy.data = null;

            oy.storage = 0;
            oy.fill = 0;
            oy.returned = 0;

            oy.unsynced = 0;
            oy.headerbytes = 0;
            oy.bodybytes = 0;
            
            return 0;
        }
        
        static public int ogg_sync_destroy(ref ogg_sync_state oy)
        {
            if (oy != null) {
                ogg_sync_clear(ref oy);
            }
            
            return 0;
        }
        
        static public int ogg_sync_check(ref ogg_sync_state oy)
        {
            if (oy == null) {
                return -1;
            } 
            else if (oy.storage < 0) {
                return -1;
            }
            else {
                return 0;
            }
        }
        
        static public IntPtr ogg_sync_buffer(ref ogg_sync_state oy, int size)
        {
            if (ogg_sync_check(ref oy) != 0) {
                return IntPtr.Zero;
            }
            
            /* first, clear out any space that has been previously returned */
            if (oy.returned > 0) {
                oy.fill -= oy.returned;
                
                if (oy.fill > 0) {
                    CopyMemory((IntPtr)oy.data, (IntPtr)(oy.data + oy.returned), oy.fill);
                }
                
                oy.returned = 0;
            }
            
            if (size > oy.storage - oy.fill) {
                /* We need to extend the internal buffer */
                int newsize = size + oy.fill + 4096; /* an extra page to be nice */
                
                if (oy.data != null) {
                    oy.data = (byte *)_ogg_realloc((IntPtr)oy.data, newsize);
                } else {
                    oy.data = (byte *)_ogg_malloc(newsize);
                }
                
                oy.storage = newsize;
            }
    
            /* expose a segment at least as large as requested at the fill mark */
            return (IntPtr)(oy.data + oy.fill); 
        }
        
        static public int ogg_sync_wrote(ref ogg_sync_state oy, int bytes)
        {
            if (ogg_sync_check(ref oy) != 0) {
                return -1;
            }
            
            if (oy.fill + bytes > oy.storage) {
                return -1;
            }
            
            oy.fill += bytes;
            return 0;
        }
        
        static public int ogg_sync_pageseek(ref ogg_sync_state oy, ref ogg_page og)
        {
            bool isSyncFailed = false;
            
            byte *page = oy.data + oy.returned;
            byte *next = null;
            
            int bytes = oy.fill - oy.returned;
            
            if (ogg_sync_check(ref oy) != 0) {
                return 0;
            }
            
            if (oy.headerbytes == 0) 
            {
                int headerbytes;
                
                if (bytes < 27) {
                    return 0;
                }
                
                if 
                (
                    page[0] != 'O' || 
                    page[1] != 'g' || 
                    page[2] != 'g' || 
                    page[3] != 'S'
                )
                { 
                    isSyncFailed = true;
                }
                else
                {
                    headerbytes = page[26] + 27;
                    
                    if (bytes < headerbytes) {
                        return 0;
                    }
                    
                    for(int i = 0; i < page[26]; i++) {
                        oy.bodybytes += page[27 + i];
                    }
                    
                    oy.headerbytes = headerbytes;
                }
            }
            
            if (isSyncFailed == false)
            {
                if (oy.bodybytes + oy.headerbytes > bytes) {
                    return 0;
                }
                
                /* The whole test page is buffered. Verify the checksum */
                {
                    byte[] chksum = new byte[4];
                    
                    ogg_page log = new ogg_page();
                    
                    /* Grab the checksum bytes, set the header field to zero */
                    Marshal.Copy((IntPtr)(page + 22), chksum, 0, 4);
                    ZeroMemory((IntPtr)(page + 22), 4);
                                            
                    /* set up a temp page struct and recompute the checksum */
                    log.header = page;
                    log.header_len = oy.headerbytes;
                    log.body = page + oy.headerbytes;
                    log.body_len = oy.bodybytes;
                    
                    ogg_page_checksum_set(ref log);
                    
                    /* Compare */
                    if
                    (
                        chksum[0] != page[22] ||
                        chksum[1] != page[23] ||
                        chksum[2] != page[24] ||
                        chksum[3] != page[25]
                    )
                    {
                        /* D'oh.  Mismatch! Corrupt page (or miscapture and not a page at all) */
                        /* replace the computed checksum with the one actually read in */
                        Marshal.Copy(chksum, 0, (IntPtr)(page + 22), 4);
                        isSyncFailed = true;
                    }
                }
                
                /* yes, have a whole page all ready to go */
                if (isSyncFailed == false)
                {
                    IntPtr _page = (IntPtr)(oy.data + oy.returned);
                 
                    bytes = oy.headerbytes + oy.bodybytes;
                
                    if (og != null)
                    {
                        og.header = (byte *)_page;
                        og.header_len = oy.headerbytes;
                        og.body = (byte *)(_page.ToInt32() + oy.headerbytes);
                        og.body_len = oy.bodybytes;
                    }
                    
                    oy.unsynced = 0;
                    oy.returned += bytes;
                    oy.headerbytes = 0;
                    oy.bodybytes = 0;
                    
                    return bytes;
                }                
            }
            
            oy.headerbytes = 0;
            oy.bodybytes = 0;
            
            for (int i = 1; i < bytes; i++)
            {
                if (page[i] == 'O') {
                    next = (byte *)(page  + i);
                    break;
                }
            }
            
            if (next == null) {
                next = oy.data + oy.fill;
            }
            
            oy.returned = (int)(next - oy.data);
            
            return ((int)-(next - page));
        }
        
        /* sync the stream and get a page.  Keep trying until we find a page.
          Suppress 'sync errors' after reporting the first.

          return values:
          -1) recapture (hole in data)
           0) need more data
           1) page returned

          Returns pointers into buffered data; invalidated by next call to
          _stream, _clear, _init, or _buffer */
        
        static public int ogg_sync_pageout(ref ogg_sync_state oy, ref ogg_page og)
        {
            if (ogg_sync_check(ref oy) != 0) {
                return 0;
            }
            
            /* all we need to do is verify a page at the head of the stream
              buffer.  If it doesn't verify, we look for the next potential
              frame */
            
            for(;;) 
            {
                int ret = ogg_sync_pageseek(ref oy, ref og);
                
                if (ret > 0)
                {
                    /* have a page */
                    return 1;
                }
                
                if (ret == 0)
                {
                    /* need more data */
                    return 0;
                }
                
                if (oy.unsynced == 0) {
                    oy.unsynced = 1;
                    return -1;
                }
            }
        }
        
        /* add the incoming page to the stream state; we decompose the page
          into packet segments here as well. */
        
        static public int ogg_stream_pagein(ref ogg_stream_state os, ref ogg_page og) 
        {
            byte *header = og.header;
            byte *body = og.body;
            
            int bodysize = og.body_len;
            int version = ogg_page_version(ref og);
            int continued = ogg_page_continued(ref og);
            int bos = ogg_page_bos(ref og);
            int eos = ogg_page_eos(ref og);
            long granulepos = ogg_page_granulepos(ref og);
            int serialno = ogg_page_serialno(ref og);
            int pageno = ogg_page_pageno(ref og);
            int segments = header[26];
            
            if (ogg_stream_check(ref os) != 0) {
                return -1;
            }
            
            /* clean up 'returned data' */
            {
                int lr = os.lacing_returned;
                int br = os.body_returned;
                
                /* body data */
                if (br > 0)
                {
                    os.body_fill -= br;
                    
                    if (os.body_fill > 0) {
                        CopyMemory((IntPtr)os.body_data, (IntPtr)(os.body_data + br), os.body_fill);
                    }
                    
                    os.body_returned = 0;
                }
                
                /* segment table */
                if (lr > 0) 
                {
                    if (os.lacing_fill - lr > 0) {
                        CopyMemory((IntPtr)os.lacing_vals, (IntPtr)(os.lacing_vals + lr), (os.lacing_fill - lr) * sizeof(int));
                    } 
                    else {
                        CopyMemory((IntPtr)os.granule_vals, (IntPtr)(os.granule_vals + lr), (os.lacing_fill - lr) * sizeof(long));
                    }
                }
                
                os.lacing_fill -= lr;
                os.lacing_packet -= lr;
                os.lacing_returned = 0;
            }
            
            /* check the serial number */
            if (serialno != os.serailno) {
                return -1;
            }
            
            if (version > 0) {
                return -1;
            }
            
            if (_os_lacing_expand(ref os, segments + 1) != 0) {
                return -1;
            }
            
            /* are we in sequence? */
            if (pageno != os.pageno) {
                /* unroll previous partial packet (if any) */
                for (int i = os.lacing_packet; i < os.lacing_fill; i++) {
                    os.body_fill -= os.lacing_vals[i] & 0xff;
                }
                
                os.lacing_fill = os.lacing_packet;
                
                /* make a note of dropped data in segment table */
                if (os.pageno != -1) {
                    os.lacing_vals[os.lacing_fill++] = 0x400;
                    os.lacing_packet++;
                }
            }
            
            int segptr = 0;
            
            /* are we a 'continued packet' page? If so, we may need to skip some segments */
            if (continued != 0)
            {
                if (os.lacing_fill < 1 || os.lacing_vals[os.lacing_fill - 1] == 0x400) 
                {
                    bos = 0;
                    
                    for(segptr = 0; segptr < segments; segptr++)
                    {
                        int val = header[27 + segptr];
                        
                        body += val;
                        bodysize -= val;
                        
                        if (val < 255) {
                            segptr++;
                            break;
                        }
                    }
                }
            }
            
            if (bodysize > 0)
            {
                if (_os_body_expand(ref os, bodysize) != 0) {
                    return -1;
                }
                
                CopyMemory((IntPtr)(os.body_data + os.body_fill), (IntPtr)body, bodysize);
                os.body_fill += bodysize;
            }
            
            int saved = -1;
            
            while (segptr < segments) 
            {
                int val = og.header[27 + segptr];
                
                os.lacing_vals[os.lacing_fill] = val;
                os.granule_vals[os.lacing_fill] = -1;
                
                if (bos != 0) {
                    os.lacing_vals[os.lacing_fill] |= 0x100;
                    bos = 0;
                } 
                
                if (val < 255) {
                    saved = os.lacing_fill;
                }
                
                os.lacing_fill++;
                segptr++;
                
                if (val < 255) {
                    os.lacing_packet = os.lacing_fill;
                }
            }
            
            if (saved != -1) {
                os.granule_vals[saved] = granulepos;
            }
            
            if (eos != 0)
            {
                os.e_o_s = 1;
                
                if (os.lacing_fill > 0) {
                    os.lacing_vals[os.lacing_fill - 1] |= 0x200;
                }
            }
            
            os.pageno = pageno + 1;
            
            return 0;
        }
        
        /* clear things to an initial state. Good to call, eg, before seeking */
        static public int ogg_sync_reset(ref ogg_sync_state oy)
        {
            if (ogg_sync_check(ref oy) != 0) {
                return -1;
            }
            
            oy.fill = 0;
            oy.returned = 0;
            oy.unsynced = 0;
            oy.headerbytes = 0;
            oy.bodybytes = 0;
            
            return 0;
        }
        
        static public int ogg_stream_reset(ref ogg_stream_state os)
        {
            if (ogg_stream_check(ref os) != 0) {
                return -1;
            }
            
            os.body_fill = 0;
            os.body_returned = 0;
            
            os.lacing_fill = 0;
            os.lacing_packet = 0;
            os.lacing_returned = 0;
            
            os.header_fill = 0;
            
            os.e_o_s = 0;
            os.b_o_s = 0;
            os.pageno = -1;
            os.packetno = 0;
            os.granulepos = 0;
            
            return 0;
        }
        
        static public int ogg_stream_reset_serailno(ref ogg_stream_state os, int serialno)
        {
            if (ogg_stream_check(ref os) != 0) {
                return -1;
            }
            
            ogg_stream_reset(ref os);
            os.serailno = serialno;
            
            return 0;
        }
        
        static public int _packetout(ref ogg_stream_state os, ref ogg_packet op, int adv) 
        {
            /* The last part of decode. We have the stream broken into packet segments. Now we need to group them into packets (or return the out of sync markers) */
            int ptr = os.lacing_returned;
            
            if (os.lacing_packet <= ptr) {
                return 0;
            }
            
            /* we need to tell the codec there's a gap; it might need to handle previous packet dependencies. */
            if ((os.lacing_vals[ptr] & 0x400) != 0) 
            {
                os.lacing_returned++;
                os.packetno++;
                return -1;
            }
            
            /* just using peek as an inexpensive way to ask if there's a whole packet waiting */
            if (op == null && adv == 0)
            {
                return 1;
            }
            
            /* Gather the whole packet. We'll have no holes or a partial packet */
            {
                int size = os.lacing_vals[ptr] & 0xff;
                int bytes = size;
                int eos = os.lacing_vals[ptr] & 0x200;
                int bos = os.lacing_vals[ptr] & 0x100;
                
                while (size == 255)
                {
                    int val = os.lacing_vals[++ptr];
                    
                    size = val & 0xff;
                    
                    if ((val & 0x200) != 0) {
                        eos = 0x200;
                    }
                    
                    bytes += size;
                }
                
                if (op != null)
                {
                    op.e_o_s = eos;
                    op.b_o_s = bos;
                    op.packet = os.body_data + os.body_returned;
                    op.packetno = os.packetno;
                    op.granulepos = os.granule_vals[ptr];
                    op.bytes = bytes;
                }
                
                if (adv != 0) {
                    os.body_returned += bytes;
                    os.lacing_returned = ptr + 1;
                    os.packetno++;
                }
            }
            
            return 1;
        }
        
        static public int ogg_stream_packetout(ref ogg_stream_state os, ref ogg_packet op) 
        {
            if (ogg_stream_check(ref os) != 0 ) {
                return 0;
            }
            else {
                return _packetout(ref os, ref op, 1);
            }
        }
        
        static public int ogg_stream_packetpeek(ref ogg_stream_state os, ref ogg_packet op)
        {
            if (ogg_stream_check(ref os) != 0) {
                return 0;
            }
            else {
                return _packetout(ref os, ref op , 0);
            }
        }
        
        static public void ogg_packet_clear(ref ogg_packet op)
        {
            if (op.packet != null) {
                _ogg_free(op.packet);
            }
                
            op = new ogg_packet();
        }
    }
}
