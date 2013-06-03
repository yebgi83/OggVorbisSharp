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
 last mod: $Id: sharedbook.c 17030 2010-03-25 06:52:55Z xiphmont $

 ********************************************************************/

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        /* 32 bit float (not IEEE; nonnormalized mantissa + biased exponent) : neeeeeee eeemmmmm mmmmmmmm mmmmmmmm
          Why not IEEE?  It's just not that important here. */
        public const int VQ_FEXP = 10;
        public const int VQ_FMAN = 21;
        public const int VQ_FEXP_BIAS = 768; /* bias toward values small than 1. */
    }
    
    // SharedBook
    static public unsafe partial class Vorbis
    {
        /* floor macro */
        static public int floor(double x)
        {
            return (int)Math.Floor(x);
        }
        
        /* rint macro */
        static public int rint(double x)
        {
            return (int)Math.Floor(x + 0.5f);
        }
        
        /* ldexp macro */
        static public float ldexp(double x, double exp)
        {
            return (float)(x * Math.Pow(2, exp));
        }
        
        /* doesn't currently guard under/overflow */        
        static public uint _float32_pack(float val)
        {
            uint sign = 0;
            uint exp;
            uint mant;
            
            if (val < 0) 
            {
                sign = 0x80000000;
                val -= val;
            }
            
            exp = (uint)floor(Math.Log(val) / Math.Log(2.0f) + .001f); // +epsilon
            mant = (uint)rint(ldexp(val, (VQ_FMAN - 1) - exp));
            exp = (uint)(exp + VQ_FEXP_BIAS) << VQ_FMAN;
            
            return sign | exp | mant;
        }
        
        static public float _float32_unpack(uint val) 
        {
            double mant = val & 0x1fffff;
            uint sign = val & 0x80000000;
            uint exp = (val & 0x7fe00000) >> VQ_FMAN;
            
            if (sign != 0) {
                mant = -mant;
            }
            
            return (float)ldexp(mant, (int)exp - (VQ_FMAN - 1) - VQ_FEXP_BIAS);
        }
        
        /* given a list of word lengths, generate a list of codewords.  Works for length ordered or unordered, always assigns the lowest valued
          codewords first.  Extended to handle unused entries (length 0) */        
        static public uint* _make_words(int* l, int n, int sparsecount)
        {
            int i, j, count = 0;
            uint[] marker = new uint[33];
            uint* r = (uint*)_ogg_malloc((sparsecount > 0 ? sparsecount : n) * sizeof(uint));

            for (i = 0; i < n; i++)
            {
                int length = l[i];

                if (length > 0)
                {
                    uint entry = marker[length];

                    /* when we claim a node for an entry, we also claim the nodes below it (pruning off the imagined tree that may have dangled
                    from it) as well as blocking the use of any nodes directly above for leaves */

                    /* update ourself */
                    if (length < 32 && (entry >> length) != 0)
                    {
                        /* error condition; the lengths must specify an overpopulated tree */
                        return null;
                    }

                    r[count++] = entry;

                    /* Look to see if the next shorter marker points to the node above. if so, update it and repeat.  */
                    {
                        for (j = length; j > 0; j--)
                        {
                            if ((marker[j] & 1) != 0)
                            {
                                /* have to jump branches */
                                if (j == 1)
                                {
                                    marker[1]++;
                                }
                                else
                                {
                                    marker[j] = marker[j - 1] << 1;
                                }

                                /* invariant says next upper marker would already have been moved if it was on the same path */
                                break;
                            }

                            marker[j]++;
                        }
                    }

                    /* prune the tree; the implicit invariant says all the longer markers were dangling from our just-taken node.  Dangle them from our *new* node. */
                    for (j = length + 1; j < 33; j++)
                    {
                        if ((marker[j] >> 1) == entry)
                        {
                            entry = marker[j];
                            marker[j] = marker[j - 1] << 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    if (sparsecount == 0)
                    {
                        count++;
                    }
                }
            }

            /* sanity check the huffman tree; an underpopulated tree must be rejected. The only exception is the one-node pseudo-nil tree,
             which appears to be underpopulated because the tree doesn't really exist; there's only one possible 'codeword' or zero bits,
             but the above tree-gen code doesn't mark that. */
            if (sparsecount != 1)
            {
                for (i = 1; i < 33; i++)
                {
                    if ((marker[i] & (0xffffffff >> (32 - i))) != 0)
                    {
                        _ogg_free(r);
                        return null;
                    }
                }
            }

            /* bitreverse the words because our bitwise packer/unpacker is LSb endian */
            for (i = 0, count = 0; i < n; i++)
            {
                uint temp = 0;

                for (j = 0; j < l[i]; j++)
                {
                    temp <<= 1;
                    temp |= (r[count] >> j) & 1;
                }

                if (sparsecount > 0)
                {
                    if (l[i] != 0)
                    {
                        r[count++] = temp;
                    }
                }
                else
                {
                    r[count++] = temp;
                }
            }

            return r;
        }
        
        /* there might be a straightforward one-line way to do the below that's portable and totally safe against roundoff, but I haven't
          thought of it.  Therefore, we opt on the side of caution */
        static int _book_maptype1_quantvals(static_codebook b)
        {
            int vals = floor(Math.Pow((float)b.entries, 1.0f / b.dim));
            
            /* the above *should* be reliable, but we'll not assume that FP is ever reliable when bitstream sync is at stake; verify via integer
             means that vals really is the greatest value of dim for which vals^b->bim <= b->entries */

            /* treat the above as an initial guess */
            while(true)
            {
                int acc = 1;
                int acc1 = 1;
                
                for (int i = 0; i < b.dim; i++) 
                {
                    acc *= vals;
                    acc1 *= vals + 1;
                }
                
                if (acc <= b.entries && acc1 > b.entries)
                {
                    return vals;
                }
                else
                {
                    if (acc > b.entries) {
                        vals--;
                    } 
                    else {
                        vals++;
                    }
                }
            }
        }
        
        /* unpack the quantized list of values for encode/decode */
        /* we need to deal with two map types: in map type 1, the values are generated algorithmically (each column of the vector counts through
          the values in the quant vector). in map type 2, all the values came in in an explicit list.  Both value lists must be unpacked */
        static float* _book_unquantize(static_codebook b, int n, ref int[] sparsemap)
        {
            int count = 0;
        
            if (b.maptype == 1 || b.maptype == 2)
            {
                int quantvals;
                
                float mindel = _float32_unpack((uint)b.q_min);
                float delta = _float32_unpack((uint)b.q_delta);
                float *r = (float *)_ogg_calloc(n * b.dim, sizeof(float));
                
                /* maptype 1 and 2 both use a quantized value vector, but different sizes */
                switch(b.maptype)
                {
                    case 1:
                    {
                        /* most of the time, entries%dimensions == 0, but we need to be well defined.  We define that the possible vales at each
                        scalar is values == entries/dim.  If entries%dim != 0, we'll have 'too few' values (values*dim<entries), which means that
                        we'll have 'left over' entries; left over entries use zeroed values (and are wasted).  So don't generate codebooks like that */
                        quantvals = _book_maptype1_quantvals(b);
                        
                        for (int j = 0; j < b.entries; j++)
                        {
                            if((sparsemap != null && b.lengthlist[j] > 0) || sparsemap == null)
                            {
                                float last = 0.0f;
                                int indexdiv = 1;
                                
                                for (int k = 0; k < b.dim; k++) 
                                {
                                    int index = (int)((j / indexdiv) % quantvals);
                                    float val = b.quantlist[index];
                                    
                                    val = Math.Abs(val) * delta + mindel + last;
                                    
                                    if (b.q_sequencep > 0) { 
                                        last = val;
                                    }
                                    
                                    if (sparsemap != null) {
                                        r[sparsemap[count] * b.dim + k] = val;
                                    } else {
                                        r[count * b.dim + k] = val;
                                    }
                                    
                                    indexdiv *= quantvals;
                                }
                                
                                count++;
                            }
                        }
                    }
                    break;
                    
                    case 2:
                    {
                        for (int j = 0; j < b.entries; j++) 
                        {
                            if ((sparsemap != null && b.lengthlist[j] > 0) || sparsemap == null) 
                            {
                                float last = 0.0f;
                                
                                for (int k = 0; k < b.dim; k++)
                                {
                                    float val = b.quantlist[j * b.dim + k];
                                    
                                    val = Math.Abs(val) * delta + mindel + last;
                                    
                                    if (b.q_sequencep > 0) {
                                        last = val;
                                    } 
                                    
                                    if (sparsemap != null) {
                                        r[sparsemap[count] * b.dim + k] = val;
                                    } else {
                                        r[count * b.dim + k] = val;
                                    }
                                    
                                    count++;
                                }
                            }
                        }
                    }
                    break;
                }
                
                return r;
            }            
            
            return null;                               
        }
        
        static void vorbis_staticbook_destroy(ref static_codebook b)
        {
            if (b.allocedp != 0) 
            {
                if (b.quantlist != null) {
                    _ogg_free(b.quantlist);
                }
                
                if (b.lengthlist != null) {
                    _ogg_free(b.lengthlist);
                }
                
                b = null;
            }
        }
        
        static void vorbis_book_clear(ref codebook b) 
        {
            if (b != null)
            {
                /* static book is not cleared; we're likely called on the lookup and the static codebook belongs to the info struct */
                if (b.valuelist != null) {
                    _ogg_free(b.valuelist);
                }
                
                if (b.codelist != null) {
                    _ogg_free(b.codelist);
                }
                
                if (b.dec_index != null) {
                    _ogg_free(b.dec_index);
                }
                
                if (b.dec_codelengths != null) {
                    _ogg_free(b.dec_codelengths);
                }
                
                if (b.dec_firsttable != null) {
                    _ogg_free(b.dec_firsttable);
                }
                
                b.dim = 0;
                b.entries = 0;
                b.used_entries = 0;
                
                b.c = null;
                
                b.valuelist = null;
                b.codelist = null;
                
                b.dec_index = null;
                b.dec_codelengths = null;
                b.dec_firsttable = null;
                b.dec_firsttablen = 0;
                b.dec_maxlength = 0;

                b.quantvals = 0;
                b.minval = 0;
                b.delta = 0;
            }
        }
        
        static int vorbis_book_init_encode(ref codebook c, static_codebook s)
        {
            c.dim = s.dim;
            c.entries = s.entries;
            c.used_entries= s.entries;
            
            c.c = s;
            
            // c.valuelist = _book_unquantize(s, ref s.entries, null);
            c.codelist = (uint *)_make_words(s.lengthlist, s.entries, 0);
            
            c.dec_index = null;
            c.dec_codelengths = null;
            c.dec_firsttable = null;
            
            c.dec_firsttablen = 0;
            c.dec_maxlength = 0;
            
            c.quantvals = (int)_book_maptype1_quantvals(s);
            c.minval = (int)rint(_float32_unpack((uint)s.q_min));
            c.delta = (int)rint(_float32_unpack((uint)s.q_delta));
            
            return 0;
        }
        
        /* decode codebook arrangement is more heavily optimized than encode */
        static int vorbis_book_init_decode(ref codebook c, static_codebook s)
        {
            int i, tabn;
            int n = 0;
            int[] sortindex;

            /* count actually used entires */
            for (i = 0; i < s.entries; i++)
            {
                if (s.lengthlist[i] > 0)
                {
                    n++;
                }
            }

            c.dim = s.dim;
            c.entries = s.entries;
            c.used_entries = n;

            c.c = null;

            c.valuelist = null;
            c.codelist = null;

            c.dec_index = null;
            c.dec_codelengths = null;
            c.dec_firsttable = null;
            c.dec_firsttablen = 0;
            c.dec_maxlength = 0;

            c.quantvals = 0;
            c.minval = 0;
            c.delta = 0;

            if (n > 0)
            {
                /* two different remappings go on here.

                 First, we collapse the likely sparse codebook down only to
                 actually represented values/words.  This collapsing needs to be
                 indexed as map-valueless books are used to encode original entry
                 positions as integers.

                 Second, we reorder all vectors, including the entry index above,
                 by sorted bitreversed codeword to allow treeless decode. */

                /* perform sort */
                uint* codes = _make_words(s.lengthlist, s.entries, c.used_entries);
                uint[] codep = new uint[n];

                if (codes == null)
                {
                    goto err_out;
                }

                for (i = 0; i < n; i++)
                {
                    codes[i] = bitreverse(codes[i]);
                    codep[i] = (uint)i;
                }

                Array.Sort
                (
                    codep,
                    (arg1, arg2) =>
                    {
                        return (codes[arg1] > codes[arg2] ? 1 : 0) - (codes[arg1] < codes[arg2] ? 1 : 0);
                    }
                );

                sortindex = new int[n];
                c.codelist = (uint*)_ogg_malloc(n * sizeof(uint));

                /* the index is a reverse index */
                for (i = 0; i < n; i++)
                {
                    sortindex[codep[i]] = i;
                }

                for (i = 0; i < n; i++)
                {
                    c.codelist[sortindex[i]] = codes[i];
                }

                codes = null;

                c.valuelist = _book_unquantize(s, n, ref sortindex);
                c.dec_index = (int*)_ogg_malloc(n * sizeof(int));

                for (n = 0, i = 0; i < s.entries; i++)
                {
                    if (s.lengthlist[i] > 0)
                    {
                        c.dec_index[sortindex[n++]] = i;
                    }
                }

                c.dec_codelengths = (byte*)_ogg_malloc(n * sizeof(byte));

                for (n = 0, i = 0; i < s.entries; i++)
                {
                    if (s.lengthlist[i] > 0)
                    {
                        c.dec_codelengths[sortindex[n++]] = (byte)s.lengthlist[i];
                    }
                }

                c.dec_firsttablen = ilog((uint)c.used_entries) - 4; /* this is magic */

                if (c.dec_firsttablen < 5)
                {
                    c.dec_firsttablen = 5;
                }

                if (c.dec_firsttablen > 8)
                {
                    c.dec_firsttablen = 8;
                }

                tabn = 1 << c.dec_firsttablen;
                c.dec_firsttable = (uint*)_ogg_calloc(tabn, sizeof(uint));
                c.dec_maxlength = 0;

                for (i = 0; i < n; i++)
                {
                    if (c.dec_maxlength < c.dec_codelengths[i])
                    {
                        c.dec_maxlength = c.dec_codelengths[i];
                    }

                    if (c.dec_codelengths[i] <= c.dec_firsttablen)
                    {
                        uint orig = bitreverse(c.codelist[i]);

                        for (int j = 0; j < (1 << (c.dec_firsttablen - c.dec_codelengths[i])); j++)
                        {
                            c.dec_firsttable[orig | (uint)(j << c.dec_codelengths[i])] = (uint)(i + 1);
                        }
                    }
                }

                /* now fill in 'unused' entries in the firsttable with hi/lo search hints for the non-direct-hits */
                {
                    uint mask = 0xfffffffe << (31 - c.dec_firsttablen);
                    uint lo = 0, hi = 0;

                    for (i = 0; i < tabn; i++)
                    {
                        uint word = (uint)(i << (32 - c.dec_firsttablen));

                        if (c.dec_firsttable[bitreverse(word)] == 0)
                        {
                            while ((lo + 1) < n && c.codelist[lo + 1] <= word)
                            {
                                lo++;
                            }

                            while (hi < n && word >= (c.codelist[hi] & mask))
                            {
                                hi++;
                            }

                            /* we only actually have 15 bits per hint to play with here. In order to overflow gracefully (nothing breaks, efficiency
                            just drops), encode as the difference from the extremes. */
                            {
                                uint loval = lo;
                                uint hival = (uint)(n - hi);

                                if (loval > 0x7fff)
                                {
                                    loval = 0x7fff;
                                }

                                if (hival > 0x7fff)
                                {
                                    hival = 0x7fff;
                                }

                                c.dec_firsttable[bitreverse(word)] = 0x80000000 | (loval << 15) | hival;
                            }
                        }
                    }
                }
            }

            return 0;

        err_out:
            vorbis_book_clear(ref c);
            return -1;
        }

        static int vorbis_book_codeword(ref codebook book, int entry)
        {
            if (book.c != null) {
                /* only use with encode; decode optimizations are allowed to break this */
                return (int)book.codelist[entry];
            }
            else {
                return -1;
            }
        }

        static int vorbis_book_codelen(ref codebook book, int entry)
        {
            if (book.c != null) {
                /* only use with encode; decode optimizations are allowed to break this */
                return book.c.lengthlist[entry];
            }
            else {
                return -1;
            }
        }        
    }
}