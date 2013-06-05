/********************************************************************
 *                                                                  *
 * THIS FILE IS PART OF THE OggVorbis SOFTWARE CODEC SOURCE CODE.   *
 * USE, DISTRIBUTION AND REPRODUCTION OF THIS LIBRARY SOURCE IS     *
 * GOVERNED BY A BSD-STYLE SOURCE LICENSE INCLUDED WITH THIS SOURCE *
 * IN 'COPYING'. PLEASE READ THESE TERMS BEFORE DISTRIBUTING.       *
 *                                                                  *
 * THE OggVorbis SOURCE CODE IS (C) COPYRIGHT 1994-2010             *
 * by the Xiph.Org Foundation http://www.xiph.org/                  *
 *                                                                  *
 ********************************************************************

 function: residue backend 0, 1 and 2 implementation
 last mod: $Id: res0.c 17556 2010-10-21 18:25:19Z tterribe $

 ********************************************************************/
  
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

/* Slow, slow, slow, simpleminded and did I mention it was slow?  The
   encode/decode loops are coded for clarity and performance is not
   yet even a nagging little idea lurking in the shadows.  Oh and BTW,
   it's slow. */

namespace OggVorbisSharp
{
    // Delegate 
    static public unsafe partial class Vorbis
    {
        delegate int vorbis_func_encode(ref Ogg.oggpack_buffer opb, int *vec, int n, ref codebook book, ref int[] acc);
        delegate int vorbis_func_decode(ref codebook c, float *a, ref Ogg.oggpack_buffer b, int n);
    }

    // Types
    static public unsafe partial class Vorbis
    {
        class vorbis_look_residue0 : vorbis_look_residue 
        {
            public vorbis_info_residue0 info;
            
            public int parts;
            public int stages;
            
            public codebook[] fullbooks;
            public codebook phrasebook;
            public codebook[][] partbooks;
            
            public int partvals;
            public int **decodemap;
            
            public int postbits;
            public int phrasebits;
            public int frames;
        }
    }
    
    // Res0
    static public unsafe partial class Vorbis
    {
        static void res0_free_info(ref vorbis_info_residue vir)
        {
            vorbis_info_residue0 info = vir as vorbis_info_residue0;
            
            if (info != null) {
                info = null;
            }
        }
        
        static void res0_free_look(ref vorbis_look_residue vlr)
        {
            vorbis_look_residue0 look = vlr as vorbis_look_residue0;
            
            if (look != null) {
                look = null;
            }
        }
        
        static void res0_pack(vorbis_info_residue vr, ref Ogg.oggpack_buffer opb)
        {
            vorbis_info_residue0 info = vr as vorbis_info_residue0;
            
            int j, acc = 0;
            
            Ogg.oggpack_write(ref opb, (uint)info.begin, 24);
            Ogg.oggpack_write(ref opb, (uint)info.end, 24);
            Ogg.oggpack_write(ref opb, (uint)info.grouping - 1, 24); /* residue vectors to group and code with a partitioned book */
            Ogg.oggpack_write(ref opb, (uint)info.partitions - 1, 6); /* possible partition choices */
            Ogg.oggpack_write(ref opb, (uint)info.groupbook, 8); /* group huffman book */
            
            /* secondstages is a bitmask; as encoding progresses pass by pass, a bitmask of one indicates this partition class has bits to write this pass */
            for (j = 0; j < info.partitions; j++) 
            {
                if (ilog((uint)info.secondstages[j]) > 3) 
                {
                    /* yes, this is a minor hack due to not thinking ahead */
                    Ogg.oggpack_write(ref opb, (uint)info.secondstages[j], 3);
                    Ogg.oggpack_write(ref opb, 1, 1);
                    Ogg.oggpack_write(ref opb, (uint)info.secondstages[j] >> 3, 5);
                }
                else
                {
                    Ogg.oggpack_write(ref opb, (uint)info.secondstages[j], 4);
                }
                
                acc += icount((uint)info.secondstages[j]);
            }
            
            for (j = 0; j < acc; j++) {
                Ogg.oggpack_write(ref opb, (uint)info.booklist[j], 8);
            }
        } 
        
        /* vorbis_info is for range checking */
        static vorbis_info_residue res0_unpack(ref vorbis_info vi, ref Ogg.oggpack_buffer opb) 
        {
            int j, acc = 0;
            
            vorbis_info_residue _info = new vorbis_info_residue0();
            vorbis_info_residue0 info = _info as vorbis_info_residue0;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
        
            info.begin = Ogg.oggpack_read(ref opb, 24);
            info.end = Ogg.oggpack_read(ref opb, 24);
            info.grouping = Ogg.oggpack_read(ref opb, 24) + 1;
            info.partitions = Ogg.oggpack_read(ref opb, 6) + 1;
            info.groupbook = Ogg.oggpack_read(ref opb, 8);
            
            /* check for prematrue EOP */
            if (info.groupbook < 0) 
            {
                goto errout;
            }
           
            for (j = 0; j < info.partitions; j++) 
            {
                int cascade = Ogg.oggpack_read(ref opb, 3);
                int cflag = Ogg.oggpack_read(ref opb, 1);
                
                if (cflag < 0) 
                {
                   goto errout;
                }
                
                if (cflag != 0) 
                {
                    int c = Ogg.oggpack_read(ref opb, 5);
                    
                    if (c < 0) 
                    {
                        goto errout;
                    }
                    
                    cascade |= (c << 3);
                }
                
                info.secondstages[j] = cascade;
                acc += icount((uint)cascade);
            }
            
            for (j = 0; j < acc; j++) 
            {
                int book = Ogg.oggpack_read(ref opb, 8);
                
                if (book < 0) 
                {
                    goto errout;
                }
                
                info.booklist[j] = book;
            }
            
            if (info.groupbook >= ci.books) 
            {
                goto errout;
            }
            
            for (j = 0; j < acc; j++) 
            {
                if (info.booklist[j] >= ci.books) 
                {
                    goto errout;
                }
                
                if (ci.book_param[info.booklist[j]].maptype == 0) 
                {
                    goto errout;
                }
            }
             
            /* verify the phrasebook is not specifying an impossible or inconsistent partitioning scheme. */
            /* modify the phrasebook ranging check from r16327; an early beta encoder had a bug where it used an oversized phrasebook by
             accident.  These files should continue to be playable, but don't allow an exploit */
            {
                int entires = ci.book_param[info.groupbook].entries;
                int dim = ci.book_param[info.groupbook].dim;
                int partvals = 1;
                
                if (dim < 1) 
                {
                    goto errout;
                }
                
                while (dim > 0)
                {
                    partvals *= info.partitions;
                    
                    if (partvals > entires) 
                    {
                        goto errout;
                    }
                    
                    dim--;
                }
                
                info.partvals = partvals;
            }
            
            return info;
            
        errout:
            res0_free_info(ref _info);
            return null;
        }
        
        static vorbis_look_residue res0_look(ref vorbis_dsp_state vd, vorbis_info_residue vir)
        {
            vorbis_info_residue0 info = vir as vorbis_info_residue0;
            vorbis_look_residue0 look = new vorbis_look_residue0();
            codec_setup_info ci = vd.vi.codec_setup as codec_setup_info;
            
            int j, k, acc = 0;
            int dim;
            int maxstage = 0;
            
            look.info = info;
            look.parts = info.partitions;
            look.fullbooks = ci.fullbooks;
            look.phrasebook = ci.fullbooks[info.groupbook];
            
            dim = look.phrasebook.dim;
            look.partbooks = new codebook[look.parts][];
            
            for (j = 0; j < look.parts; j++)
            {
                int stages = ilog((uint)info.secondstages[j]);
                
                if (stages != 0)
                {
                    if (stages > maxstage) {
                        maxstage = stages;
                    }
                    
                    look.partbooks[j] = new codebook[stages];
                   
                    for (k = 0; k < stages; k++)
                    {
                        if ((info.secondstages[j] & (1 << k)) != 0) {
                            look.partbooks[j][k] = ci.fullbooks[info.booklist[acc++]];
                        }
                    }
                }                    
            }
            
            look.partvals = 1;
            
            for (j = 0; j < dim; j++) {
                look.partvals *= look.parts;
            }
            
            look.stages = maxstage;
            look.decodemap = (int **)_ogg_malloc(look.partvals * sizeof(int *));
            
            for (j = 0; j < look.partvals; j++) 
            {
                int val = j;
                int mult = look.partvals / look.parts;
                
                look.decodemap[j] = (int *)_ogg_malloc(dim * sizeof(int));
                
                for (k = 0; k < dim; k++) 
                {
                    int deco = val / mult;
                    
                    val -= deco * mult;
                    mult /= look.parts;
                    
                    look.decodemap[j][k] = deco;
                }
            }
            
            return look;
        }
        
        /* break an abstraction and copy some code for performance purposes */
        static int local_book_besterror(ref codebook book, int *a)
        {
            int dim = book.dim;
            int i, j, o;
            int minval = book.minval;
            int del = book.delta;
            int qv = book.quantvals;
            int ze = qv >> 1;
            int index = 0;
            
            /* assumes integer/centered encoder codebook maptype 1 no more than dim 8 */
            int[] p = { 0, 0, 0, 0, 0, 0, 0, 0 };

            if (del != 1)
            {
                for (i = 0, o = dim; i < dim; i++)
                {
                    int v = (a[--o] - minval + (del >> 1)) / del;
                    int m = (v < ze ? ((ze - v) << 1) - 1 : ((v - ze) << 1));
                    
                    index = index * qv + (m < 0 ? 0 : (m >= qv ? qv - 1 : m));
                    p[o] = v * del + minval;
                }
            }
            else
            {
                for (i = 0, o = dim; i < dim; i++)
                {
                    int v = a[--o] - minval;
                    int m = (v < ze ? ((ze - v) << 1) - 1 : ((v - ze) << 1));
                    
                    index = index * qv + (m < 0 ? 0 : (m >= qv ? qv - 1 : m));
                    p[o] = v * del + minval;
                }
            }

            if (book.c.lengthlist[index] <= 0)
            {
                static_codebook c = book.c;
                int best = -1;
                
                /* assumes integer/centered encoder codebook maptype 1 no more than dim 8 */
                int[] e = { 0, 0, 0, 0, 0, 0, 0, 0 };
                int maxval = book.minval + book.delta * (book.quantvals - 1);
                
                for (i = 0; i < book.entries; i++)
                {
                    if (c.lengthlist[i] > 0)
                    {
                        int _this = 0;
                        
                        for (j = 0; j < dim; j++)
                        {
                            int val = (e[j] - a[j]);
                            _this += val * val;
                        }
                        
                        if (best == -1 || _this < best)
                        {
                            p = e;
                            best = _this;
                            index = i;
                        }
                    }
                  
                    /* assumes the value patterning created by the tools in vq/ */
                    j = 0;
                  
                    while (e[j] >= maxval) {
                        e[j++] = 0;
                    }
                    
                    if (e[j] >= 0) {
                        e[j] += book.delta;
                    }
                    
                    e[j] = -e[j];
                }
            }  
            
            if (index > -1) 
            {
                for (i = 0; i < dim; i++) {
                    *(a++) -= p[i];
                }
            }    
            
            return index;      
        }
        
        static int _encodepart(ref Ogg.oggpack_buffer opb, int* vec, int n, ref codebook book, ref int[] acc)
        {
            int i, bits = 0;
            int dim = book.dim;
            int step = n / dim;
            
            for (i = 0; i < step; i++) 
            {
                int entry = local_book_besterror(ref book, vec + i * dim);
                bits += vorbis_book_encode(ref book, entry, ref opb);
            }
            
            return bits;
        }
        
        static int** _01class(ref vorbis_block vb, vorbis_look_residue vlr, int** _in, int ch)
        {
            int i, j, k;
            vorbis_look_residue0 look = vlr as vorbis_look_residue0;
            vorbis_info_residue0 info = look.info;

            /* move all this setup out later */
            int samples_per_partition = info.grouping;
            int possible_partitions = info.partitions;
            int n = info.end - info.begin;

            int partvals = n / samples_per_partition;
            int** partword = (int **)_vorbis_block_alloc(ref vb, ch * sizeof(int *));
            float scale = 100.0f / samples_per_partition;

            /* we find the partition type for each partition of each channel. We'll go back and do the interleaved encoding in a bit. For now, clarity */
            
            for (i = 0; i < ch; i++) {
                partword[i] = (int *)_vorbis_block_alloc(ref vb, n / samples_per_partition * sizeof(int));
                ZeroMemory(partword[i], n / samples_per_partition * sizeof(int));
            }
            
            for (i = 0; i < partvals; i++) 
            {
                int offset = i * samples_per_partition * info.begin;
                
                for (j = 0; j < ch; j++) 
                {
                    int max = 0;
                    int ent = 0;
                    
                    for (k = 0; k < samples_per_partition; k++) 
                    {
                        if (Math.Abs(_in[j][offset + k]) > max) {
                            max = Math.Abs(_in[j][offset + k]);
                        }
                        
                        ent += Math.Abs(_in[j][offset + k]);
                    }
                    
                    ent *= (int)scale;
                    
                    for (k = 0; k < possible_partitions - 1; k++) 
                    {
                        if (max <= info.classmetric1[k] && (info.classmetric2[k] < 0 || ent < info.classmetric2[k])) {
                            break;
                        }
                        
                        partword[j][i] = k;
                    }
                }
            }
                
            look.frames++;
            return partword;
        }
        
        /* designed for stereo or other modes where the partition size is an integer multiple of the number of channels encoded in the current submap */
        static int** _2class(ref vorbis_block vb, vorbis_look_residue vlr, int** a, int ch)
        {
            int i, j, k, l;
            
            vorbis_look_residue0 look = vlr as vorbis_look_residue0;
            vorbis_info_residue0 info = look.info;
            
            /* move all this setup out later */
            int samples_per_partition = info.grouping;
            int possible_partitions = info.partitions;
            int n = info.end - info.begin;
            
            int partvals = n / samples_per_partition;
            int** partword = (int **)_vorbis_block_alloc(ref vb, sizeof(int *));
            
            partword[0] = (int *)_vorbis_block_alloc(ref vb, partvals * sizeof(int));
            ZeroMemory(partword[0], partvals * sizeof(int));
            
            for (i = 0, l = info.begin / ch; i < partvals; i++)
            {
                int magmax = 0;
                int angmax = 0;

                for (j = 0; j < samples_per_partition; j += ch)
                {
                    if (Math.Abs(a[0][l]) > magmax)
                    {
                        magmax = Math.Abs(a[0][l]);
                    }

                    for (k = 1; k < ch; k++)
                    {
                        if (Math.Abs(a[k][l]) > angmax)
                        {
                            angmax = Math.Abs(a[k][l]);
                        }
                    }

                    l++;
                }

                for (j = 0; j < possible_partitions - 1; j++)
                {
                    if (magmax <= info.classmetric1[j] && angmax <= info.classmetric2[j])
                    {
                        break;
                    }

                    partword[0][j] = j;
                }
            }
            
            look.frames++;
            return partword;
        }
        
        static int _01forward(ref Ogg.oggpack_buffer opb, ref vorbis_block vb, vorbis_look_residue vl, int** _in, int ch, int** partword, vorbis_func_encode encode, int submap) 
        {
            int i, j, k, s;
            
            vorbis_look_residue0 look = vl as vorbis_look_residue0;
            vorbis_info_residue0 info = look.info;
            
            /* move all this setup out later */
            int samples_per_partition = info.grouping;
            int possible_partitions = info.partitions;
            int partitions_per_word = look.phrasebook.dim;
            int n = info.end - info.begin;
            
            int partvals = n / samples_per_partition;
            int[] resbits = new int[128];
            int[] resvals = new int[128];

            /* we code the partition words for each channel, then the residual words for a partition per channel until we've written all the
             residual words for that partition word.  Then write the next partition channel words... */
            
            for (s = 0; s < look.stages; s++) 
            {
                for (i = 0; i < partvals;)
                {
                    /* first we encode a partition codeword for each channel */
                    if (s == 0) 
                    {
                        for (j = 0; j < ch; j++) 
                        {
                            int val = partword[j][i];
                            
                            for (k = 1; k < partitions_per_word; k++) 
                            {
                                val *= possible_partitions;
                                
                                if (i + k < partvals) {
                                    val += partword[j][i + k];
                                }
                            }
                            
                            /* training hack */
                            if (val < look.phrasebook.entries) {
                                look.phrasebits += vorbis_book_encode(ref look.phrasebook, val, ref opb);
                            }
                        }
                    }
                    
                    /* now we encode interlaved residual values for the partitions */
                    for (k = 0; k < partitions_per_word && i < partvals; k++, i++)
                    {
                        int offset = i * samples_per_partition + info.begin;
                        
                        for (j = 0; j < ch; j++) 
                        {
                            if (s == 0) {
                                resvals[partword[j][i]] += samples_per_partition;
                            }
                            
                            if ((info.secondstages[partword[j][i]] & (1 << s)) != 0) 
                            {
                                codebook statebook = look.partbooks[partword[j][i]][s];
                                
                                if (statebook != null)
                                {
                                    int ret;
                                    int[] accumulator = null;
                                    
                                    ret = encode(ref opb, _in[j] + offset, samples_per_partition, ref statebook, ref accumulator);
                                    
                                    look.postbits += ret;
                                    resbits[partword[j][i]] += ret;
                                }
                            }   
                        }
                    }
                }
            }
            
            return 0;
        }
        
        /* a truncated packet here just means 'stop working'; it's not an error */
        static int _01inverse(ref vorbis_block vb, vorbis_look_residue vl, float** _in, int ch, vorbis_func_decode decodepart)
        {
            int i, j, k, l, s;
            
            vorbis_look_residue0 look = vl as vorbis_look_residue0;
            vorbis_info_residue0 info = look.info;
            
            /* move all this setup out later */
            int samples_per_partition = info.grouping;
            int partitions_per_word = look.phrasebook.dim;
            int max = vb.pcmend >> 1;
            int end = (info.end < max) ? info.end : max;
            int n = end - info.begin;
            
            if (n > 0)
            {
                int partvals = n / samples_per_partition;
                int partwords = (partvals + partitions_per_word - 1) / partitions_per_word;
                int ***partword = stackalloc int **[ch];

                for (j = 0; j < ch; j++)
                {
                    partword[j] = (int**)_vorbis_block_alloc(ref vb, partwords * sizeof(int *));
                }
                
                for (s = 0; s < look.stages; s++) 
                {
                    /* each loop decodes on partition codeword containing partitions_per_word partitions */
                    for (i = 0, l = 0; i < partvals; l++) 
                    {
                        if (s == 0)
                        {
                            /* fetch the partition word for each channel */
                            for (j = 0; j < ch; j++)
                            {
                                int temp = vorbis_book_decode(ref look.phrasebook, ref vb.opb);

                                if (temp == -1 || temp >= info.partvals)
                                {
                                    goto eopbreak;
                                }

                                partword[j][l] = look.decodemap[temp];

                                if (partword[j][l] == null)
                                {
                                    goto errout;
                                }
                            }
                        }
                            
                        /* now we decode residual values for the partitions */
                        for (k = 0; k < partitions_per_word && i < partvals; k++, i++) 
                        {
                            for (j = 0; j < ch; j++) 
                            {
                                int offset = info.begin + i * samples_per_partition;
                                    
                                if ((info.secondstages[partword[j][l][k]] & (1 << s)) != 0) 
                                {
                                    codebook stagebook = look.partbooks[partword[j][l][k]][s];
                                    
                                    if (stagebook != null)
                                    {
                                        if (decodepart(ref stagebook, _in[j] + offset, ref vb.opb, samples_per_partition) == -1) 
                                        {
                                            goto eopbreak;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
        errout:
        eopbreak:
            return 0;
        }
        
        static int res0_inverse(ref vorbis_block vb, vorbis_look_residue vl, float** _in, int* nonzero, int ch)
        {
            int i, used = 0;

            for (i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                {
                    _in[used++] = _in[i];
                }
            }

            if (used != 0)
            {
                return _01inverse(ref vb, vl, _in, used, vorbis_book_decodev_add);
            }
            else
            {
                return 0;
            }
        }

        static int res1_forward(ref Ogg.oggpack_buffer opb, ref vorbis_block vb, vorbis_look_residue vlr, int** _in, int* nonzero, int ch, int** partword, int submap)
        {
            int i, used = 0;

            for (i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                {
                    _in[used++] = _in[i];
                }
            }

            if (used != 0)
            {
                return _01forward(ref opb, ref vb, vlr, _in, used, partword, _encodepart, submap);
            }
            else
            {
                return 0;
            }
        }

        static int** res1_class(ref vorbis_block vb, vorbis_look_residue vlr, int** _in, int* nonzero, int ch)
        {
            int i, used = 0;

            for (i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                {
                    _in[used++] = _in[i];
                }
            }

            if (used != 0)
            {
                return _01class(ref vb, vlr, _in, ch);
            }
            else
            {
                return null;
            }
        }

        static int res1_inverse(ref vorbis_block vb, vorbis_look_residue vlr, float** _in, int* nonzero, int ch)
        {
            int i, used = 0;

            for (i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                {
                    _in[used++] = _in[i];
                }
            }

            if (used != 0)
            {
                return _01inverse(ref vb, vlr, _in, ch, vorbis_book_decodev_add);
            }
            else
            {
                return 0;
            } 
        }   
        
        static int** res2_class(ref vorbis_block vb, vorbis_look_residue vlr, int** a, int* nonzero, int ch)
        {
            int i, used = 0;

            for (i = 0; i < ch; i++)
            {
                if (nonzero[i] != 0)
                {
                    used++;
                }
            }

            if (used != 0)
            {
                return _2class(ref vb, vlr, a, ch);
            }
            else
            {
                return null;
            }
        }
        
        /* res2 is slightly more different; all the channels are interleaved into a single vector and encoded. */

        static int res2_forward(ref Ogg.oggpack_buffer opb, ref vorbis_block vb, vorbis_look_residue vlr, int** _in, int* nonzero, int ch, int** partword, int submap)
        {
            int i, j, k, n = vb.pcmend / 2, used = 0;

            /* don't duplicate the code; use a working vector hack for now and reshape ourselves into a single channel res1 */
            /* ugly; reallocs for each coupling pass :-( */
            int* work = (int *)_vorbis_block_alloc(ref vb, ch * n * sizeof(int));
            
            for (i = 0; i < ch; i++)
            {
                int* pcm = _in[i];
    
                if (nonzero[i] != 0) {
                    used++;
                }
            
                for (j = 0, k = i; j < n; j++, k += ch) {
                    work[k] = pcm[j];
                }
            }

            if (used != 0)
            {
                return _01forward(ref opb, ref vb, vlr, &work, 1, partword, _encodepart, submap);
            }
            else
            {
                return 0;
            }
        }
        
        /* duplicate code here as speed is somewhat more important */
        static int res2_inverse(ref vorbis_block vb, vorbis_look_residue vlr, float** _in, int* nonzero, int ch)
        {
            int i, k, l, s;
            
            vorbis_look_residue0 look = vlr as vorbis_look_residue0;
            vorbis_info_residue0 info = look.info;

            /* move all this setup out later */
            int samples_per_partition = info.grouping;
            int partitions_per_word = look.phrasebook.dim;
            int max = (vb.pcmend * ch) >> 1;
            int end = (info.end < max ? info.end : max);
            int n = end - info.begin;
  
            if(n > 0)
            {
                int partvals = n / samples_per_partition;
                int partwords = (partvals + partitions_per_word - 1) / partitions_per_word;
                int **partword = (int **)_vorbis_block_alloc(ref vb, partwords * sizeof(int *));

                for(i = 0; i < ch; i++) 
                {
                    if(nonzero[i] != 0) {
                        break;
                    }
                }
                
                if (i == ch) {
                    return 0; /* no nonzero vectors */
                }

                for (s = 0; s < look.stages; s++)
                {
                    for (i = 0, l = 0; i < partvals; l++)
                    {
                        if (s == 0)
                        {
                            /* fetch the partition word */
                            int temp = vorbis_book_decode(ref look.phrasebook, ref vb.opb);
                        
                            if (temp == -1 || temp >= info.partvals) {
                                goto eopbreak;
                            }
                            
                            partword[l] = look.decodemap[temp];
                            
                            if (partword[l] == null) {
                                goto errout;
                            }
                        }

                        /* now we decode residual values for the partitions */
                        for (k = 0; k < partitions_per_word && i < partvals; k++, i++)
                        {
                            if ((info.secondstages[partword[l][k]] & (1 << s)) != 0)
                            {
                                codebook stagebook = look.partbooks[partword[l][k]][s];
                                
                                if (stagebook != null)
                                {
                                    if (vorbis_book_decodevv_add(ref stagebook, _in, i * samples_per_partition + info.begin, ch, ref vb.opb, samples_per_partition) == -1) {
                                        goto eopbreak;
                                    }
                                }
                            }
                        }
                    }
                }
            }  
            
        errout:
        eopbreak:
            return 0;        
        }
        
        static readonly vorbis_func_residue residue0_exportbundle = new vorbis_func_residue()
        {
            unpack = res0_unpack,
            look = res0_look,
            free_info = res0_free_info,
            free_look = res0_free_look,
            inverse = res0_inverse
        };
        
        static readonly vorbis_func_residue residue1_exportbundle = new vorbis_func_residue()
        {
            pack = res0_pack,
            unpack = res0_unpack,
            look = res0_look,
            free_info = res0_free_info,
            free_look = res0_free_look,
            _class = res1_class,
            forward = res1_forward,
            inverse = res1_inverse
        };
        
        static readonly vorbis_func_residue residue2_exportbundle = new vorbis_func_residue()
        {
            pack = res0_pack,
            unpack = res0_unpack,
            look = res0_look,
            free_info = res0_free_info,
            free_look = res0_free_look,
            _class = res2_class,
            forward = res2_forward,
            inverse = res2_inverse
        };
    }
}
