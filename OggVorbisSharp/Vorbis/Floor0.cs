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

 function: floor backend 0 implementation
 last mod: $Id: floor0.c 18184 2012-02-03 20:55:12Z xiphmont $

 ********************************************************************/

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace OggVorbisSharp
{
    // Types 
    static public unsafe partial class Vorbis
    {
        class vorbis_look_floor0 : vorbis_look_floor
        {
            public int ln;
            public int m;
            public int[][] linearmap; 
            public int[] n = new int[2];
            
            public vorbis_info_floor0 vi; 
            
            public int bits;
            public int frames;
        }
    }
    
    // Floor0
    static public unsafe partial class Vorbis 
    {
        static void floor0_free_info(ref vorbis_info_floor vif)
        {
            vorbis_info_floor0 info = vif as vorbis_info_floor0;
            
            if (info != null) {
                info = null;
            }
        }
        
        static void floor0_free_lock(ref vorbis_look_floor vif) 
        {
            vorbis_look_floor0 look = vif as vorbis_look_floor0;
            
            if (look != null) {
                look = null;
            }
        }
        
        static vorbis_info_floor floor0_unpack(ref vorbis_info vi, ref Ogg.oggpack_buffer opb)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            vorbis_info_floor _info = new vorbis_info_floor0();
            vorbis_info_floor0 info = _info as vorbis_info_floor0;
            
            info.order = Ogg.oggpack_read(ref opb, 8);
            info.rate = Ogg.oggpack_read(ref opb, 16);
            info.barkmap = Ogg.oggpack_read(ref opb, 16);
            info.ampbits = Ogg.oggpack_read(ref opb, 6);
            info.ampdB = Ogg.oggpack_read(ref opb, 8);
            info.numbooks = Ogg.oggpack_read(ref opb, 4) + 1;
            
            if (info.order < 1) {
                goto err_out;
            }
            
            if (info.rate < 1) {
                goto err_out;
            }
            
            if (info.barkmap < 1) {
                goto err_out;
            }
            
            if (info.numbooks < 1) {
                goto err_out;
            }
            
            for (int j = 0; j < info.numbooks; j++) 
            {
                info.books[j] = Ogg.oggpack_read(ref opb, 8);
                
                if (info.books[j] < 0 || info.books[j] >= ci.books) {
                    goto err_out;
                }
                
                if (ci.book_param[info.books[j]].maptype == 0) {
                    goto err_out;
                }
                
                if (ci.book_param[info.books[j]].dim < 1) {
                    goto err_out;
                }
            }
            
            return info;
        
        err_out:
            floor0_free_info(ref _info);
            return null;
        }
        
        /* initialize Bark scale and normalization lookups.  We could do this with static tables, but Vorbis allows a number of possible
          combinations, so it's best to do it computationally.

          The below is authoritative in terms of defining scale mapping. Note that the scale depends on the sampling rate as well as the
          linear block and mapping sizes */        
        
        static void floor0_map_lazy_init(ref vorbis_block vb, vorbis_info_floor infoX, vorbis_look_floor0 look)
        {
            if (look.linearmap[vb.W] == null)
            {
                vorbis_dsp_state   vd = vb.vd;
                vorbis_info        vi = vd.vi;
                codec_setup_info   ci = vi.codec_setup as codec_setup_info;
                vorbis_info_floor0 info = infoX as vorbis_info_floor0;
                
                int W = vb.W;
                int n = ci.blocksizes[W] / 2, j;
                
                /* we choose a scaling constant so that: 
                 floor(bark(rate / 2 - 1) * C) = mapped - 1
                 floor(bark(rate / 2) * C) = mapped */
                float scale = (float)(look.ln / toBARK(info.rate / 2.0f));
                
                /* the mapping from a linear scale to a smaller bark scale is straightforward.  We do *not* make sure that the linear mapping
                 does not skip bark-scale bins; the decoder simply skips them and the encoder may do what it wishes in filling them.  They're
                 necessary in some mapping combinations to keep the scale spacing accurate */
                look.linearmap[W] = new int[n + 1];
                
                for (j = 0; j < n; j++) 
                {
                    int val = (int)floor(toBARK((info.rate / 2.0f) / n * j) * scale); /* bark numbers represent band edges */
                    
                    if (val >= look.ln) {
                        val = look.ln - 1; /* guard against the approximation */
                    }
                    
                    look.linearmap[W][j] = val;
                }
                
                look.linearmap[W][j] = -1;
                look.n[W] = n;
            }
        }
        
        static vorbis_look_floor floor0_look(ref vorbis_dsp_state vd, vorbis_info_floor vif)
        {
            vorbis_info_floor0 info = vif as vorbis_info_floor0;
            vorbis_look_floor0 look = new vorbis_look_floor0();
            
            look.m = info.order;
            look.ln = info.barkmap;
            look.vi = info;
            look.linearmap = new int[2][];
            
            return look;
        }
        
        static void *floor0_inverse1(ref vorbis_block vb, vorbis_look_floor vlf)
        {
            vorbis_look_floor0 look = vlf as vorbis_look_floor0;
            vorbis_info_floor0 info = look.vi;
            
            int ampraw = Ogg.oggpack_read(ref vb.opb, info.ampbits);
            
            if (ampraw > 0) /* also handles the -1 out of data case */
            {
                uint maxval = (uint)((1 << info.ampbits) - 1);
                float amp = (float)ampraw / maxval * info.ampdB;
                int booknum = Ogg.oggpack_read(ref vb.opb, ilog((uint)info.numbooks));
                
                if (booknum != -1 && booknum < info.numbooks) /* be paranoid */
                {
                    codec_setup_info ci = vb.vd.vi.codec_setup as codec_setup_info;
                    codebook b = ci.fullbooks[info.books[booknum]];
                    
                    float last = 0.0f;
                    
                    /* the additional b->dim is a guard against any possible stack smash; b->dim is provably more than we can overflow the vector */
                    float *lsp = (float *)_vorbis_block_alloc(ref vb, sizeof(float) * (look.m + b.dim + 1));
                
                    if (vorbis_book_decodev_set(ref b, lsp, ref vb.opb, look.m) == -1) 
                    {
                        goto eop;
                    }
                    
                    for (int j = 0; j < look.m;) 
                    {
                        for (int k = 0; k < look.m && k < b.dim; k++, j++)
                        {
                            lsp[j] += last;
                        }
                        
                        last = lsp[j - 1];
                    }
                
                    lsp[look.m] = amp;
                    return lsp;
                }
            }

        eop:
            return null;
        }
        
        static int floor0_inverse2(ref vorbis_block vb, vorbis_look_floor vlf, void *memo, float *_out)
        {
            vorbis_look_floor0 look = vlf as vorbis_look_floor0;
            vorbis_info_floor  info = look.vi;

            floor0_map_lazy_init(ref vb, info, look);
            
            if (memo != null)
            {
                float *lsp = (float *)memo; 
                float amp = lsp[look.m];
                
                /* take the coefficients back to a spectral envelope curve */
                vorbis_lsp_to_curve
                (
                    _out,
                    ref look.linearmap[vb.W],
                    look.n[vb.W],
                    look.ln,
                    lsp,
                    look.m,
                    amp,
                    (info as vorbis_info_floor0).ampdB
                );
                return 1;
            }
            else 
            {
                ZeroMemory((IntPtr)_out, sizeof(float) * look.n[vb.W]);
                return 0;
            }
        }
        
        /* export hooks */
        static readonly vorbis_func_floor floor0_exportbundle = new vorbis_func_floor()
        {
            pack = null,
            unpack = floor0_unpack, 
            look = floor0_look, 
            free_info = floor0_free_info, 
            free_look = floor0_free_lock, 
            inverse1 = floor0_inverse1, 
            inverse2 = floor0_inverse2 
        };        
    }
}
