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

 function: basic shared codebook operations
 last mod: $Id: codebook.h 17030 2010-03-25 06:52:55Z xiphmont $

 ********************************************************************/

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Types
    static public unsafe partial class Vorbis
    {
        class static_codebook
        {
            public int dim; /* codebook dimensions (elements per vector) */
            public int entries; /* codebook entries */
            public int* lengthlist; /* codeword lengths in bits */

            /* mapping */
            public int maptype; /* 0=none, 1=implicitly populated values from map column, 2=listed arbitrary values */

            /* The below does a linear, single monotonic sequence mapping. */
            public int q_min; /* packed 32 bit float; quant value 0 maps to minval */
            public int q_delta; /* packed 32 bit float; val 1 - val 0 == delta */
            public int q_quant; /* bits: 0 < quant <= 16 */
            public int q_sequencep; /* bitflag */

            public int* quantlist; /* map == 1: (int)(entries^(1/dim)) element column map, map == 2: list of dim*entries quantized entry vals */
            public int allocedp;
        }

        class codebook
        {
            public int dim; /* codebook dimensions (elements per vector) */
            public int entries; /* codebook entries */
            public int used_entries; /* populated codebook entries */

            public static_codebook c;

            /* for encode, the below are entry-ordered, fully populated */
            /* for decode, the below are ordered by bitreversed codeword and only
              used entries are populated */
            public float* valuelist;  /* list of dim*entries actual entry values */
            public uint* codelist;   /* list of bitstream codewords for each entry */

            public int* dec_index;  /* only used if sparseness collapsed */
            public byte* dec_codelengths;
            public uint* dec_firsttable;
            public int dec_firsttablen;
            public int dec_maxlength;

            /* The current encoder uses only centered, integer-only lattice books. */
            public int quantvals;
            public int minval;
            public int delta;
        }
    }

    // CodeBook
    static public unsafe partial class Vorbis
    {
        static static_codebook vorbis_staticbook_unpack(ref Ogg.oggpack_buffer opb)
        {
            int i, j;
            
            static_codebook s = new static_codebook();
            s.allocedp = 1;

            /* make sure alignment is correct */
            if (Ogg.oggpack_read(ref opb, 24) != 0x564342)
            {
                goto _eofout;
            }

            /* first the basic parameters */
            s.dim = Ogg.oggpack_read(ref opb, 16);
            s.entries = Ogg.oggpack_read(ref opb, 24);

            if (s.entries == -1)
            {
                goto _eofout;
            }

            if (ilog((uint)s.dim) + ilog((uint)s.entries) > 24)
            {
                goto _eofout;
            }

            /* codeword ordering.... length ordered or unordered? */
            switch (Ogg.oggpack_read(ref opb, 1))
            {
                case 0:
                    {
                        int unused;

                        /* allocated but unused entries? */
                        unused = Ogg.oggpack_read(ref opb, 1);

                        if (((s.entries * (unused > 0 ? 1 : 5) + 7) >> 3) > opb.storage - Ogg.oggpack_bytes(ref opb))
                        {
                            goto _eofout;
                        }

                        /* unordered */
                        s.lengthlist = (int*)_ogg_malloc(sizeof(int) * s.entries);

                        /* allocated but unused entries? */
                        if (unused != 0)
                        {
                            /* yes, unused entries */
                            for (i = 0; i < s.entries; i++)
                            {
                                if (Ogg.oggpack_read(ref opb, 1) > 0)
                                {
                                    int num = Ogg.oggpack_read(ref opb, 5);

                                    if (num == -1)
                                    {
                                        goto _eofout;
                                    }

                                    s.lengthlist[i] = num + 1;
                                }
                                else
                                {
                                    s.lengthlist[i] = 0;
                                }
                            }
                        }
                        else
                        {
                            /* all entries used; no tagging */
                            for (i = 0; i < s.entries; i++)
                            {
                                int num = Ogg.oggpack_read(ref opb, 5);

                                if (num == -1)
                                {
                                    goto _eofout;
                                }

                                s.lengthlist[i] = num + 1;
                            }
                        }
                    }
                    break;

                case 1:
                    {
                        /* ordered */
                        int length = Ogg.oggpack_read(ref opb, 5) + 1;

                        if (length == 0)
                        {
                            goto _eofout;
                        }

                        s.lengthlist = (int *)_ogg_malloc(sizeof(int) * s.entries);

                        for (i = 0; i < s.entries; )
                        {
                            int num = Ogg.oggpack_read(ref opb, ilog((uint)(s.entries - i)));

                            if (num == -1)
                            {
                                goto _eofout;
                            }

                            if (length > 32 || num > s.entries - i || num > 0 && ((num - 1) >> (length - 1)) > 1)
                            {
                                for (j = 0; j < num; j++, i++)
                                {
                                    s.lengthlist[i] = length;
                                }

                                length++;
                            }
                        }
                    }
                    break;

                default:
                    {
                        /* EOF */
                        goto _eofout;
                    }
            }

            /* Do we have a mapping to unpack? */
            switch ((s.maptype = Ogg.oggpack_read(ref opb, 4)))
            {
                case 0:
                    {
                        /* no mapping */
                    }
                    break;

                case 1:
                case 2:
                    {
                        /* implicitly populated value mapping */
                        /* explicitly populated value mapping */

                        s.q_min = Ogg.oggpack_read(ref opb, 32);
                        s.q_delta = Ogg.oggpack_read(ref opb, 32);
                        s.q_quant = Ogg.oggpack_read(ref opb, 4) + 1;
                        s.q_sequencep = Ogg.oggpack_read(ref opb, 1);

                        if (s.q_sequencep == -1)
                        {
                            goto _eofout;
                        }

                        {
                            int quantvals = 0;

                            switch (s.maptype)
                            {
                                case 1:
                                    {
                                        quantvals = (s.dim == 0 ? 0 : _book_maptype1_quantvals(s));
                                    }
                                    break;

                                case 2:
                                    {
                                        quantvals = s.entries * s.dim;
                                    }
                                    break;
                            }

                            /* quantized values */
                            if (((quantvals * s.q_quant + 7) >> 3) > opb.storage - Ogg.oggpack_bytes(ref opb))
                            {
                                goto _eofout;
                            }

                            s.quantlist = (int *)_ogg_malloc(sizeof(int) * quantvals);

                            for (i = 0; i < quantvals; i++)
                            {
                                s.quantlist[i] = Ogg.oggpack_read(ref opb, s.q_quant);
                            }

                            if (quantvals > 0 && s.quantlist[quantvals - 1] == -1)
                            {
                                goto _eofout;
                            }
                        }
                    }
                    break;

                default:
                    goto _errout;
            }

            /* all set */
            return s;
            
        _errout:
        _eofout:
            vorbis_staticbook_destroy(ref s);
            return null;
        }

        /* returns the number of bits ************************************************/
        static int vorbis_book_encode(ref codebook book, int a, ref Ogg.oggpack_buffer b)
        {
            if (a < 0 || a >= book.c.entries)
            {
                return 0;
            }

            Ogg.oggpack_write(ref b, book.codelist[a], book.c.lengthlist[a]);
            return book.c.lengthlist[a];
        }

        /* the 'eliminate the decode tree' optimization actually requires the codewords to be MSb first, not LSb.  This is an annoying inelegancy
          (and one of the first places where carefully thought out design turned out to be wrong; Vorbis II and future Ogg codecs should go
          to an MSb bitpacker), but not actually the huge hit it appears to be.  The first-stage decode table catches most words so that
          bitreverse is not in the main execution path. */

        static int decode_packed_entry_number(ref codebook book, ref Ogg.oggpack_buffer b)
        {
            int read = book.dec_maxlength;
            int lo, hi;
            int lok = Ogg.oggpack_look(ref b, book.dec_firsttablen);

            if (lok >= 0)
            {
                uint entry = book.dec_firsttable[lok];

                if ((entry & 0x80000000) != 0)
                {
                    lo = (int)((entry >> 15) & 0x7fff);
                    hi = (int)(book.used_entries - (entry & 0x7fff));
                }
                else
                {
                    Ogg.oggpack_adv(ref b, book.dec_codelengths[entry - 1]);
                    return (int)(entry - 1);
                }
            }
            else
            {
                lo = 0;
                hi = book.used_entries;
            }

            lok = Ogg.oggpack_look(ref b, read);

            while (lok < 0 && read > 1)
            {
                lok = Ogg.oggpack_look(ref b, --read);
            }

            if (lok < 0)
            {
                return -1;
            }

            /* bisect search for the codeword in the ordered list */
            {
                uint testword = bitreverse((uint)lok);

                while (hi - lo > 1)
                {
                    uint p = (uint)((hi - lo) >> 1);
                    uint test = (uint)(book.codelist[lo + p] > testword ? 1 : 0);

                    lo += (int)(p & (test - 1));
                    hi -= (int)(p & -test);
                }

                if (book.dec_codelengths[lo] <= read)
                {
                    Ogg.oggpack_adv(ref b, book.dec_codelengths[lo]);
                    return (lo);
                }
            }

            Ogg.oggpack_adv(ref b, read);
            return (-1);
        }

        /* Decode side is specced and easier, because we don't need to find matches using different criteria; we simply read and map.  There are
          two things we need to do 'depending':

          We may need to support interleave.  We don't really, but it's convenient to do it here rather than rebuild the vector later.

          Cascades may be additive or multiplicitive; this is not inherent in the codebook, but set in the code using the codebook.  Like
          interleaving, it's easiest to do it here.
          
          addmul==0 -> declarative (set the value)
          addmul==1 -> additive
          addmul==2 -> multiplicitive */

        /* returns the [original, not compacted] entry number or -1 on eof *********/
        static int vorbis_book_decode(ref codebook book, ref Ogg.oggpack_buffer b)
        {
            if (book.used_entries > 0)
            {
                int packed_entry = decode_packed_entry_number(ref book, ref b);

                if (packed_entry >= 0)
                {
                    return book.dec_index[packed_entry];
                }
            }

            /* if there's no dec_index, the codebook unpacking isn't collapsed */
            return -1;
        }

        /* returns 0 on OK or -1 on eof *************************************/
        /* decode vector / dim granularity gaurding is done in the upper layer */
        static int vorbis_book_decodevs_add(ref codebook book, ref float[] a, ref Ogg.oggpack_buffer b, int n)
        {
            if (book.used_entries > 0)
            {
                int i, j, o;
                int step = n / book.dim;
                int[] entry = new int[step];

                for (i = 0; i < step; i++)
                {
                    entry[i] = decode_packed_entry_number(ref book, ref b);

                    if (entry[i] == -1)
                    {
                        return -1;
                    }
                }

                for (i = 0, o = 0; i < book.dim; i++, o += step)
                {
                    for (j = 0; j < step; j++)
                    {
                        a[o + j] += book.valuelist[entry[j] * book.dim + i];
                    }
                }
            }

            return 0;
        }

        /* decode vector / dim granularity guarding is done in the upper layer */
        static int vorbis_book_decodev_add(ref codebook book, float* a, ref Ogg.oggpack_buffer b, int n)
        {
            if (book.used_entries > 0)
            {
                int i, j, entry;

                if (book.dim > 8)
                {
                    for (i = 0; i < n; )
                    {
                        entry = decode_packed_entry_number(ref book, ref b);

                        if (entry == -1)
                        {
                            return -1;
                        }

                        for (j = 0; j < book.dim; )
                        {
                            a[i++] += book.valuelist[entry * book.dim + j++];
                        }
                    }
                }
                else
                {
                    for (i = 0; i < n; )
                    {
                        entry = decode_packed_entry_number(ref book, ref b);

                        if (entry == -1)
                        {
                            return -1;
                        }

                        j = 0;

                        switch (book.dim)
                        {
                            case 8:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 7;

                            case 7:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 6;

                            case 6:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 5;

                            case 5:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 4;

                            case 4:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 3;

                            case 3:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 2;

                            case 2:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                goto case 1;

                            case 1:
                                {
                                    a[i++] += book.valuelist[entry * book.dim + j++];
                                }
                                break;
                        }
                    }
                }
            }

            return 0;
        }

        /* unlike the others, we guard against n not being an integer number of <dim> internally rather than in the upper layer (called only by floor0) */
        static int vorbis_book_decodev_set(ref codebook book, float *a, ref Ogg.oggpack_buffer b, int n)
        {
            if (book.used_entries > 0) 
            {
                int i, j, entry;
                
                for (i = 0; i < n; )
                {
                    entry = decode_packed_entry_number(ref book, ref b);
                    
                    if (entry == -1) {
                        return -1;
                    }
                    
                    float *t = book.valuelist + entry * book.dim;
                    
                    for (j = 0; i < n && j < book.dim; ) {
                        a[i++] = t[j++];
                    }
                }
            }
            else
            {
                for (int i = 0; i < n; ) {
                    a[i++] = 0.0f;
                }
            }
            
            return 0;
        }

        static int vorbis_book_decodevv_add(ref codebook book, float** a, int offset, int ch, ref Ogg.oggpack_buffer b, int n)
        {
            int i, j, entry;
            int chptr = 0;

            if (book.used_entries > 0)
            {
                for (i = offset / ch; i < (offset + n) / ch; )
                {
                    entry = decode_packed_entry_number(ref book, ref b);

                    if (entry == -1)
                    {
                        return -1;
                    }
                    else
                    {
                        for (j = 0; j < book.dim; j++)
                        {
                            a[chptr++][i] += book.valuelist[entry * book.dim + j];

                            if (chptr == ch)
                            {
                                chptr = 0;
                                i++;
                            }
                        }
                    }
                }
            }

            return 0;
        }
    }
}
