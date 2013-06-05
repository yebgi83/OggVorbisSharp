/********************************************************************
 *                                                                  *
 * THIS FILE IS PART OF THE OggVorbis SOFTWARE CODEC SOURCE CODE.   *
 * USE, DISTRIBUTION AND REPRODUCTION OF THIS LIBRARY SOURCE IS     *
 * GOVERNED BY A BSD-STYLE SOURCE LICENSE INCLUDED WITH THIS SOURCE *
 * IN 'COPYING'. PLEASE READ THESE TERMS BEFORE DISTRIBUTING.       *
 *                                                                  *
 * THE OggVorbis SOURCE CODE IS (C) COPYRIGHT 1994-2009             *
 * by the Xiph.Org Foundation http://www.xiph.org/                  *
 *                                                                  *
 ********************************************************************

 function: stdio-based convenience library for opening/seeking/decoding
 last mod: $Id: vorbisfile.c 17573 2010-10-27 14:53:59Z xiphmont $

 ********************************************************************/

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using OggVorbisSharp;

/* A 'chained bitstream' is a Vorbis bitstream that contains more than
   one logical bitstream arranged end to end (the only form of Ogg
   multiplexing allowed in a Vorbis bitstream; grouping [parallel
   multiplexing] is not allowed in Vorbis) */

/* A Vorbis file can be played beginning to end (streamed) without
   worrying ahead of time about chaining (see decoder_example.c).  If
   we have the whole file, however, and want random access
   (seeking/scrubbing) or desire to know the total length/time of a
   file, we need to account for the possibility of chaining. */

/* We can handle things a number of ways; we can determine the entire
   bitstream structure right off the bat, or find pieces on demand.
   This example determines and caches structure for the entire
   bitstream, but builds a virtual decoder on the fly when moving
   between links in the chain. */

/* There are also different ways to implement seeking.  Enough
   information exists in an Ogg bitstream to seek to
   sample-granularity positions in the output.  Or, one can seek by
   picking some portion of the stream roughly in the desired area if
   we only want coarse navigation through the stream. */

/* Many, many internal helpers.  The intention is not to be confusing;
 * rampant duplication and monolithic function implementation would be
 * harder to understand anyway.  The high level functions are last.  Begin
 * grokking near the end of the file */

/* read a little more data from the file/pipe into the ogg_sync framer */

namespace OggVorbisSharp
{
    using size_t = Int32;

    // Native API
    static public unsafe partial class VorbisFile
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static private extern void CopyMemory(void* dest, void* source, int length);

        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
        static private extern void ZeroMemory(void* dest, int length);
    }

    // Delegates
    static public unsafe partial class VorbisFile
    {
        public delegate size_t ov_callback_read_func(void* ptr, size_t size, size_t nmemb, Stream datasource);
        public delegate int ov_callback_seek_func(Stream datasource, long offset, SeekOrigin whence);
        public delegate int ov_callback_close_func(Stream datasource);
        public delegate int ov_callback_tell_func(Stream datasource);
        public delegate int ov_filter_func(float** pcm, int channels, int samples, void* filter_param);
        
        delegate int ov_64_localseek_func(ref OggVorbis_File vf, long offset);
        delegate int ov_d_localseek_func(ref OggVorbis_File vf, double offset);
    }

    // Constants 
    static public unsafe partial class VorbisFile
    {
        public const int NOTOPEN = 0;
        public const int PARTOPEN = 1;
        public const int OPENED = 2;
        public const int STREAMSET = 3;
        public const int INITSET = 4;
    }

    // Types
    static public unsafe partial class VorbisFile
    {
        public class OggVorbis_File
        {
            public Stream datasource; /* File or memory stream */
            public int seekable;
            public long offset;
            public long end;
            public Ogg.ogg_sync_state oy = new Ogg.ogg_sync_state();

            /* If the FILE handle isn't seekable (eg, a pipe), only the current stream appears */
            public int links;
            public long* offsets;
            public long* dataoffsets;
            public int* serialnos;
            public long* pcmlengths; /* overloaded to maintain binary compatibility; x2 size, stores bothbeginning and end values */

            public Vorbis.vorbis_info[] vi;
            public Vorbis.vorbis_comment[] vc;

            /* Decoding working state local storage */
            public long pcm_offset;
            public int ready_state;
            public int current_serialno;
            public int current_link;

            public double bittrack;
            public double samptrack;

            public Ogg.ogg_stream_state os = new Ogg.ogg_stream_state(); /* take physical pages, weld into a logical stream of packets */
            public Vorbis.vorbis_dsp_state vd = new Vorbis.vorbis_dsp_state(); /* central working state for the packet->PCM decoder */
            public Vorbis.vorbis_block vb = new Vorbis.vorbis_block(); /* local working space for packet->PCM decode */

            public ov_callbacks callbacks;
        }

        /* The function prototypes for the callbacks are basically the same as for
        * the stdio functions fread, fseek, fclose, ftell.
        * The one difference is that the FILE * arguments have been replaced with
        * a void * - this is to be used as a pointer to whatever internal data these
        * functions might need. In the stdio case, it's just a FILE * cast to a void *
        *
        * If you use other functions, check the docs for these functions and return
        * the right values. For seek_func(), you *MUST* return -1 if the stream is
        * unseekable */

        public class ov_callbacks
        {
            public ov_callback_read_func read_func;
            public ov_callback_seek_func seek_func;
            public ov_callback_close_func close_func;
            public ov_callback_tell_func tell_func;
        }
    }

    // VorbisFile
    static public unsafe partial class VorbisFile
    {
        const int CHUNKSIZE = 65536; /* greater-than-page-size granularity seeking */
        const int READSIZE = 2048;   /* a smaller read size is needed for low-rate streaming. */

        static int _get_data(ref OggVorbis_File vf)
        {
            if (vf.callbacks.read_func == null)
            {
                return -1;
            }

            if (vf.datasource != null)
            {
                byte* buffer = Ogg.ogg_sync_buffer(ref vf.oy, READSIZE);
                int bytes = vf.callbacks.read_func(buffer, 1, READSIZE, vf.datasource);

                if (bytes > 0)
                {
                    Ogg.ogg_sync_wrote(ref vf.oy, bytes);
                }

                if (bytes == 0)
                {
                    return -1;
                }
                else
                {
                    return bytes;
                }
            }
            else
            {
                return 0;
            }
        }

        /* save a tiny smidge of verbosity to make the code more readable */
        static int _seek_helper(ref OggVorbis_File vf, long offset)
        {
            if (vf.datasource != null)
            {
                if (vf.callbacks.seek_func == null || vf.callbacks.seek_func(vf.datasource, offset, SeekOrigin.Begin) == -1)
                {
                    return Vorbis.OV_EREAD;
                }

                vf.offset = offset;
                Ogg.ogg_sync_reset(ref vf.oy);
            }
            else
            {
                /* shouldn't happen unless someone writes a broken callback */
                return Vorbis.OV_EFAULT;
            }

            return 0;
        }

        /* The read/seek functions track absolute position within the stream */

        /* from the head of the stream, get the next page.  boundary specifies
          if the function is allowed to fetch more data from the stream (and
          how much) or only use internally buffered data.

          boundary: -1) unbounded search
                     0) read no additional data; use cached only
                     n) search for a new page beginning for n bytes

          return:   <0) did not find a page (OV_FALSE, OV_EOF, OV_EREAD)
                     n) found a page at absolute offset n */

        static long _get_next_page(ref OggVorbis_File vf, ref Ogg.ogg_page og, long boundary)
        {
            if (boundary > 0)
            {
                boundary += vf.offset;
            }

            while (true)
            {
                if (boundary > 0 && vf.offset >= boundary)
                {
                    return Vorbis.OV_FALSE;
                }

                int more = Ogg.ogg_sync_pageseek(ref vf.oy, ref og);

                if (more < 0)
                {
                    /* skipped n bytes */
                    vf.offset -= more;
                }
                else
                {
                    if (more == 0)
                    {
                        /* send more paramedics */
                        if (boundary == 0)
                        {
                            return Vorbis.OV_FALSE;
                        }
                        else
                        {
                            int ret = _get_data(ref vf);

                            if (ret == 0)
                            {
                                return Vorbis.OV_EOF;
                            }
                            else if (ret < 0)
                            {
                                return Vorbis.OV_EREAD;
                            }
                        }
                    }
                    else
                    {
                        /* got a page.  Return the offset at the page beginning, advance the internal offset past the page end */
                        long ret = vf.offset;
                        vf.offset += more;

                        return ret;
                    }
                }
            }
        }

        /* find the latest page beginning before the current stream cursor
          position. Much dirtier than the above as Ogg doesn't have any
          backward search linkage.  no 'readp' as it will certainly have to read. */

        /* returns offset or OV_EREAD, OV_FAULT */

        static long _get_prev_page(ref OggVorbis_File vf, ref Ogg.ogg_page og)
        {
            long begin = vf.offset;
            long end = begin;
            long ret;
            long offset = -1;

            while (offset == -1)
            {
                begin -= CHUNKSIZE;
                
                if (begin < 0)
                {
                    begin = 0;
                }

                ret = _seek_helper(ref vf, begin);

                if (ret != 0)
                {
                    return ret;
                }

                while (vf.offset < end)
                {
                    og.header = null;
                    og.header_len = 0;

                    og.body = null;
                    og.body_len = 0;

                    ret = _get_next_page(ref vf, ref og, end - vf.offset);

                    if (ret == Vorbis.OV_EREAD)
                    {
                        return Vorbis.OV_EREAD;
                    }
                    else if (ret < 0)
                    {
                        break;
                    }
                    else
                    {
                        offset = ret;
                    }
                }
            }

            /* In a fully compliant, non-multiplexed stream, we'll still be
              holding the last page.  In multiplexed (or noncompliant streams),
              we will probably have to re-read the last page we saw */

            if (og.header_len == 0)
            {
                ret = _seek_helper(ref vf, offset);

                if (ret != 0)
                {
                    return ret;
                }

                ret = _get_next_page(ref vf, ref og, CHUNKSIZE);

                if (ret < 0)
                {
                    /* this shouldn't be possible */
                    return Vorbis.OV_EFAULT;
                }
            }

            return offset;
        }

        static void _add_serialno(ref Ogg.ogg_page og, ref int* serialno_list, ref int n)
        {
            int s = Ogg.ogg_page_serialno(ref og);
            n++;

            if (serialno_list != null)
            {
                serialno_list = (int*)Ogg._ogg_realloc(serialno_list, sizeof(int) * n);
            }
            else
            {
                serialno_list = (int*)Ogg._ogg_malloc(sizeof(int));
            }

            serialno_list[n - 1] = s;
        }

        /* returns nonzero if found */
        static int _lookup_serialno(int s, int* serialno_list, int n)
        {
            if (serialno_list != null)
            {
                while (n-- > 0)
                {
                    if (*serialno_list == s)
                    {
                        return 1;
                    }
                    else
                    {
                        serialno_list++;
                    }
                }
            }

            return 0;
        }

        static int _lookup_page_serialno(ref Ogg.ogg_page og, int* serialno_list, int n)
        {
            return _lookup_serialno(Ogg.ogg_page_serialno(ref og), serialno_list, n);
        }

        /* performs the same search as _get_prev_page, but prefers pages of the specified serial number. If a page of the specified serialno is
          spotted during the seek-back-and-read-forward, it will return the info of last page of the matching serial number instead of the very
          last page.  If no page of the specified serialno is seen, it will return the info of last page and alter *serialno.  */

        static long _get_prev_page_serial(ref OggVorbis_File vf, int* serial_list, int serial_n, ref int serialno, ref long granpos)
        {
            Ogg.ogg_page og = new Ogg.ogg_page();

            long begin = vf.offset;
            long end = begin;
            long ret;

            long prefoffset = -1;
            long offset = -1;

            int ret_serialno = -1;
            long ret_gran = -1;

            while (offset == -1)
            {
                begin -= CHUNKSIZE;

                if (begin < 0)
                {
                    begin = 0;
                }

                ret = _seek_helper(ref vf, begin);

                if (ret != 0)
                {
                    return ret;
                }

                while (vf.offset < end)
                {
                    ret = _get_next_page(ref vf, ref og, end - vf.offset);

                    if (ret == Vorbis.OV_EREAD)
                    {
                        return Vorbis.OV_EREAD;
                    }

                    if (ret < 0)
                    {
                        break;
                    }
                    else
                    {
                        ret_serialno = Ogg.ogg_page_serialno(ref og);
                        ret_gran = Ogg.ogg_page_granulepos(ref og);
                        offset = ret;

                        if (ret_serialno == serialno)
                        {
                            prefoffset = ret;
                            granpos = ret_gran;
                        }

                        if (_lookup_serialno(ret_serialno, serial_list, serial_n) != 0)
                        {
                            /* we fell off the end of the link, which means we seeked back too far and shouldn't have been looking in that link
                            to begin with.  If we found the preferred serial number, forget that we saw it. */
                            prefoffset = -1;
                        }
                    }
                }
            }

            /* we're not interested in the page... just the serialno and granpos. */
            if (prefoffset >= 0)
            {
                return prefoffset;
            }

            serialno = (int)ret_serialno;
            granpos = ret_gran;

            return (offset);
        }

        /* uses the local ogg_stream storage in vf; this is important for non-streaming input sources */

        static int _fetch_headers(ref OggVorbis_File vf, ref Vorbis.vorbis_info vi, ref Vorbis.vorbis_comment vc, ref int* serialno_list, ref int serialno_n, ref Ogg.ogg_page og_ptr)
        {
            Ogg.ogg_page og = new Ogg.ogg_page();
            Ogg.ogg_packet op = new Ogg.ogg_packet();

            int i, ret;
            int allbos = 0;

            if (og_ptr == null)
            {
                long llret = _get_next_page(ref vf, ref og, CHUNKSIZE);

                if (llret == Vorbis.OV_EREAD)
                {
                    return Vorbis.OV_EREAD;
                }
                else if (llret < 0)
                {
                    return Vorbis.OV_ENOTVORBIS;
                }
                else
                {
                    og_ptr = og;
                }
            }

            Vorbis.vorbis_info_init(ref vi);
            Vorbis.vorbis_comment_init(ref vc);
            vf.ready_state = OPENED;

            /* extract the serialnos of all BOS pages + the first set of vorbis headers we see in the link */

            while (Ogg.ogg_page_bos(ref og_ptr) != 0)
            {
                if (_lookup_page_serialno(ref og_ptr, serialno_list, serialno_n) != 0)
                {
                    /* a dupe serialnumber in an initial header packet set == invalid stream */
                    Ogg._ogg_free(serialno_list);

                    serialno_list = null;
                    serialno_n = 0;
                    ret = Vorbis.OV_EBADHEADER;

                    goto bail_header;
                }

                _add_serialno(ref og_ptr, ref serialno_list, ref serialno_n);
            
                if (vf.ready_state < STREAMSET)
                {
                    /* we don't have a vorbis stream in this link yet, so begin prospective stream setup. We need a stream to get packets */
                    Ogg.ogg_stream_reset_serialno(ref vf.os, Ogg.ogg_page_serialno(ref og_ptr));
                    Ogg.ogg_stream_pagein(ref vf.os, ref og_ptr);

                    if (Ogg.ogg_stream_packetout(ref vf.os, ref op) > 0 && Vorbis.vorbis_synthesis_idheader(ref op) != 0)
                    {
                        /* vorbis header; continue setup */
                        vf.ready_state = STREAMSET;

                        if ((ret = Vorbis.vorbis_synthesis_headerin(ref vi, ref vc, ref op)) != 0)
                        {
                            ret = Vorbis.OV_EBADHEADER;
                            goto bail_header;
                        }
                    }
                }

                /* get next page */
                {
                    long llret = _get_next_page(ref vf, ref og_ptr, CHUNKSIZE);

                    if (llret == Vorbis.OV_EREAD)
                    {
                        ret = Vorbis.OV_EREAD;
                        goto bail_header;
                    }
                    else if (llret < 0)
                    {
                        ret = Vorbis.OV_ENOTVORBIS;
                        goto bail_header;
                    }

                    /* if this page also belongs to our vorbis stream, submit it and break */
                    if (vf.ready_state == STREAMSET && vf.os.serialno == Ogg.ogg_page_serialno(ref og_ptr))
                    {
                        Ogg.ogg_stream_pagein(ref vf.os, ref og_ptr);
                        break;
                    }
                }
            }

            if (vf.ready_state != STREAMSET)
            {
                ret = Vorbis.OV_ENOTVORBIS;
                goto bail_header;
            }

            while (true)
            {
                i = 0;

                while (i < 2) /* get a page loop */
                {
                    while (i < 2) /* get a packet loop */
                    {
                        int result = Ogg.ogg_stream_packetout(ref vf.os, ref op);

                        if (result == 0)
                        {
                            break;
                        }
                        else if (result == -1)
                        {
                            ret = Vorbis.OV_EBADHEADER;
                            goto bail_header;
                        }

                        if ((ret = Vorbis.vorbis_synthesis_headerin(ref vi, ref vc, ref op)) != 0)
                        {
                            goto bail_header;
                        }

                        i++;
                    }

                    while (i < 2)
                    {
                        if (_get_next_page(ref vf, ref og_ptr, CHUNKSIZE) < 0)
                        {
                            ret = Vorbis.OV_EBADHEADER;
                            goto bail_header;
                        }

                        /* if this page belongs to the correct stream, go parse it */
                        if (vf.os.serialno == Ogg.ogg_page_serialno(ref og_ptr))
                        {
                            Ogg.ogg_stream_pagein(ref vf.os, ref og_ptr);
                            break;
                        }

                        /* if we never see the final vorbis headers before the link ends, abort */
                        if (Ogg.ogg_page_bos(ref og_ptr) != 0)
                        {
                            if (allbos != 0)
                            {
                                ret = Vorbis.OV_EBADHEADER;
                                goto bail_header;
                            }
                            else
                            {
                                allbos = 1;
                            }
                        }

                        /* otherwise, keep looking */
                    }
                }

                return 0;
            }

        bail_header:
            Vorbis.vorbis_info_clear(ref vi);
            Vorbis.vorbis_comment_clear(ref vc);
            vf.ready_state = OPENED;

            return ret;
        }

        static int _fetch_headers(ref OggVorbis_File vf, ref Vorbis.vorbis_info vi, ref Vorbis.vorbis_comment vc, ref int* serialno_list, ref int serialno_n)
        {
            Ogg.ogg_page og_null = null;
            return _fetch_headers(ref vf, ref vi, ref vc, ref serialno_list, ref serialno_n, ref og_null);
        }

        /* Starting from current cursor position, get initial PCM offset of next page.  Consumes the page in the process without decoding
          audio, however this is only called during stream parsing upon seekable open. */

        static long _initial_pcmoffset(ref OggVorbis_File vf, ref Vorbis.vorbis_info vi)
        {
            Ogg.ogg_page og = new Ogg.ogg_page();

            long accumulated = 0;
            int lastblock = -1;
            int result;
            int serialno = vf.os.serialno;

            while (true)
            {
                Ogg.ogg_packet op = new Ogg.ogg_packet();

                if (_get_next_page(ref vf, ref og, -1) < 0)
                {
                    /* should not be possible unless the file is truncated/mangled */
                    break;
                }

                if (Ogg.ogg_page_bos(ref og) != 0)
                {
                    break;
                }

                if (Ogg.ogg_page_serialno(ref og) != serialno)
                {
                    continue;
                }

                /* count blocksizes of all frames in the page */
                Ogg.ogg_stream_pagein(ref vf.os, ref og);

                while ((result = Ogg.ogg_stream_packetout(ref vf.os, ref op)) != 0)
                {
                    if (result > 0)
                    {
                        /* ignore holes */
                        int thisblock = Vorbis.vorbis_packet_blocksize(ref vi, ref op);

                        if (lastblock != -1)
                        {
                            accumulated += (lastblock + thisblock) >> 2;
                        }

                        lastblock = thisblock;
                    }
                }

                if (Ogg.ogg_page_granulepos(ref og) != -1)
                {
                    /* pcm offset of last packet on the first audio page */
                    accumulated = Ogg.ogg_page_granulepos(ref og) - accumulated;
                    break;
                }
            }

            /* less than zero?  Either a corrupt file or a stream with samples trimmed off the beginning, a normal occurrence; 
             in both cases set the offset to zero */

            if (accumulated < 0)
            {
                accumulated = 0;
            }

            return accumulated;
        }

        /* finds each bitstream link one at a time using a bisection search  (has to begin by knowing the offset of the lb's initial page).
          Recurses for each link so it can alloc the link storage after finding them all, then unroll and fill the cache at the same time */

        static int _bisect_forward_serialno(ref OggVorbis_File vf, long begin, long searched, long end, long endgran, int endserial, int* currentno_list, int currentnos, int m)
        {
            Ogg.ogg_page og = new Ogg.ogg_page();

            long pcmoffset;
            long dataoffset = searched;
            long endsearched = end;
            long next = end;
            long searchgran = -1;
            long last;
            int ret, serialno = vf.os.serialno;

            /* invariants:
              we have the headers and serialnos for the link beginning at 'begin'
              we have the offset and granpos of the last page in the file (potentially not a page we care about) */

            /* Is the last page in our list of current serialnumbers? */

            if (_lookup_serialno(endserial, currentno_list, currentnos) != 0)
            {
                /* last page is in the starting serialno list, so we've bisected down to (or just started with) a single link.  Now we need to
                 find the last vorbis page belonging to the first vorbis stream for this link. */

                while (endserial != serialno)
                {
                    endserial = serialno;
                    vf.offset = _get_prev_page_serial(ref vf, currentno_list, currentnos, ref endserial, ref endgran);
                }

                vf.links = m + 1;

                if (vf.offsets != null)
                {
                    Ogg._ogg_free(vf.offsets);
                }

                if (vf.serialnos != null)
                {
                    Ogg._ogg_free(vf.serialnos);
                }

                if (vf.dataoffsets != null)
                {
                    Ogg._ogg_free(vf.dataoffsets);
                }

                Array.Resize(ref vf.vi, vf.links);
                Array.Resize(ref vf.vc, vf.links);

                vf.offsets = (long*)Ogg._ogg_malloc((vf.links + 1) * sizeof(long));
                vf.serialnos = (int*)Ogg._ogg_malloc(vf.links * sizeof(int));
                vf.dataoffsets = (long*)Ogg._ogg_malloc(vf.links * sizeof(long));
                vf.pcmlengths = (long*)Ogg._ogg_malloc(vf.links * 2 * sizeof(long));

                vf.offsets[m + 1] = end;
                vf.offsets[m] = begin;
                vf.pcmlengths[m * 2 + 1] = (endgran < 0 ? 0 : endgran);
            }
            else
            {
                int* next_serialno_list = null;
                int next_serialnos = 0;

                Vorbis.vorbis_info vi = new Vorbis.vorbis_info();
                Vorbis.vorbis_comment vc = new Vorbis.vorbis_comment();

                /* the below guards against garbage seperating the last and first pages of two links. */
                while (searched < endsearched)
                {
                    long bisect;

                    if (endsearched - searched < CHUNKSIZE)
                    {
                        bisect = searched;
                    }
                    else
                    {
                        bisect = (searched + endsearched) / 2;
                    }

                    if (bisect != vf.offset)
                    {
                        ret = _seek_helper(ref vf, bisect);

                        if (ret != 0)
                        {
                            return ret;
                        }
                    }

                    last = _get_next_page(ref vf, ref og, -1);

                    if (last == Vorbis.OV_EREAD)
                    {
                        return Vorbis.OV_EREAD;
                    }

                    if (last < 0 || _lookup_page_serialno(ref og, currentno_list, currentnos) == 0)
                    {
                        endsearched = bisect;

                        if (last >= 0)
                        {
                            next = last;
                        }
                    }
                    else
                    {
                        searched = vf.offset;
                    }
                }

                /* Bisection point found */
                /* for the time being, fetch end PCM offset the simple way */

                {
                    int testserial = serialno + 1;

                    vf.offset = next;

                    while (testserial != serialno)
                    {
                        testserial = serialno;
                        vf.offset = _get_prev_page_serial(ref vf, currentno_list, currentnos, ref testserial, ref searchgran);
                    }
                }

                if (vf.offset != next)
                {
                    ret = _seek_helper(ref vf, next);

                    if (ret != 0)
                    {
                        return ret;
                    }
                }

                ret = _fetch_headers(ref vf, ref vi, ref vc, ref next_serialno_list, ref next_serialnos);

                if (ret != 0)
                {
                    return ret;
                }

                serialno = vf.os.serialno;
                dataoffset = vf.offset;

                /* this will consume a page, however the next bistection always starts with a raw seek */
                pcmoffset = _initial_pcmoffset(ref vf, ref vi);

                ret = _bisect_forward_serialno(ref vf, next, vf.offset, end, endgran, endserial, next_serialno_list, next_serialnos, m + 1);

                if (ret != 0)
                {
                    return ret;
                }

                Ogg._ogg_free(next_serialno_list);

                vf.offsets[m + 1] = next;
                vf.serialnos[m + 1] = serialno;
                vf.dataoffsets[m + 1] = dataoffset;

                vf.vi[m + 1] = vi;
                vf.vc[m + 1] = vc;

                vf.pcmlengths[m * 2 + 1] = searchgran;
                vf.pcmlengths[m * 2 + 2] = pcmoffset;
                vf.pcmlengths[m * 2 + 3] -= pcmoffset;

                if (vf.pcmlengths[m * 2 + 3] < 0)
                {
                    vf.pcmlengths[m * 2 + 3] = 0;
                }
            }

            return 0;
        }

        static int _make_decode_ready(ref OggVorbis_File vf)
        {
            if (vf.ready_state > STREAMSET)
            {
                return 0;
            }

            if (vf.ready_state < STREAMSET)
            {
                return Vorbis.OV_EFAULT;
            }

            if (vf.seekable != 0)
            {
                if (Vorbis.vorbis_synthesis_init(ref vf.vd, ref vf.vi[vf.current_link]) != 0)
                {
                    return Vorbis.OV_EBADLINK;
                }
            }
            else
            {
                if (Vorbis.vorbis_synthesis_init(ref vf.vd, ref vf.vi[0]) != 0)
                {
                    return Vorbis.OV_EBADLINK;
                }
            }

            Vorbis.vorbis_block_init(ref vf.vd, ref vf.vb);

            vf.ready_state = INITSET;
            vf.bittrack = 0.0f;
            vf.samptrack = 0.0f;

            return 0;
        }

        static long _open_seekable2(ref OggVorbis_File vf)
        {
            long dataoffset = vf.dataoffsets[0], end, endgran = -1;
            int endserial = vf.os.serialno;
            int serialno = vf.os.serialno;

            /* we're partially open and have a first link header state in storage in vf */
            /* fetch initial PCM offset */
            long pcmoffset = _initial_pcmoffset(ref vf, ref vf.vi[0]);

            /* we can seek, so set out learning all about this file */
            if (vf.callbacks.seek_func != null && vf.callbacks.tell_func != null)
            {
                vf.callbacks.seek_func(vf.datasource, 0, SeekOrigin.End);
                vf.offset = vf.end = vf.callbacks.tell_func(vf.datasource);
            }
            else
            {
                vf.offset = vf.end = -1;
            }

            /* If seek_func is implemented, tell_func must also be implemented */
            if (vf.end == -1)
            {
                return Vorbis.OV_EINVAL;
            }

            /* Get the offset of the last page of the physical bitstream, or, if we're lucky the last vorbis page of this link as most OggVorbis
             files will contain a single logical bitstream */
            end = _get_prev_page_serial(ref vf, vf.serialnos + 2, vf.serialnos[1], ref endserial, ref endgran);

            if (end < 0)
            {
                return end;
            }

            /* now determine bitstream structure recursively */
            if (_bisect_forward_serialno(ref vf, 0, dataoffset, vf.offset, endgran, endserial, vf.serialnos + 2, vf.serialnos[1], 0) < 0)
            {
                return Vorbis.OV_EREAD;
            }

            vf.offsets[0] = 0;
            vf.serialnos[0] = serialno;
            vf.dataoffsets[0] = dataoffset;
            vf.pcmlengths[0] = pcmoffset;
            vf.pcmlengths[1] -= pcmoffset;

            if (vf.pcmlengths[1] < 0)
            {
                vf.pcmlengths[1] = 0;
            }

            return ov_raw_seek(ref vf, dataoffset);
        }

        /* clear out the current logical bitstream decoder */

        static void _decode_clear(ref OggVorbis_File vf)
        {
            Vorbis.vorbis_dsp_clear(ref vf.vd);
            Vorbis.vorbis_block_clear(ref vf.vb);
            vf.ready_state = OPENED;
        }

        /* fetch and process a packet.  Handles the case where we're at a bitstream boundary and dumps the decoding machine.  If the decoding
          machine is unloaded, it loads it.  It also keeps pcm_offset up to date (seek and read both use this.  seek uses a special hack with readp).

          return: < 0) error, OV_HOLE (lost packet) or OV_EOF
                    0) need more data (only if readp==0)
                    1) got a packet */

        static int _fetch_and_process_packet(ref OggVorbis_File vf, ref Ogg.ogg_packet op_in, int readp, int spanp)
        {
            Ogg.ogg_page og = new Ogg.ogg_page();

            /* handle one packet.  Try to fetch it from current stream state */
            /* extract packets from page */
            while (true)
            {
                if (vf.ready_state == STREAMSET)
                {
                    int ret = _make_decode_ready(ref vf);

                    if (ret < 0)
                    {
                        return ret;
                    }
                }

                /* process a packet if we can. */

                if (vf.ready_state == INITSET)
                {
                    int hs = Vorbis.vorbis_synthesis_halfrate_p(ref vf.vi[0]);

                    while (true)
                    {
                        Ogg.ogg_packet op = new Ogg.ogg_packet();
                        Ogg.ogg_packet op_ptr = (op_in != null ? op_in : op);

                        int result = Ogg.ogg_stream_packetout(ref vf.os, ref op_ptr);
                        long granulepos;

                        op_in = null;

                        if (result == -1)
                        {
                            return Vorbis.OV_HOLE; /* hole in the data. */
                        }

                        if (result > 0)
                        {
                            /* got a packet.  process it */
                            granulepos = op_ptr.granulepos;

                            /* lazy check for lazy header handling.  The header packets aren't audio, so if/when we submit them,
                            vorbis_synthesis will reject them */

                            if (Vorbis.vorbis_synthesis(ref vf.vb, ref op_ptr) == 0)
                            {
                                /* suck in the synthesis data and track bitrate */
                                {
                                    int oldsamples = Vorbis.vorbis_synthesis_pcmout(ref vf.vd);

                                    /* for proper use of libvorbis within libvorbisfile, oldsamples will always be zero. */
                                    if (oldsamples != 0)
                                    {
                                        return Vorbis.OV_EFAULT;
                                    }

                                    Vorbis.vorbis_synthesis_blockin(ref vf.vd, ref vf.vb);
                                    vf.samptrack += Vorbis.vorbis_synthesis_pcmout(ref vf.vd) << hs;
                                    vf.bittrack += op_ptr.bytes * 8;
                                }

                                /* update the pcm offset. */
                                if (granulepos != -1 && op_ptr.e_o_s == 0)
                                {
                                    int link = (vf.seekable != 0 ? vf.current_link : 0);
                                    int i, samples;

                                    /* this packet has a pcm_offset on it (the last packet completed on a page carries the offset) After processing
                                   (above), we know the pcm position of the *last* sample ready to be returned. Find the offset of the *first*

                                   As an aside, this trick is inaccurate if we begin reading anew right at the last page; the end-of-stream
                                   granulepos declares the last frame in the stream, and the last packet of the last page may be a partial frame.
                                   So, we need a previous granulepos from an in-sequence page to have a reference point.  Thus the !op_ptr->e_o_s clause
                                   above */

                                    if (vf.seekable != 0 && link > 0)
                                    {
                                        granulepos -= vf.pcmlengths[link * 2];
                                    }

                                    /* actually, this shouldn't be possible here unless the stream is very broken */

                                    if (granulepos < 0)
                                    {
                                        granulepos = 0;
                                    }

                                    samples = Vorbis.vorbis_synthesis_pcmout(ref vf.vd) << hs;

                                    granulepos -= samples;

                                    for (i = 0; i < link; i++)
                                    {
                                        granulepos += vf.pcmlengths[i * 2 + 1];
                                    }

                                    vf.pcm_offset = granulepos;
                                }

                                return 1;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (vf.ready_state >= OPENED)
                {
                    long ret;

                    while (true)
                    {
                        /* the loop is not strictly necessary, but there's no sense in doing the extra checks of the larger loop for the common
                          case in a multiplexed bistream where the page is simply part of a different logical bitstream; keep reading until
                          we get one with the correct serialno */

                        if (readp == 0)
                        {
                            return 0;
                        }

                        if ((ret = _get_next_page(ref vf, ref og, -1)) < 0)
                        {
                            return Vorbis.OV_EOF; /* eof. leave unitialized */
                        }

                        /* bitrate tracking; add the header's bytes here, the body bytes are done by packet above */
                        vf.bittrack += og.header_len * 8;

                        if (vf.ready_state == INITSET)
                        {
                            if (vf.current_serialno != Ogg.ogg_page_serialno(ref og))
                            {
                                /* two possibilities:
                               1) our decoding just traversed a bitstream boundary
                               2) another stream is multiplexed into this logical section */

                                if (Ogg.ogg_page_bos(ref og) != 0)
                                {
                                    /* boundary case */
                                    if (spanp == 0)
                                    {
                                        return Vorbis.OV_EOF;
                                    }

                                    _decode_clear(ref vf);

                                    if (vf.seekable == 0)
                                    {
                                        Vorbis.vorbis_info_clear(ref vf.vi[0]);
                                        Vorbis.vorbis_comment_clear(ref vf.vc[0]);
                                    }

                                    break;
                                }
                                else
                                {
                                    continue; /* possibility #2 */
                                }
                            }
                        }

                        break;
                    }
                }

                /* Do we need to load a new machine before submitting the page? */

                /* This is different in the seekable and non-seekable cases.

                 In the seekable case, we already have all the header information loaded and cached; we just initialize the machine
                 with it and continue on our merry way.

                 In the non-seekable (streaming) case, we'll only be at a boundary if we just left the previous logical bitstream and
                 we're now nominally at the header of the next bitstream */

                if (vf.ready_state != INITSET)
                {
                    int link;

                    if (vf.ready_state < STREAMSET)
                    {
                        if (vf.seekable != 0)
                        {
                            int serialno = Ogg.ogg_page_serialno(ref og);

                            /* match the serialno to bitstream section.  We use this rather than offset positions to avoid problems near logical bitstream boundaries */

                            for (link = 0; link < vf.links; link++)
                            {
                                if (vf.serialnos[link] == serialno)
                                {
                                    break;
                                }
                            }

                            /* not the desired Vorbis bitstream section; keep trying */

                            if (link == vf.links)
                            {
                                continue;
                            }

                            vf.current_serialno = serialno;
                            vf.current_link = link;

                            Ogg.ogg_stream_reset_serialno(ref vf.os, vf.current_serialno);
                            vf.ready_state = STREAMSET;
                        }
                        else
                        {
                            int serialno_n_zero = 0;

                            /* we're streaming */
                            /* fetch the three header packets, build the info struct */
                            int *serialno_list_dummy = null;
                            int ret = _fetch_headers(ref vf, ref vf.vi[0], ref vf.vc[0], ref serialno_list_dummy, ref serialno_n_zero, ref og);

                            if (ret != 0)
                            {
                                return ret;
                            }

                            vf.current_serialno = vf.os.serialno;
                            vf.current_link++;
                            link = 0;
                        }
                    }
                }

                /* the buffered page is the data we want, and we're ready for it; add it to the stream state */
                Ogg.ogg_stream_pagein(ref vf.os, ref og);
            }
        }

        static int _fetch_and_process_packet(ref OggVorbis_File vf, int readp, int spanp)
        {
            Ogg.ogg_packet op_null = null;
            return _fetch_and_process_packet(ref vf, ref op_null, readp, spanp);
        }

        /* read64_wrap */

        static int _read64_wrap(void* ptr, size_t size, size_t nmemb, Stream stream)
        {
            if (stream == null)
            {
                return -1;
            }
            else
            {
                int i;
                byte* temp_ptr = (byte*)ptr;

                for (i = 0; i < size * nmemb; i++)
                {
                    int result = stream.ReadByte();

                    if (result == -1)
                    {
                        break;
                    }
                    else
                    {
                        *temp_ptr = (byte)result;
                        temp_ptr++;
                    }
                }

                return i;
            }
        }

        /* _seek64_wrap - makes enable to access both file and memory stream. */

        static int _seek64_wrap(Stream stream, long off, SeekOrigin whence)
        {
            if (stream == null)
            {
                return -1;
            }
            else
            {
                return (int)stream.Seek(off, whence);
            }
        }

        static int _close64_wrap(Stream stream)
        {
            if (stream == null)
            {
                return -1;
            }
            else
            {
                stream.Close();
                return 0;
            }
        }

        static int _tell64_wrap(Stream stream)
        {
            if (stream == null)
            {
                return -1;
            }
            else
            {
                return (int)stream.Position;
            }
        }

        static int _ov_open1(Stream stream, ref OggVorbis_File vf, byte* initial, int ibytes, ov_callbacks callbacks)
        {
            int offsettest = ((stream != null && callbacks.seek_func != null) ? callbacks.seek_func(stream, 0, SeekOrigin.Current) : -1);
            int* serialno_list = null;
            int serialno_list_size = 0;
            int ret;

            if (ov_clear(ref vf) != 0)
            {
                return -1;
            }

            vf.datasource = stream;
            vf.callbacks = callbacks;

            /* init the framing state */
            Ogg.ogg_sync_init(ref vf.oy);

            /* perhaps some data was previously read into a buffer for testing against other stream types.  Allow initialization from thispreviously read data (especially as we may be reading from a non-seekable stream) */
            if (initial != null)
            {
                byte* buffer = Ogg.ogg_sync_buffer(ref vf.oy, ibytes);

                CopyMemory(buffer, initial, ibytes);
                Ogg.ogg_sync_wrote(ref vf.oy, ibytes);
            }

            /* can we seek? Stevens suggests the seek test was portable */
            if (offsettest != -1)
            {
                vf.seekable = 1;
            }

            /* No seeking yet; Set up a 'single' (current) logical bitstream    entry for partial open */
            vf.links = 1;
            vf.vi = Ogg._ogg_calloc_managed<Vorbis.vorbis_info>(vf.links);
            vf.vc = Ogg._ogg_calloc_managed<Vorbis.vorbis_comment>(vf.links);
            Ogg.ogg_stream_init(ref vf.os, -1); /* fill in the serialno later */

            /* Fetch all BOS pages, store the vorbis header and all seen serial numbers, load subsequent vorbis setup headers */
            {
                if ((ret = _fetch_headers(ref vf, ref vf.vi[0], ref vf.vc[0], ref serialno_list, ref serialno_list_size)) < 0)
                {
                    vf.datasource = null;
                    ov_clear(ref vf);
                }
                else
                {
                    /* serial number list for first link needs to be held somewhere for second stage of seekable stream open; this saves having to seek/reread first link's serialnumber data then. */
                    vf.serialnos = (int*)Ogg._ogg_calloc(serialno_list_size + 2, sizeof(int));
                    vf.serialnos[0] = vf.current_serialno = vf.os.serialno;
                    vf.serialnos[1] = serialno_list_size;
                    CopyMemory(vf.serialnos + 2, serialno_list, serialno_list_size * sizeof(int));

                    vf.offsets = (long*)Ogg._ogg_calloc(1, sizeof(long));
                    vf.dataoffsets = (long*)Ogg._ogg_calloc(1, sizeof(long));
                    vf.offsets[0] = 0;
                    vf.dataoffsets[0] = vf.offset;
                    vf.ready_state = PARTOPEN;
                }

                Ogg._ogg_free(serialno_list);
            }

            return ret;
        }

        static int _ov_open2(ref OggVorbis_File vf)
        {
            if (vf.ready_state != PARTOPEN)
            {
                return Vorbis.OV_EINVAL;
            }

            vf.ready_state = OPENED;

            if (vf.seekable != 0)
            {
                int ret = (int)_open_seekable2(ref vf);

                if (ret != 0)
                {
                    vf.datasource = null;
                    ov_clear(ref vf);
                }

                return ret;
            }
            else
            {
                vf.ready_state = STREAMSET;
            }

            return 0;
        }

        /* clear out the OggVorbis_File struct */
        static int ov_clear(ref OggVorbis_File vf)
        {
            if (vf == null)
            {
                return -1;
            }

            Vorbis.vorbis_block_clear(ref vf.vb);
            Vorbis.vorbis_dsp_clear(ref vf.vd);
            Ogg.ogg_stream_clear(ref vf.os);

            if (vf.vi != null && vf.links != 0)
            {
                for (int i = 0; i < vf.links; i++)
                {
                    Vorbis.vorbis_info_clear(ref vf.vi[i]);
                    Vorbis.vorbis_comment_clear(ref vf.vc[i]);
                }
            }

            Ogg._ogg_free(vf.dataoffsets);
            Ogg._ogg_free(vf.pcmlengths);
            Ogg._ogg_free(vf.serialnos);
            Ogg._ogg_free(vf.offsets);
            Ogg.ogg_sync_clear(ref vf.oy);

            if (vf.datasource != null && vf.callbacks.close_func != null)
            {
                vf.callbacks.close_func(vf.datasource);

                // clear
                {
                    vf.datasource = null;
                    vf.seekable = 0;
                    vf.offset = 0;
                    vf.end = 0;
                    vf.oy = new Ogg.ogg_sync_state();

                    vf.links = 0;
                    vf.offsets = null;
                    vf.dataoffsets = null;
                    vf.serialnos = null;
                    vf.pcmlengths = null;

                    vf.vi = null;
                    vf.vc = null;

                    vf.pcm_offset = 0;
                    vf.ready_state = 0;
                    vf.current_serialno = 0;
                    vf.current_link = 0;

                    vf.bittrack = 0.0f;
                    vf.samptrack = 0.0f;

                    vf.os = new Ogg.ogg_stream_state();
                    vf.vd = new Vorbis.vorbis_dsp_state();
                    vf.vb = new Vorbis.vorbis_block();

                    vf.callbacks = null;
                }
            }

            return 0;
        }

        /* inspects the OggVorbis file and finds/documents all the logical bitstreams contained in it.  Tries to be tolerant of logical
          bitstream sections that are truncated/woogie.

          return: -1) error
                   0) OK */

        static public int ov_open_callbacks(Stream stream, ref OggVorbis_File vf, byte* initial, int ibytes, ov_callbacks callbacks)
        {
            int ret = _ov_open1(stream, ref vf, initial, ibytes, callbacks);

            if (ret != 0)
            {
                return ret;
            }
            else
            {
                return _ov_open2(ref vf);
            }
        }

        static public int ov_open(Stream stream, ref OggVorbis_File vf, byte* initial, int ibytes)
        {
            ov_callbacks callbacks = new ov_callbacks()
            {
                read_func = _read64_wrap,
                seek_func = _seek64_wrap,
                close_func = _close64_wrap,
                tell_func = _tell64_wrap,
            };

            return ov_open_callbacks(stream, ref vf, initial, ibytes, callbacks);
        }

        static public int ov_fopen(string path, ref OggVorbis_File vf)
        {
            if (File.Exists(path) == false)
            {
                return -1;
            }

            FileStream stream = File.OpenRead(path);

            int ret = ov_open(stream, ref vf, null, 0);

            if (ret != 0)
            {
                stream.Close();
            }

            return ret;
        }

        /* cheap hack for game usage where downsampling is desirable; there's no need for SRC as we can just do it cheaply in libvorbis. */

        static public int ov_halfrate(ref OggVorbis_File vf, int flag)
        {
            int i;

            if (vf.vi == null)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.ready_state > STREAMSET)
            {
                /* clear out stream state; dumping the decode machine is needed to reinit the MDCT lookups. */
                Vorbis.vorbis_dsp_clear(ref vf.vd);
                Vorbis.vorbis_block_clear(ref vf.vb);

                vf.ready_state = STREAMSET;

                if (vf.pcm_offset >= 0)
                {
                    long pos = vf.pcm_offset;

                    vf.pcm_offset = -1; /* make sure the pos is dumped if unseekable */
                    ov_pcm_seek(ref vf, pos);
                }
            }

            for (i = 0; i < vf.links; i++)
            {
                if (Vorbis.vorbis_synthesis_halfrate(ref vf.vi[i], flag) != 0)
                {
                    if (flag != 0)
                    {
                        ov_halfrate(ref vf, 0);
                    }

                    return Vorbis.OV_EINVAL;
                }
            }

            return 0;
        }

        static int ov_halfrate_p(ref OggVorbis_File vf)
        {
            if (vf == null)
            {
                return Vorbis.OV_EINVAL;
            }
            else if (vf.vi == null)
            {
                return Vorbis.OV_EINVAL;
            }
            else
            {
                return Vorbis.vorbis_synthesis_halfrate_p(ref vf.vi[0]);
            }
        }

        /* Only partially open the vorbis file; test for Vorbisness, and load the headers for the first chain.  Do not seek (although test for
          seekability).  Use ov_test_open to finish opening the file, else ov_clear to close/free it. Same return codes as open. */

        static public int ov_test_callbacks(Stream stream, ref OggVorbis_File vf, byte* initial, int ibytes, ov_callbacks callbacks)
        {
            return _ov_open1(stream, ref vf, initial, ibytes, callbacks);
        }

        static public int ov_test(Stream stream, ref OggVorbis_File vf, byte* initial, int ibytes)
        {
            ov_callbacks callbacks = new ov_callbacks()
            {
                read_func = _read64_wrap,
                seek_func = _seek64_wrap,
                close_func = _close64_wrap,
                tell_func = _tell64_wrap
            };

            return ov_test_callbacks(stream, ref vf, initial, ibytes, callbacks);
        }

        static public int ov_test_open(ref OggVorbis_File vf)
        {
            if (vf.ready_state != PARTOPEN)
            {
                return Vorbis.OV_EINVAL;
            }
            else
            {
                return _ov_open2(ref vf);
            }
        }

        /* How many logical bitstreams in this physical bitstream? */
        static public int ov_streams(ref OggVorbis_File vf)
        {
            return vf.links;
        }

        /* Is the FILE * associated with vf seekable? */
        static public int ov_seekable(ref OggVorbis_File vf)
        {
            return vf.seekable;
        }

        /* returns the bitrate for a given logical bitstream or the entire physical bitstream.  If the file is open for random access, it will
          find the *actual* average bitrate.  If the file is streaming, it returns the nominal bitrate (if set) else the average of the 
          upper/lower bounds (if set) else -1 (unset).

          If you want the actual bitrate field settings, get them from the vorbis_info structs */

        static public int ov_bitrate(ref OggVorbis_File vf, int i)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (i >= vf.links)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0 && i != 0)
            {
                return ov_bitrate(ref vf, 0);
            }

            if (i < 0)
            {
                long bits = 0;
                float br;

                for (int _i = 0; i < vf.links; i++)
                {
                    bits += (vf.offsets[_i + 1] - vf.dataoffsets[i]) * 8;
                }

                /* This once read: return(rint(bits/ov_time_total(vf,-1)));
                 gcc 3.x on x86 miscompiled this at optimisation level 2 and above,
                 so this is slightly transformed to make it work. */

                br = bits / ov_time_total(ref vf, -1);
                return Vorbis.rint(br);
            }
            else
            {
                if (vf.seekable != 0)
                {
                    /* return the actual bitrate */
                    return (Vorbis.rint((vf.offsets[i + 1] - vf.dataoffsets[i]) * 8 / ov_time_total(ref vf, i)));
                }
                else
                {
                    /* return nominal if set */
                    if (vf.vi[i].bitrate_nominal > 0)
                    {
                        return vf.vi[i].bitrate_nominal;
                    }
                    else
                    {
                        if (vf.vi[i].bitrate_upper > 0)
                        {
                            if (vf.vi[i].bitrate_lower > 0)
                            {
                                return (vf.vi[i].bitrate_upper + vf.vi[i].bitrate_lower) / 2;
                            }
                            else
                            {
                                return vf.vi[i].bitrate_upper;
                            }
                        }

                        return Vorbis.OV_FALSE;
                    }
                }
            }
        }

        /* returns the actual bitrate since last call.  returns -1 if no additional data to offer since last call (or at beginning of stream),
          EINVAL if stream is only partially open */

        static public int ov_bitrate_instant(ref OggVorbis_File vf)
        {
            int link = vf.seekable != 0 ? vf.current_link : 0;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.samptrack == 0)
            {
                return Vorbis.OV_FALSE;
            }

            int ret = (int)(vf.bittrack / vf.samptrack * vf.vi[link].rate + 0.5f);

            vf.bittrack = 0.0f;
            vf.samptrack = 0.0f;

            return (ret);
        }

        /* Guess */

        static public int ov_serialnumber(ref OggVorbis_File vf, int i)
        {
            if (i >= vf.links)
            {
                return ov_serialnumber(ref vf, vf.links - 1);
            }

            if (vf.seekable == 0 && i >= 0)
            {
                return ov_serialnumber(ref vf, -1);
            }

            if (i < 0)
            {
                return vf.current_serialno;
            }
            else
            {
                return vf.serialnos[i];
            }
        }

        /* returns: total raw (compressed) length of content if i==-1 raw (compressed) length of that logical bitstream for i==0 to n
          OV_EINVAL if the stream is not seekable (we can't know the length) or if stream is only partially open */

        static public long ov_raw_total(ref OggVorbis_File vf, int i)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0 || i >= vf.links)
            {
                return Vorbis.OV_EINVAL;
            }

            if (i < 0)
            {
                long acc = 0;

                for (int _i = 0; _i < vf.links; _i++)
                {
                    acc += ov_raw_total(ref vf, _i);
                }

                return acc;
            }
            else
            {
                return vf.offsets[i + 1] - vf.offsets[i];
            }
        }

        /* returns: total PCM length (samples) of content if i==-1 PCM length (samples) of that logical bitstream for i==0 to n
          OV_EINVAL if the stream is not seekable (we can't know the length) or only partially open */

        static public long ov_pcm_total(ref OggVorbis_File vf, int i)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0 || i >= vf.links)
            {
                return Vorbis.OV_EINVAL;
            }

            if (i < 0)
            {
                long acc = 0;

                for (int _i = 0; _i < vf.links; _i++)
                {
                    acc += ov_pcm_total(ref vf, _i);
                }

                return acc;
            }
            else
            {
                return vf.pcmlengths[i * 2 + 1];
            }
        }

        /* returns: total seconds of content if i==-1 seconds in that logical bitstream for i==0 to n
          OV_EINVAL if the stream is not seekable (we can't know the length) or only partially open */

        static public float ov_time_total(ref OggVorbis_File vf, int i)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0 || i >= vf.links)
            {
                return Vorbis.OV_EINVAL;
            }

            if (i < 0)
            {
                float acc = 0;

                for (int _i = 0; _i < vf.links; _i++)
                {
                    acc += ov_time_total(ref vf, _i);
                }

                return acc;
            }
            else
            {
                return ((float)(vf.pcmlengths[i * 2 + 1]) / vf.vi[i].rate);
            }
        }

        /* seek to an offset relative to the *compressed* data. This also scans packets to update the PCM cursor. It will cross a logical
          bitstream boundary, but only if it can't get any packets out of the tail of the bitstream we seek to (so no surprises).
          returns zero on success, nonzero on failure */

        static public int ov_raw_seek(ref OggVorbis_File vf, long pos)
        {
            Ogg.ogg_stream_state work_os = new Ogg.ogg_stream_state();

            int ret;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0)
            {
                return Vorbis.OV_ENOSEEK; /* don't dump machine if we can't seek */
            }

            if (pos < 0 || pos > vf.end)
            {
                return Vorbis.OV_EINVAL;
            }

            /* is the seek position outside our current link [if any]? */

            if (vf.ready_state >= STREAMSET)
            {
                if (pos < vf.offsets[vf.current_link] || pos >= vf.offsets[vf.current_link + 1])
                {
                    _decode_clear(ref vf); /* clear out stream state */
                }
            }

            /* don't yet clear out decoding machine (if it's initialized), in the case we're in the same link.  Restart the decode lapping, and
             let _fetch_and_process_packet deal with a potential bitstream boundary */

            vf.pcm_offset = -1;

            Ogg.ogg_stream_reset_serialno(ref vf.os, vf.current_serialno); /* must set serialno */
            Vorbis.vorbis_synthesis_restart(ref vf.vd);

            ret = _seek_helper(ref vf, pos);

            if (ret != 0)
            {
                goto seek_error;
            }

            /* we need to make sure the pcm_offset is set, but we don't want to advance the raw cursor past good packets just to get to the first
             with a granulepos.  That's not equivalent behavior to beginning decoding as immediately after the seek position as possible.

             So, a hack.  We use two stream states; a local scratch state and the shared vf->os stream state.  We use the local state to
             scan, and the shared state as a buffer for later decode.

             Unfortuantely, on the last page we still advance to last packet because the granulepos on the last page is not necessarily on a
             packet boundary, and we need to make sure the granpos is correct. */

            {
                Ogg.ogg_page og = new Ogg.ogg_page();
                Ogg.ogg_packet op = new Ogg.ogg_packet();

                int lastblock = 0;
                int accblock = 0;
                int thisblock = 0;
                int lastflag = 0;
                int firstflag = 0;
                long pagepos = -1;

                Ogg.ogg_stream_init(ref work_os, vf.current_serialno); /* get the memory ready */
                Ogg.ogg_stream_reset(ref work_os); /* eliminate the spurious OV_HOLE return from not necessarily starting from the beginning */

                while (true)
                {
                    if (vf.ready_state >= STREAMSET)
                    {
                        /* snarf/scan a packet if we can */
                        int result = Ogg.ogg_stream_packetout(ref work_os, ref op);

                        if (result > 0)
                        {
                            if (vf.vi[vf.current_link].codec_setup != null)
                            {
                                thisblock = Vorbis.vorbis_packet_blocksize(ref vf.vi[vf.current_link], ref op);

                                if (thisblock < 0)
                                {
                                    Ogg.ogg_stream_packetout(ref vf.os);
                                    thisblock = 0;
                                }
                                else
                                {
                                    /* We can't get a guaranteed correct pcm position out of the last page in a stream because it might have a 'short'
                                   granpos, which can only be detected in the presence of a preceding page.  However, if the last page is also the first
                                   page, the granpos rules of a first page take precedence.  Not only that, but for first==last, the EOS page must be treated
                                   as if its a normal first page for the stream to open/play. */
                                    if (lastflag != 0 && firstflag == 0)
                                    {
                                        Ogg.ogg_stream_packetout(ref vf.os);
                                    }
                                    else if (lastblock != 0)
                                    {
                                        accblock += (lastblock + thisblock) >> 2;
                                    }
                                }

                                if (op.granulepos != -1)
                                {
                                    int i, link = vf.current_link;
                                    long granulepos = op.granulepos - vf.pcmlengths[link * 2];

                                    if (granulepos < 0)
                                    {
                                        granulepos = 0;
                                    }

                                    for (i = 0; i < link; i++)
                                    {
                                        granulepos += vf.pcmlengths[i * 2 + 1];
                                    }

                                    vf.pcm_offset = granulepos - accblock;

                                    if (vf.pcm_offset < 0)
                                    {
                                        vf.pcm_offset = 0;
                                    }
                                    break;
                                }

                                lastblock = thisblock;
                                continue;
                            }
                            else
                            {
                                Ogg.ogg_stream_packetout(ref vf.os);
                            }
                        }
                    }

                    if (lastblock == 0)
                    {
                        pagepos = _get_next_page(ref vf, ref og, -1);

                        if (pagepos < 0)
                        {
                            vf.pcm_offset = ov_pcm_total(ref vf, -1);
                            break;
                        }
                    }
                    else
                    {
                        /* huh?  Bogus stream with packets but no granulepos */
                        vf.pcm_offset = -1;
                        break;
                    }

                    /* has our decoding just traversed a bitstream boundary? */
                    if (vf.ready_state >= STREAMSET)
                    {
                        if (vf.current_serialno != Ogg.ogg_page_serialno(ref og))
                        {

                            /* two possibilities:
                               1) our decoding just traversed a bitstream boundary
                               2) another stream is multiplexed into this logical section? */

                            if (Ogg.ogg_page_bos(ref og) != 0)
                            {
                                /* we traversed */
                                _decode_clear(ref vf); /* clear out stream state */
                                Ogg.ogg_stream_clear(ref work_os);
                            } /* else, do nothing; next loop will scoop another page */
                        }
                    }

                    if (vf.ready_state < STREAMSET)
                    {
                        int link;
                        int serialno = Ogg.ogg_page_serialno(ref og);

                        for (link = 0; link < vf.links; link++)
                        {
                            if (vf.serialnos[link] == serialno)
                            {
                                break;
                            }
                        }

                        if (link == vf.links)
                        {
                            /* not the desired Vorbis bitstream section; keep trying */
                            continue;
                        }

                        vf.current_link = link;
                        vf.current_serialno = serialno;

                        Ogg.ogg_stream_reset_serialno(ref vf.os, serialno);
                        Ogg.ogg_stream_reset_serialno(ref work_os, serialno);

                        vf.ready_state = STREAMSET;
                        firstflag = (pagepos <= vf.dataoffsets[link] ? 1 : 0);
                    }

                    Ogg.ogg_stream_pagein(ref vf.os, ref og);
                    Ogg.ogg_stream_pagein(ref work_os, ref og);

                    lastflag = Ogg.ogg_page_eos(ref og);
                }
            }

            Ogg.ogg_stream_clear(ref work_os);

            vf.bittrack = 0.0f;
            vf.samptrack = 0.0f;

            return 0;

        seek_error:
            /* dump the machine so we're in a known state */
            vf.pcm_offset = -1;

            Ogg.ogg_stream_clear(ref work_os);
            _decode_clear(ref vf);

            return Vorbis.OV_EBADLINK;
        }

        /* Page granularity seek (faster than sample granularity because we don't do the last bit of decode to find a specific sample).

          Seek to the last [granule marked] page preceding the specified pos location, such that decoding past the returned point will quickly
          arrive at the requested position. */

        static public int ov_pcm_seek_page(ref OggVorbis_File vf, long pos)
        {
            int link = -1;
            long result = 0;
            long total = ov_pcm_total(ref vf, -1);

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0)
            {
                return Vorbis.OV_ENOSEEK;
            }

            if (pos < 0 || pos > total)
            {
                return Vorbis.OV_EINVAL;
            }

            /* which bitstream section does this pcm offset occur in? */
            for (link = vf.links - 1; link >= 0; link--)
            {
                total -= vf.pcmlengths[link * 2 + 1];

                if (pos >= total)
                {
                    break;
                }
            }

            /* search within the logical bitstream for the page with the highest pcm_pos preceding (or equal to) pos.  There is a danger here;
              missing pages or incorrect frame number information in the bitstream could make our task impossible.  Account for that (it
              would be an error condition) */

            /* new search algorithm by HB (Nicholas Vinen) */
            {
                long end = vf.offsets[link + 1];
                long begin = vf.offsets[link];
                long begintime = vf.pcmlengths[link * 2];
                long endtime = vf.pcmlengths[link * 2 + 1] + begintime;
                long target = pos - total + begintime;
                long best = begin;

                Ogg.ogg_page og;
                Ogg.ogg_packet op;

                og = new Ogg.ogg_page();

                while (begin < end)
                {
                    long bisect;

                    if (end - begin < CHUNKSIZE)
                    {
                        bisect = begin;
                    }
                    else
                    {
                        /* take a (pretty decent) guess. */
                        bisect = begin + (long)((double)(target - begintime) * (end - begin) / (endtime - begintime)) - CHUNKSIZE;

                        if (bisect < begin + CHUNKSIZE)
                        {
                            bisect = begin;
                        }
                    }

                    if (bisect != vf.offset)
                    {
                        result = _seek_helper(ref vf, bisect);

                        if (result != 0)
                        {
                            goto seek_error;
                        }
                    }

                    while (begin < end)
                    {
                        result = _get_next_page(ref vf, ref og, end - vf.offset);

                        if (result == Vorbis.OV_EREAD)
                        {
                            goto seek_error;
                        }

                        if (result < 0)
                        {
                            if (bisect <= begin + 1)
                            {
                                end = begin; /* found it */
                            }
                            else
                            {
                                if (bisect == 0)
                                {
                                    goto seek_error;
                                }

                                bisect -= CHUNKSIZE;

                                if (bisect <= begin)
                                {
                                    bisect = begin + 1;
                                }

                                result = _seek_helper(ref vf, bisect);

                                if (result != 0)
                                {
                                    goto seek_error;
                                }
                            }
                        }
                        else
                        {
                            long granulepos;

                            if (Ogg.ogg_page_serialno(ref og) != vf.serialnos[link])
                            {
                                continue;
                            }

                            granulepos = Ogg.ogg_page_granulepos(ref og);

                            if (granulepos == -1)
                            {
                                continue;
                            }

                            if (granulepos < target)
                            {
                                best = result;  /* raw offset of packet with granulepos */
                                begin = vf.offset; /* raw offset of next page */
                                begintime = granulepos;

                                if (target - begintime > 44100)
                                {
                                    break;
                                }

                                bisect = begin; /* *not* begin + 1 */
                            }
                            else
                            {
                                if (bisect <= begin + 1)
                                {
                                    end = begin;  /* found it */
                                }
                                else
                                {
                                    if (end == vf.offset)
                                    { /* we're pretty close - we'd be stuck in */
                                        end = result;
                                        bisect -= CHUNKSIZE; /* an endless loop otherwise. */

                                        if (bisect <= begin)
                                        {
                                            bisect = begin + 1;
                                        }

                                        result = _seek_helper(ref vf, bisect);

                                        if (result != 0)
                                        {
                                            goto seek_error;
                                        }
                                    }
                                    else
                                    {
                                        end = bisect;
                                        endtime = granulepos;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                /* found our page. seek to it, update pcm offset. Easier case than raw_seek, don't keep packets preceding granulepos. */

                {
                    og = new Ogg.ogg_page();
                    op = new Ogg.ogg_packet();

                    /* seek */
                    result = _seek_helper(ref vf, best);
                    vf.pcm_offset = -1;

                    if (result != 0)
                    {
                        goto seek_error;
                    }

                    result = _get_next_page(ref vf, ref og, -1);

                    if (result < 0)
                    {
                        goto seek_error;
                    }

                    if (link != vf.current_link)
                    {
                        /* Different link; dump entire decode machine */
                        _decode_clear(ref vf);

                        vf.current_link = link;
                        vf.current_serialno = vf.serialnos[link];
                        vf.ready_state = STREAMSET;
                    }
                    else
                    {
                        Vorbis.vorbis_synthesis_restart(ref vf.vd);
                    }

                    Ogg.ogg_stream_reset_serialno(ref vf.os, vf.current_serialno);
                    Ogg.ogg_stream_pagein(ref vf.os, ref og);

                    /* pull out all but last packet; the one with granulepos */
                    while (true)
                    {
                        result = Ogg.ogg_stream_packetpeek(ref vf.os, ref op);

                        if (result == 0)
                        {
                            /* !!! the packet finishing this page originated on a
                               preceding page. Keep fetching previous pages until we
                               get one with a granulepos or without the 'continued' flag
                               set.  Then just use raw_seek for simplicity. */

                            result = _seek_helper(ref vf, best);

                            if (result < 0)
                            {
                                goto seek_error;
                            }

                            while (true)
                            {
                                result = _get_prev_page(ref vf, ref og);

                                if (result < 0)
                                {
                                    goto seek_error;
                                }

                                if (Ogg.ogg_page_serialno(ref og) == vf.current_serialno && (Ogg.ogg_page_granulepos(ref og) > -1 || Ogg.ogg_page_continued(ref og) == 0))
                                {
                                    return ov_raw_seek(ref vf, result);
                                }

                                vf.offset = result;
                            }
                        }
                        if (result < 0)
                        {
                            result = Vorbis.OV_EBADPACKET;
                            goto seek_error;
                        }
                        if (op.granulepos != -1)
                        {
                            vf.pcm_offset = op.granulepos - vf.pcmlengths[vf.current_link * 2];

                            if (vf.pcm_offset < 0)
                            {
                                vf.pcm_offset = 0;
                            }

                            vf.pcm_offset += total;
                            break;
                        }
                        else
                        {
                            result = Ogg.ogg_stream_packetout(ref vf.os);
                        }
                    }
                }
            }

            /* verify result */
            if (vf.pcm_offset > pos || pos > ov_pcm_total(ref vf, -1))
            {
                result = Vorbis.OV_EFAULT;
                goto seek_error;
            }

            vf.bittrack = 0.0f;
            vf.samptrack = 0.0f;

            return 0;

        seek_error:

            /* dump machine so we're in a known state */
            vf.pcm_offset = -1;
            _decode_clear(ref vf);
            return (int)result;
        }

        /* seek to a sample offset relative to the decompressed pcm stream returns zero on success, nonzero on failure */
        static public int ov_pcm_seek(ref OggVorbis_File vf, long pos)
        {
            int thisblock, lastblock = 0;
            int ret = ov_pcm_seek_page(ref vf, pos);

            if (ret < 0)
            {
                return ret;
            }

            if ((ret = _make_decode_ready(ref vf)) != 0)
            {
                return ret;
            }

            /* discard leading packets we don't need for the lapping of the
              position we want; don't decode them */

            Ogg.ogg_packet op = new Ogg.ogg_packet();
            Ogg.ogg_page og = new Ogg.ogg_page();
                
            while (true)
            {
                ret = Ogg.ogg_stream_packetpeek(ref vf.os, ref op);

                if (ret > 0)
                {
                    thisblock = Vorbis.vorbis_packet_blocksize(ref vf.vi[vf.current_link], ref op);

                    if (thisblock < 0)
                    {
                        Ogg.ogg_stream_packetout(ref vf.os);

                        /* non audio packet */
                        continue;
                    }

                    if (lastblock != 0)
                    {
                        vf.pcm_offset += (lastblock + thisblock) >> 2;
                    }

                    if (vf.pcm_offset + ((thisblock + Vorbis.vorbis_info_blocksize(ref vf.vi[0], 1)) >> 2) >= pos)
                    {
                        break;
                    }

                    /* remove the packet from packet queue and track its granulepos */
                    Ogg.ogg_stream_packetout(ref vf.os);
                    Vorbis.vorbis_synthesis_trackonly(ref vf.vb, ref op);  /* set up a vb with only tracking, no pcm_decode */
                    Vorbis.vorbis_synthesis_blockin(ref vf.vd, ref vf.vb);

                    /* end of logical stream case is hard, especially with exact length positioning. */
                    if (op.granulepos > -1)
                    {
                        /* always believe the stream markers */
                        vf.pcm_offset = op.granulepos - vf.pcmlengths[vf.current_link * 2];

                        if (vf.pcm_offset < 0)
                        {
                            vf.pcm_offset = 0;
                        }

                        for (int i = 0; i < vf.current_link; i++)
                        {
                            vf.pcm_offset += vf.pcmlengths[i * 2 + 1];
                        }
                    }

                    lastblock = thisblock;

                }
                else
                {
                    if (ret < 0 && ret != Vorbis.OV_HOLE)
                    {
                        break;
                    }

                    /* suck in a new page */
                    if (_get_next_page(ref vf, ref og, -1) < 0)
                    {
                        break;
                    }

                    if (Ogg.ogg_page_bos(ref og) != 0)
                    {
                        _decode_clear(ref vf);
                    }

                    if (vf.ready_state < STREAMSET)
                    {
                        int serialno = Ogg.ogg_page_serialno(ref og);
                        int link;

                        for (link = 0; link < vf.links; link++)
                        {
                            if (vf.serialnos[link] == serialno)
                            {
                                break;
                            }
                        }

                        if (link == vf.links)
                        {
                            continue;
                        }
                        else
                        {
                            vf.current_link = link;
                        }

                        vf.ready_state = STREAMSET;
                        vf.current_serialno = Ogg.ogg_page_serialno(ref og);

                        Ogg.ogg_stream_reset_serialno(ref vf.os, serialno);
                        ret = _make_decode_ready(ref vf);

                        if (ret != 0)
                        {
                            return ret;
                        }

                        lastblock = 0;
                    }

                    Ogg.ogg_stream_pagein(ref vf.os, ref og);
                }
            }

            vf.bittrack = 0.0f;
            vf.samptrack = 0.0f;

            /* discard samples until we reach the desired position. Crossing a logical bitstream boundary with abandon is OK. */
            {
                /* note that halfrate could be set differently in each link, but vorbisfile encoforces all links are set or unset */
                int hs = Vorbis.vorbis_synthesis_halfrate_p(ref vf.vi[0]);

                while (vf.pcm_offset < ((pos >> hs) << hs))
                {
                    long target = (pos - vf.pcm_offset) >> hs;
                    int samples = Vorbis.vorbis_synthesis_pcmout(ref vf.vd);

                    if (samples > target)
                    {
                        samples = (int)target;
                    }

                    Vorbis.vorbis_synthesis_read(ref vf.vd, samples);
                    vf.pcm_offset += samples << hs;

                    if (samples < target)
                    {
                        if (_fetch_and_process_packet(ref vf, 1, 1) <= 0)
                        {
                            vf.pcm_offset = ov_pcm_total(ref vf, -1); /* eof */
                        }
                    }
                }
            }

            return 0;
        }

        /* seek to a playback time relative to the decompressed pcm stream returns zero on success, nonzero on failure */
        static public int ov_time_seek(ref OggVorbis_File vf, double seconds)
        {
            /* translate time to PCM position and call ov_pcm_seek */
            int link = -1;
            long pcm_total = 0;
            double time_total = 0.0;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0)
            {
                return Vorbis.OV_ENOSEEK;
            }

            if (seconds < 0)
            {
                return Vorbis.OV_EINVAL;
            }

            /* which bitstream section does this time offset occur in? */
            for (link = 0; link < vf.links; link++)
            {
                double addsec = ov_time_total(ref vf, link);

                if (seconds < time_total + addsec)
                {
                    break;
                }

                time_total += addsec;
                pcm_total += vf.pcmlengths[link * 2 + 1];
            }

            if (link == vf.links)
            {
                return Vorbis.OV_EINVAL;
            }

            /* enough information to convert time offset to pcm offset */
            {
                long target = (long)(pcm_total + (seconds - time_total) * vf.vi[link].rate);
                return ov_pcm_seek(ref vf, target);
            }
        }

        /* page-granularity version of ov_time_seek returns zero on success, nonzero on failure */
        static public int ov_time_seek_page(ref OggVorbis_File vf, double seconds)
        {
            /* translate time to PCM position and call ov_pcm_seek */
            int link = -1;
            long pcm_total = 0;
            double time_total = 0.0;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0)
            {
                return Vorbis.OV_ENOSEEK;
            }

            if (seconds < 0.0)
            {
                return Vorbis.OV_EINVAL;
            }

            /* which bitstream section does this time offset occur in? */
            for (link = 0; link < vf.links; link++)
            {
                double addsec = ov_time_total(ref vf, link);

                if (seconds < time_total + addsec)
                {
                    break;
                }

                time_total += addsec;
                pcm_total += vf.pcmlengths[link * 2 + 1];
            }

            if (link == vf.links)
            {
                return Vorbis.OV_EINVAL;
            }

            /* enough information to convert time offset to pcm offset */
            {
                long target = (long)(pcm_total + (seconds - time_total) * vf.vi[link].rate);
                return ov_pcm_seek_page(ref vf, target);
            }
        }

        /* tell the current stream offset cursor.  Note that seek followed by tell will likely not give the set offset due to caching */
        static public long ov_raw_tell(ref OggVorbis_File vf)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }
            else
            {
                return vf.offset;
            }
        }

        /* return PCM offset (sample) of next PCM sample to be read */
        static public long ov_pcm_tell(ref OggVorbis_File vf)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }
            else
            {
                return vf.pcm_offset;
            }
        }

        /* return time offset (seconds) of next PCM sample to be read */
        static double ov_time_tell(ref OggVorbis_File vf)
        {
            int link = 0;
            long pcm_total = 0;
            double time_total = 0.0;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf.seekable == 0)
            {
                pcm_total = ov_pcm_total(ref vf, -1);
                time_total = ov_time_total(ref vf, -1);

                /* which bitstream section does this time offset occur in? */
                for (link = vf.links - 1; link >= 0; link--)
                {
                    pcm_total -= vf.pcmlengths[link * 2 + 1];
                    time_total -= ov_time_total(ref vf, link);

                    if (vf.pcm_offset >= pcm_total)
                    {
                        break;
                    }
                }
            }

            return ((double)time_total + (double)(vf.pcm_offset - pcm_total) / vf.vi[link].rate);
        }

        /* link: -1) return the vorbis_info struct for the bitstream section
                     currently being decoded
         
                 0-n) to request information for a specific bitstream section

          In the case of a non-seekable bitstream, any call returns the current bitstream.  NULL in the case that the machine is not
          initialized */

        static public Vorbis.vorbis_info ov_info(ref OggVorbis_File vf, int link)
        {
            if (vf.seekable != 0)
            {
                if (link < 0)
                {
                    if (vf.ready_state >= STREAMSET)
                    {
                        return vf.vi[vf.current_link];
                    }
                    else
                    {
                        return vf.vi[0];
                    }
                }
                else
                {
                    if (link >= vf.links)
                    {
                        return null;
                    }
                    else
                    {
                        return vf.vi[link];
                    }
                }
            }
            else
            {
                return vf.vi[0];
            }
        }

        /* grr, strong typing, grr, no templates/inheritence, grr */
        static public Vorbis.vorbis_comment ov_comment(ref OggVorbis_File vf, int link)
        {
            if (vf.seekable != 0)
            {
                if (link < 0)
                {
                    if (vf.ready_state >= STREAMSET)
                    {
                        return vf.vc[vf.current_link];
                    }
                    else
                    {
                        return vf.vc[0];
                    }
                }
                else
                {
                    if (link >= vf.links)
                    {
                        return null;
                    }
                    else
                    {
                        return vf.vc[link];
                    }
                }
            }
            else
            {
                return vf.vc[0];
            }
        }

        static int host_is_big_endian()
        {
            long pattern = 0xfeedface; /* deadbeef */
            byte* bytewise = (byte*)&pattern;

            if (bytewise[0] == 0xfe)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        /* up to this point, everything could more or less hide the multiple
          logical bitstream nature of chaining from the toplevel application
          if the toplevel application didn't particularly care.  However, at
          the point that we actually read audio back, the multiple-section
          nature must surface: Multiple bitstream sections do not necessarily
          have to have the same number of channels or sampling rate.

          ov_read returns the sequential logical bitstream number currently
          being decoded along with the PCM data in order that the toplevel
          application can take action on channel/sample rate changes.  This
          number will be incremented even for streamed (non-seekable) streams
          (for seekable streams, it represents the actual logical bitstream
          index within the physical bitstream.  Note that the accessor
          functions above are aware of this dichotomy).

          ov_read_filter is exactly the same as ov_read except that it processes
          the decoded audio data through a filter before packing it into the
          requested format. This gives greater accuracy than applying a filter
          after the audio has been converted into integral PCM.

          input values: buffer) a buffer to hold packed PCM data for return
                        length) the byte length requested to be placed into buffer
                        bigendianp) should the data be packed LSB first (0) or
                                     MSB first (1)
                        word) word size for output.  currently 1 (byte) or
                              2 (16 bit short)

          return values: <0) error/hole in data (OV_HOLE), partial open (OV_EINVAL)
                          0) EOF
                          n) number of bytes of PCM actually returned.  The
                             below works on a packet-by-packet basis, so the
                             return length is not related to the 'length' passed
                             in, just guaranteed to fit.

                *section) set to the logical bitstream number */

        static public int ov_read_filter(ref OggVorbis_File vf, byte* buffer, int length, int bigendianp, int word, int sgned, ref int bitstream, ov_filter_func filter, void* filter_param)
        {
            int i, j;
            int host_endian = host_is_big_endian();
            int hs;
            int samples;

            float** pcm = null;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            while (true)
            {
                if (vf.ready_state == INITSET)
                {
                    samples = Vorbis.vorbis_synthesis_pcmout(ref vf.vd, ref pcm);

                    if (samples != 0)
                    {
                        break;
                    }
                }

                /* suck in another packet */
                {
                    int ret = _fetch_and_process_packet(ref vf, 1, 1);

                    if (ret == Vorbis.OV_EOF)
                    {
                        return 0;
                    }
                    else if (ret <= 0)
                    {
                        return ret;
                    }
                }
            }

            if (samples > 0)
            {
                /* yay! proceed to pack data into the byte buffer */

                int channels = ov_info(ref vf, -1).channels;
                int bytespersample = word * channels;

                if (samples > length / bytespersample)
                {
                    samples = length / bytespersample;
                }

                if (samples <= 0)
                {
                    return Vorbis.OV_EINVAL;
                }

                if (filter != null)
                {
                    filter(pcm, channels, samples, filter_param);
                }

                /* a tight loop to pack each size */
                {
                    int val;
                    
                    if (word == 1)
                    {
                        int off = (sgned != 0 ? 0 : 128);

                        for (j = 0; j < samples; j++)
                        {
                            for (i = 0; i < channels; i++)
                            {
                                val = (int)(pcm[i][j] * 128.0f);

                                if (val > 127)
                                {
                                    val = 127;
                                }
                                else if (val < -128)
                                {
                                    val = -128;
                                }

                                *buffer++ = (byte)(val + off);
                            }
                        }
                    }
                    else
                    {
                        int off = (sgned != 0 ? 0 : 32768);

                        if (host_endian == bigendianp)
                        {
                            if (sgned != 0)
                            {
                                for (i = 0; i < channels; i++)
                                { 
                                    /* It's faster in this order */
                                    float* src = pcm[i];
                                    short* dest = ((short*)buffer) + i;
                                    
                                    for (j = 0; j < samples; j++)
                                    {
                                        val = (int)(src[j] * 32768.0f);

                                        if (val > 32767)
                                        {
                                            val = 32767;
                                        }
                                        else if (val < -32768)
                                        {
                                            val = -32768;
                                        }

                                        *dest = (short)val;
                                        dest += channels;
                                    }
                                }
                            }
                            else
                            {
                                for (i = 0; i < channels; i++)
                                {
                                    float* src = pcm[i];
                                    short* dest = ((short*)buffer) + i;

                                    for (j = 0; j < samples; j++)
                                    {
                                        val = (int)(src[j] * 32768.0f);

                                        if (val > 32767)
                                        {
                                            val = 32767;
                                        }
                                        else if (val < -32768)
                                        {
                                            val = -32768;
                                        }

                                        *dest = (short)(val + off);
                                        dest += channels;
                                    }
                                }
                            }
                        }
                        else if (bigendianp != 0)
                        {
                            for (j = 0; j < samples; j++)
                            {
                                for (i = 0; i < channels; i++)
                                {
                                    val = (int)(pcm[i][j] * 32768.0f);

                                    if (val > 32767)
                                    {
                                        val = 32767;
                                    }
                                    else if (val < -32768)
                                    {
                                        val = -32768;
                                    }

                                    val += off;
                                    
                                    *buffer++ = (byte)(val >> 8);
                                    *buffer++ = (byte)(val & 0xff);
                                }
                            }
                        }
                        else
                        {
                            for (j = 0; j < samples; j++)
                            {
                                for (i = 0; i < channels; i++)
                                {
                                    val = (int)(pcm[i][j] * 32768.0f);

                                    if (val > 32767)
                                    {
                                        val = 32767;
                                    }
                                    else if (val < -32768)
                                    {
                                        val = -32768;
                                    }

                                    val += off;
                                    
                                    *buffer++ = (byte)(val & 0xff);
                                    *buffer++ = (byte)(val >> 8);
                                }
                            }
                        }
                    }
                }

                Vorbis.vorbis_synthesis_read(ref vf.vd, samples);
                hs = Vorbis.vorbis_synthesis_halfrate_p(ref vf.vi[0]);

                vf.pcm_offset += (samples << hs);
                bitstream = vf.current_link;

                return samples * bytespersample;
            }
            else
            {
                return samples;
            }
        }

        static public int ov_read(ref OggVorbis_File vf, byte* buffer, int length, int bigendianp, int word, int sgned, ref int bitstream)
        {
            return ov_read_filter(ref vf, buffer, length, bigendianp, word, sgned, ref bitstream, null, null);
        }

        /* input values: pcm_channels) a float vector per channel of output
                         length) the sample length being read by the app

          return values: <0) error/hole in data (OV_HOLE), partial open (OV_EINVAL)
                          0) EOF
                          n) number of samples of PCM actually returned.  The
                          below works on a packet-by-packet basis, so the
                          return length is not related to the 'length' passed
                          in, just guaranteed to fit.

                   *section) set to the logical bitstream number */

        static public int ov_read_float(ref OggVorbis_File vf, ref float** pcm_channels, int length, ref int bitstream)
        {
            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            while (true)
            {
                if (vf.ready_state == INITSET)
                {
                    float** pcm = null;
                    int samples = Vorbis.vorbis_synthesis_pcmout(ref vf.vd, ref pcm);

                    if (samples != 0)
                    {
                        int hs = Vorbis.vorbis_synthesis_halfrate_p(ref vf.vi[0]);

                        if (pcm_channels != null)
                        {
                            pcm_channels = pcm;
                        }

                        if (samples > length)
                        {
                            samples = length;
                        }

                        Vorbis.vorbis_synthesis_read(ref vf.vd, samples);

                        vf.pcm_offset += samples << hs;
                        bitstream = vf.current_link;

                        return samples;
                    }
                }

                /* suck in another packet */
                {
                    int ret = _fetch_and_process_packet(ref vf, 1, 1);

                    if (ret == Vorbis.OV_EOF)
                    {
                        return 0;
                    }
                    else if (ret <= 0)
                    {
                        return ret;
                    }
                }
            }
        }

        static void _ov_splice(float** pcm, float** lappcm, int n1, int n2, int ch1, int ch2, ref float[] w1, ref float[] w2)
        {
            int i, j;
            float[] w = w1;
            int n = n1;

            if (n1 > n2)
            {
                n = n2;
                w = w2;
            }

            /* splice */
            for (j = 0; j < ch1 && j < ch2; j++)
            {
                float* s = lappcm[j];
                float* d = pcm[j];

                for (i = 0; i < n; i++)
                {
                    float wd = w[i] * w[i];
                    float ws = 1.0f - wd;
                    d[i] = d[i] * wd + s[i] * ws;
                }
            }

            /* window from zero */
            for (; j < ch2; j++)
            {
                float* d = pcm[j];

                for (i = 0; i < n; i++)
                {
                    float wd = w[i] * w[i];
                    d[i] = d[i] * wd;
                }
            }
        }

        /* make sure vf is INITSET */
        static int _ov_initset(ref OggVorbis_File vf)
        {
            while (true)
            {
                if (vf.ready_state == INITSET)
                {
                    break;
                }

                /* suck in another packet */
                {
                    int ret = _fetch_and_process_packet(ref vf, 1, 0);

                    if (ret < 0 && ret != Vorbis.OV_HOLE)
                    {
                        return ret;
                    }
                }
            }

            return 0;
        }

        /* make sure vf is INITSET and that we have a primed buffer; if we're crosslapping at a stream section boundary, this also makes
          sure we're sanity checking against the right stream information */

        static int _ov_initprime(ref OggVorbis_File vf)
        {
            Vorbis.vorbis_dsp_state vd = vf.vd;

            while (true)
            {
                if (vf.ready_state == INITSET)
                {
                    if (Vorbis.vorbis_synthesis_pcmout(ref vd) != 0)
                    {
                        break;
                    }
                }

                /* suck in another packet */
                {
                    int ret = _fetch_and_process_packet(ref vf, 1, 0);

                    if (ret < 0 && ret != Vorbis.OV_HOLE)
                    {
                        return ret;
                    }
                }
            }

            return 0;
        }

        /* grab enough data for lapping from vf; this may be in the form of unreturned, already-decoded pcm, remaining PCM we will need to
          decode, or synthetic postextrapolation from last packets. */

        static void _ov_getlap(ref OggVorbis_File vf, ref Vorbis.vorbis_info vi, ref Vorbis.vorbis_dsp_state vd, float** lappcm, int lapsize)
        {
            int lapcount = 0;
            float** pcm = null;

            /* try first to decode the lapping data */
            while (lapcount < lapsize)
            {
                int samples = Vorbis.vorbis_synthesis_pcmout(ref vd, ref pcm);

                if (samples != 0)
                {
                    if (samples > lapsize - lapcount)
                    {
                        samples = lapsize - lapcount;
                    }

                    for (int i = 0; i < vi.channels; i++)
                    {
                        CopyMemory(lappcm[i] + lapcount, pcm[i], sizeof(float) * samples);
                    }

                    lapcount += samples;
                    Vorbis.vorbis_synthesis_read(ref vd, samples);
                }
                else
                {
                    /* suck in another packet */
                    int ret = _fetch_and_process_packet(ref vf, 1, 0); /* do *not* span */

                    if (ret == Vorbis.OV_EOF)
                    {
                        break;
                    }
                }
            }

            if (lapcount < lapsize)
            {
                /* failed to get lapping data from normal decode; pry it from the postextrapolation buffering, or the second half of the MDCT from the last packet */
                int samples = Vorbis.vorbis_synthesis_lapout(ref vf.vd, ref pcm);

                if (samples == 0)
                {
                    for (int i = 0; i < vi.channels; i++)
                    {
                        ZeroMemory(lappcm[i] + lapcount, sizeof(float) * lapsize - lapcount);
                    }

                    lapcount = lapsize;
                }
                else
                {
                    if (samples > lapsize - lapcount)
                    {
                        samples = lapsize - lapcount;
                    }

                    for (int i = 0; i < vi.channels; i++)
                    {
                        CopyMemory(lappcm[i] + lapcount, pcm[i], sizeof(float) * samples);
                    }

                    lapcount += samples;
                }
            }
        }

        /* this sets up crosslapping of a sample by using trailing data from sample 1 and lapping it into the windowing buffer of sample 2 */

        static int ov_crosslap(ref OggVorbis_File vf1, ref OggVorbis_File vf2)
        {
            Vorbis.vorbis_info vi1 = new Vorbis.vorbis_info();
            Vorbis.vorbis_info vi2 = new Vorbis.vorbis_info();

            float** pcm = null;
            float[] w1, w2;
            int n1, n2, i, ret, hs1, hs2;

            if (vf1 == vf2)
            {
                return 0; /* degenerate case */
            }

            if (vf1.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            if (vf2.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            /* the relevant overlap buffers must be pre-checked and pre-primed before looking at settings in the event that priming would cross
             a bitstream boundary. So, do it now */

            ret = _ov_initset(ref vf1);

            if (ret != 0)
            {
                return (ret);
            }

            ret = _ov_initprime(ref vf2);

            if (ret != 0)
            {
                return (ret);
            }

            vi1 = ov_info(ref vf1, -1);
            vi2 = ov_info(ref vf2, -1);

            hs1 = ov_halfrate_p(ref vf1);
            hs2 = ov_halfrate_p(ref vf2);

            float** lappcm = stackalloc float*[vi1.channels];

            n1 = Vorbis.vorbis_info_blocksize(ref vi1, 0) >> (1 + hs1);
            n2 = Vorbis.vorbis_info_blocksize(ref vi2, 0) >> (1 + hs2);

            w1 = Vorbis.vorbis_window(ref vf1.vd, 0);
            w2 = Vorbis.vorbis_window(ref vf2.vd, 0);

            for (i = 0; i < vi1.channels; i++)
            {
                float* sub_lappcm = stackalloc float[n1];
                lappcm[i] = sub_lappcm;
            }

            _ov_getlap(ref vf1, ref vi1, ref vf1.vd, lappcm, n1);

            /* have a lapping buffer from vf1; now to splice it into the lapping buffer of vf2 */
            /* consolidate and expose the buffer. */
            Vorbis.vorbis_synthesis_lapout(ref vf2.vd, ref pcm);

            /* splice */
            _ov_splice(pcm, lappcm, n1, n2, vi1.channels, vi2.channels, ref w1, ref w2);

            /* done */
            return 0;
        }

        static int _ov_64_seek_lap(ref OggVorbis_File vf, long pos, ov_64_localseek_func localseek)
        {
            Vorbis.vorbis_info vi;

            float** pcm = null;
            float[] w1, w2;
            int n1, n2, ch1, ch2, hs;
            int i, ret;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            ret = _ov_initset(ref vf);

            if (ret != 0)
            {
                return ret;
            }

            vi = ov_info(ref vf, -1);
            hs = ov_halfrate_p(ref vf);

            ch1 = vi.channels;
            n1 = Vorbis.vorbis_info_blocksize(ref vi, 0) >> (1 + hs);
            w1 = Vorbis.vorbis_window(ref vf.vd, 0);  /* window arrays from libvorbis are persistent; even if the decode state from this link gets dumped, this window array continues to exist */

            float** lappcm = stackalloc float*[ch1];

            for (i = 0; i < ch1; i++)
            {
                float* sub_lappcm = stackalloc float[n1];
                lappcm[i] = sub_lappcm;
            }

            _ov_getlap(ref vf, ref vi, ref vf.vd, lappcm, n1);

            /* have lapping data; seek and prime the buffer */
            ret = localseek(ref vf, pos);

            if (ret != 0)
            {
                return ret;
            }

            ret = _ov_initprime(ref vf);

            if (ret != 0)
            {
                return (ret);
            }

            /* Guard against cross-link changes; they're perfectly legal */
            vi = ov_info(ref vf, -1);
            ch2 = vi.channels;
            n2 = Vorbis.vorbis_info_blocksize(ref vi, 0) >> (1 + hs);
            w2 = Vorbis.vorbis_window(ref vf.vd, 0);

            /* consolidate and expose the buffer. */
            Vorbis.vorbis_synthesis_lapout(ref vf.vd, ref pcm);

            /* splice */
            _ov_splice(pcm, lappcm, n1, n2, ch1, ch2, ref w1, ref w2);

            /* done */
            return 0;
        }

        static public int ov_raw_seek_lap(ref OggVorbis_File vf, long pos)
        {
            return _ov_64_seek_lap(ref vf, pos, ov_raw_seek);
        }

        static public int ov_pcm_seek_lap(ref OggVorbis_File vf, long pos)
        {
            return _ov_64_seek_lap(ref vf, pos, ov_pcm_seek);
        }

        static public int ov_pcm_seek_page_lap(ref OggVorbis_File vf, long pos)
        {
            return _ov_64_seek_lap(ref vf, pos, ov_pcm_seek_page);
        }

        static int _ov_d_seek_lap(ref OggVorbis_File vf, double pos, ov_d_localseek_func localseek)
        {
            Vorbis.vorbis_info vi;

            float** pcm = null;
            float[] w1, w2;
            int n1, n2, ch1, ch2, hs;
            int i, ret;

            if (vf.ready_state < OPENED)
            {
                return Vorbis.OV_EINVAL;
            }

            ret = _ov_initset(ref vf);

            if (ret != 0)
            {
                return ret;
            }

            vi = ov_info(ref vf, -1);
            hs = ov_halfrate_p(ref vf);

            ch1 = vi.channels;
            n1 = Vorbis.vorbis_info_blocksize(ref vi, 0) >> (1 + hs);
            w1 = Vorbis.vorbis_window(ref vf.vd, 0);  /* window arrays from libvorbis are persistent; even if the decode state from this link gets dumped, this window array continues to exist */

            float** lappcm = stackalloc float*[ch1];

            for (i = 0; i < ch1; i++)
            {
                float* sub_lappcm = stackalloc float[n1];
                lappcm[i] = sub_lappcm;
            }

            _ov_getlap(ref vf, ref vi, ref vf.vd, lappcm, n1);

            /* have lapping data; seek and prime the buffer */
            ret = localseek(ref vf, pos);

            if (ret != 0)
            {
                return ret;
            }

            ret = _ov_initprime(ref vf);

            if (ret != 0)
            {
                return (ret);
            }

            /* Guard against cross-link changes; they're perfectly legal */
            vi = ov_info(ref vf, -1);
            ch2 = vi.channels;
            n2 = Vorbis.vorbis_info_blocksize(ref vi, 0) >> (1 + hs);
            w2 = Vorbis.vorbis_window(ref vf.vd, 0);

            /* consolidate and expose the buffer. */
            Vorbis.vorbis_synthesis_lapout(ref vf.vd, ref pcm);

            /* splice */
            _ov_splice(pcm, lappcm, n1, n2, ch1, ch2, ref w1, ref w2);

            /* done */
            return 0;
        }

        static public int ov_time_seek_lap(ref OggVorbis_File vf, double pos)
        {
            return _ov_d_seek_lap(ref vf, pos, ov_time_seek);
        }

        static public int ov_time_seek_page_lap(ref OggVorbis_File vf, double pos)
        {
            return _ov_d_seek_lap(ref vf, pos, ov_time_seek_page);
        }
    }
}