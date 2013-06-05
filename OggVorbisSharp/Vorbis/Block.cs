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

 function: PCM data vector blocking, windowing and dis/reassembly
 last mod: $Id: block.c 17561 2010-10-23 10:34:24Z xiphmont $

 Handle windowing, overlap-add, etc of the PCM vectors.  This is made
 more amusing by Vorbis' current two allowed block sizes.

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
        public const int WORD_ALIGN = 8;
    }
    
    // Block
    static public unsafe partial class Vorbis
    {
        static public int vorbis_block_init(ref vorbis_dsp_state v, ref vorbis_block vb)
        {
            if (v == null)
            {
                return -1;
            }
            else if (vb == null)
            {
                return -1;
            }
        
            vorbis_block_clear(ref vb);

            vb.opb = new Ogg.oggpack_buffer();
            vb.vd = v;
            
            if (v.analysisp != 0)
            {
                vorbis_block_internal vbi = new vorbis_block_internal();
                
                vb._internal = vbi;
                vbi.ampmax = -9999;
                
                for (int i = 0; i < PACKETBLOBS; i++) 
                {
                    if (i == PACKETBLOBS / 2) 
                    {
                        vbi.packetblob[i] = vb.opb;
                    }
                    else
                    {
                        vbi.packetblob[i] = new Ogg.oggpack_buffer();
                    }
                    
                    Ogg.oggpack_writeinit(ref vbi.packetblob[i]);
                }                    
            }   
            
            return 0;
        }
        
        static void *_vorbis_block_alloc(ref vorbis_block vb, int bytes) 
        {
            bytes = (bytes + (WORD_ALIGN - 1)) & ~(WORD_ALIGN - 1);
            
            if (bytes + vb.localtop > vb.localalloc)
            {
                /* can't just _ogg_realloc... there are outstanding pointers */
                if (vb.localstore != null) 
                {
                    alloc_chain link = new alloc_chain();
                    vb.totaluse += vb.localtop;
                    
                    link.next = vb.reap;
                    link.ptr = vb.localstore;
                    
                    vb.reap = link;
                }
                
                /* highly conservative */
                vb.localalloc = bytes;
                vb.localstore = _ogg_malloc(vb.localalloc);
                vb.localtop = 0;
            }
            
            void *ret = (void *)((byte *)vb.localstore + vb.localtop);
            vb.localtop += bytes;
            return ret;
        }
        
        /* reap the chain, pull the ripcord */
        static public void _vorbis_block_ripcord(ref vorbis_block vb)
        {
            /* reap the chain */
            alloc_chain reap = vb.reap;
            
            while(reap != null) 
            {
                alloc_chain next = reap.next;
                _ogg_free(reap.ptr);
                reap = next;
            }
            
            /* consolidate storage */
            if (vb.totaluse > 0) 
            {
                vb.localstore = _ogg_realloc(vb.localstore, vb.totaluse + vb.localalloc);
                vb.localalloc += vb.totaluse;
                vb.totaluse = 0;
            }
            
            /* pull the ripcord */
            vb.localtop = 0;
            vb.reap = null;
        }
        
        static public int vorbis_block_clear(ref vorbis_block vb)
        {
            if (vb == null)
            {
                return 1;
            }
            
            vorbis_block_internal vbi = vb._internal as vorbis_block_internal;
            
            _vorbis_block_ripcord(ref vb);
            
            if (vb.localstore != null) {
                _ogg_free(vb.localstore);
            }
            
            if (vbi != null)
            {
                for (int i = 0; i < PACKETBLOBS; i++) {
                    Ogg.oggpack_writeclear(ref vbi.packetblob[i]);
                }
            }
            
            vb.pcm = null;
            vb.opb = null;
            
            vb.lW = 0;
            vb.W = 0;
            vb.nW = 0;
            vb.pcmend = 0;
            vb.mode = 0;
            
            vb.eofflag = 0;
            vb.glue_bits = 0;
            vb.sequence = 0;
            vb.vd = null;
            
            vb.localstore = null;
            vb.localtop = 0;
            vb.localalloc = 0;
            vb.totaluse = 0;
            vb.reap = null;
            
            vb.glue_bits = 0;
            vb.time_bits = 0;
            vb.floor_bits = 0;
            vb.res_bits = 0;
            
            vb._internal = null;
            
            return 0;
        }            
        
        /* Analysis side code, but directly related to blocking.  Thus it's here and not in analysis.c (which is for analysis transforms only).
          The init is here because some of it is shared */
        static public int _vds_shared_init(ref vorbis_dsp_state v, ref vorbis_info vi, int encp)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            private_state b; 
            
            int hs;
            
            if (ci == null) 
            {
                return 1;
            }
            
            hs = ci.halfrate_flag;
            
            b = new private_state();
            v.backend_state = b;
            
            v.vi = vi;
            b.modebits = ilog2((uint)ci.modes);
            
            b.transform[0] = new vorbis_look_transform[VI_TRANSFORMB];
            b.transform[1] = new vorbis_look_transform[VI_TRANSFORMB];
            
            /* MDCT is transform 0 */
            
            b.transform[0][0] = new mdct_lookup();
            b.transform[1][0] = new mdct_lookup();
            
            mdct_init(b.transform[0][0] as mdct_lookup, ci.blocksizes[0] >> hs);
            mdct_init(b.transform[1][0] as mdct_lookup, ci.blocksizes[1] >> hs);
            
            /* Vorbis I uses only window type 0 */
            b.window[0] = ilog2((uint)ci.blocksizes[0]) - 6;
            b.window[1] = ilog2((uint)ci.blocksizes[1]) - 6;
            
            if (encp != 0) /* encode/decode differ here */
            {
                /* analysis always need an fft */
                drft_init(ref b.fft_look[0], ci.blocksizes[0]);
                drft_init(ref b.fft_look[1], ci.blocksizes[1]);
                
                /* finish the codebooks */
                if(ci.fullbooks == null) 
                {
                    ci.fullbooks = new codebook[ci.books];
                    
                    for (int i = 0; i < ci.books; i++) {
                        vorbis_book_init_encode(ref ci.fullbooks[i], ci.book_param[i]);
                    }
                }
                
                b.psy = new vorbis_look_psy[ci.psys];
                
                for (int i = 0; i < ci.psys; i++) 
                {
                    _vp_psy_init 
                    (
                        ref b.psy[i],
                        ci.psy_param[i],
                        ci.psy_g_param,
                        ci.blocksizes[ci.psy_param[i].blockflag] / 2,
                        vi.rate
                    );
                }
                
                v.analysisp = 1;
            }
            else 
            {
                /* finish the codebooks */
                if (ci.fullbooks == null)
                {
                    ci.fullbooks = new codebook[ci.books];
                    
                    for (int i = 0; i < ci.fullbooks.Length; i++) 
                    {
                        ci.fullbooks[i] = new codebook();
                    }
                    
                    for (int i = 0; i < ci.books; i++) 
                    {
                        if (ci.book_param[i] == null) {
                            goto abort_books;
                        }
                        
                        if (vorbis_book_init_decode(ref ci.fullbooks[i], ci.book_param[i]) != 0) {
                            goto abort_books;
                        }
                        
                        /* decode codebooks are now standalone after init */
                        vorbis_staticbook_destroy(ref ci.book_param[i]);
                        ci.book_param[i] = null;
                    }
                }
            }
            
            /* initialize the storage vectors. blocksize[1] is small for encode, but the correct size for decode */
            v.pcm_storage = ci.blocksizes[1];
            v.pcm = (float **)_ogg_malloc(vi.channels * sizeof(float *));
            v.pcmret = (float **)_ogg_malloc(vi.channels * sizeof(float *));
            {
                for (int i = 0; i < vi.channels; i++) 
                {
                    v.pcm[i] = (float *)_ogg_calloc(v.pcm_storage, sizeof(float));
                }
            }
            
            /* all 1 (large block) or 0 (small block) */
            /* explicitly set for the sake of clarity */
            v.lW = 0; /* previous window size */
            v.W = 0; /* current window size */

            /* all vector indexed */
            v.centerW = ci.blocksizes[1] / 2;
            v.pcm_current = v.centerW;
            
            /* initialize all the backend lookups */
            b.flr = new vorbis_look_floor[ci.floors];
            b.residue = new vorbis_look_residue[ci.residues];
            
            for (int i = 0; i < ci.floors; i++) {
                b.flr[i] = _floor_P[ci.floor_type[i]].look(ref v, ci.floor_param[i]);
            }
            
            for (int i = 0; i < ci.residues; i++) {
                b.residue[i] = _residue_P[ci.residue_type[i]].look(ref v, ci.residue_param[i]);
            }
            
            return 0;
            
        abort_books:
            for (int i = 0; i < ci.books; i++) 
            {
                if (ci.book_param[i] != null) 
                {
                    vorbis_staticbook_destroy(ref ci.book_param[i]);
                    ci.book_param[i] = null;
                }
            }
                
            vorbis_dsp_clear(ref v);
            return -1;
        }
        
        /* arbitary settings and spec-mandated numbers get filled in here */
        static public int vorbis_analysis_init(ref vorbis_dsp_state v, ref vorbis_info vi)
        {
            if (v == null)
            {
                return 1;
            }
            else if (vi == null)
            {
                return 1;
            }
            else if (_vds_shared_init(ref v, ref vi, 1) != 0) 
            {
                return 1;
            }
            
            private_state b = null;
            
            b = v.backend_state as private_state;
            b.psy_g_look = _vp_global_look(ref vi);
            
            /* Initialize the envelope state storage */
            b.ve = new envelope_lookup();
            _ve_envelope_init(b.ve, ref vi);
            
            vorbis_bitrate_init(ref vi, ref b.bms);
            
            /* compressed audio packets start after the headers with sequence number 3 */
            v.sequence = 3;
            return 0;
        }
        
        static public void vorbis_dsp_clear(ref vorbis_dsp_state v)
        {
            if (v == null)
            {
                return;
            }
            
            int i;
            
            vorbis_info vi = v.vi;
            codec_setup_info ci = (vi != null) ? vi.codec_setup as codec_setup_info : null;
            private_state b = v.backend_state as private_state;
            
            if (b != null) 
            {
                if (b.ve != null) 
                {
                    _ve_envelope_clear(b.ve);
                    b.ve = null;
                }
                
                if (b.transform[0] != null) {
                    mdct_clear(b.transform[0][0] as mdct_lookup);
                }
                
                if (b.transform[1] != null) {
                    mdct_clear(b.transform[1][0] as mdct_lookup);
                }
                
                b.transform = null;
                
                if (b.flr != null) 
                {
                    if (ci != null) 
                    {
                        for (i = 0; i < ci.floors; i++) {
                            _floor_P[ci.floor_type[i]].free_look(ref b.flr[i]);
                        }
                        
                        b.flr = null;
                    }
                }
                
                if (b.residue != null)
                {
                    if (ci != null)
                    {
                        for (i = 0; i < ci.residues; i++) {
                            _residue_P[ci.residue_type[i]].free_look(ref b.residue[i]);
                        }
                        
                        b.residue = null;
                    }
                }
                
                if (b.psy != null)
                {
                    if (ci != null)
                    {
                        for (i = 0; i < ci.psys; i++) {
                            _vp_psy_clear(ref b.psy[i]);
                        }
                        
                        b.psy = null;
                    }
                }
                
                if (b.psy_g_look != null) {
                    _vp_global_free(ref b.psy_g_look);
                }
                
                vorbis_bitrate_clear(ref b.bms);
                
                drft_clear(ref b.fft_look[0]);
                drft_clear(ref b.fft_look[1]);

                _ogg_free(b.header);
                _ogg_free(b.header1);
                _ogg_free(b.header2);
                
                b.header = null;
                b.header1 = null;
                b.header2 = null;
            }

            v.pcm = null;
            v.pcmret = null;
        }
        
        /* do the deltas, envelope shaping, pre-echo and determine the size of the next block on which to continue analysis */
        static public int vorbis_analysis_blockout(ref vorbis_dsp_state v, ref vorbis_block vb)
        {
            vorbis_info vi = v.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            private_state b = v.backend_state as private_state;
            vorbis_look_psy_global g = b.psy_g_look;
            vorbis_block_internal vbi = vb._internal as vorbis_block_internal;
            
            int i;
            int beginW = v.centerW - ci.blocksizes[v.W] / 2, centerNext;
            
            /* check to see if we're started... */
            if (v.preextrapolate == 0) {
                return 0;
            }

            /* check to see if we're done... */
            if (v.eofflag == -1) {
                return 0;
            }

            /* By our invariant, we have lW, W and centerW set.  Search for the next boundary so we can determine nW (the next window size)
             which lets us compute the shape of the current block's window */

            /* we do an envelope search even on a single blocksize; we may still be throwing more bits at impulses, and envelope search handles
             marking impulses too. */
            {
                int bp = _ve_envelope_search(ref v);
    
                if (bp == -1)
                {
                    if(v.eofflag == 0) {
                        return 0; /* not enough data currently to search for a full long block */
                    }
                    
                    v.nW = 0;
                } 
                else
                {
                    if (ci.blocksizes[0] == ci.blocksizes[1]) {
                        v.nW = 0;
                    } 
                    else {
                        v.nW = bp;
                    }
                }
            }
            
            centerNext = v.centerW + ci.blocksizes[v.W] / 4 + ci.blocksizes[v.nW] / 4;
            
            {
                /* center of next block + next block maximum right side. */
                int blockbound = centerNext + ci.blocksizes[v.nW] / 2;
                
                if (v.pcm_current < blockbound) {
                    /* not enough data yet; although this check is less strict that the _ve_envelope_search, the search is not run if we only use one block size */
                    return 0;
                }
            }
            
            /* fill in the block. Note that for a short window, lW and nW are *short* regardless of actual settings in the stream */
            _vorbis_block_ripcord(ref vb);
            
            vb.lW = v.lW;
            vb.W = v.W;
            vb.nW = v.nW;
            
            if (v.W != 0)
            {
                if (v.lW == 0 || v.nW == 0) {
                    vbi.blocktype = BLOCKTYPE_TRANSITION;
                }
                else {
                    vbi.blocktype = BLOCKTYPE_LONG;
                }
            }
            else
            {
                if (_ve_envelope_mark(ref v) != 0) {
                    vbi.blocktype = BLOCKTYPE_IMPULSE;
                }
                else {
                    vbi.blocktype = BLOCKTYPE_PADDING;
                }
            }
            
            vb.vd = v;
            vb.sequence = v.sequence++;
            vb.granulepos = v.granulepos;
            vb.pcmend = ci.blocksizes[v.W];
            
            /* copy the vectors; this uses the local storage in vb */
            
            /* this tracks 'strongest peak' for later psychoacoustics */
            /* moves to the global psy state; clean this mess up */
            if (vbi.ampmax > g.ampmax) {
                g.ampmax = vbi.ampmax;
            }
            
            g.ampmax = _vp_ampmax_decay(g.ampmax, ref v);
            vbi.ampmax = g.ampmax;
            
            vb.pcm = (float **)_vorbis_block_alloc(ref vb, sizeof(float *) * vi.channels);
            vbi.pcmdelay = (float **)_vorbis_block_alloc(ref vb, sizeof(float *) * vi.channels);
            
            for (i = 0; i < vi.channels; i++) {
                vbi.pcmdelay[i] = (float *)_vorbis_block_alloc(ref vb, sizeof(float) * (vb.pcmend + beginW));
                CopyMemory(vbi.pcmdelay[i], v.pcm[i], sizeof(float) * (vb.pcmend + beginW));
                vb.pcm[i] = vbi.pcmdelay[i] + beginW;
            }
            
            /* handle eof detection: eof==0 means that we've not yet received EOF
                                    eof>0  marks the last 'real' sample in pcm[]
                                    eof<0  'no more to do'; doesn't get here */
            if (v.eofflag != 0) 
            {
                if (v.centerW >= v.eofflag) 
                {
                    v.eofflag = -1;
                    vb.eofflag = 1;
                    return 1;
                }
            }
            
            /* advance storage vectors and clean up */
            {
                int new_centerNext = ci.blocksizes[1] / 2;
                int movementW = centerNext - new_centerNext;
                
                if (movementW > 0) 
                {
                    _ve_envelope_shift(b.ve, movementW);
                    v.pcm_current -= movementW;
                    
                    for (i = 0; i < vi.channels; i++) {
                        CopyMemory(v.pcm[i], v.pcm[i] + movementW, v.pcm_current);
                    }
                    
                    v.lW = v.W;
                    v.W = v.nW;
                    v.centerW = new_centerNext;
                    
                    if (v.eofflag != 0) 
                    {
                        v.eofflag -= movementW;
                        
                        if (v.eofflag <= 0) {
                            v.eofflag = -1;
                        }
                        
                        /* do not add padding to end of stream! */
                        if (v.centerW >= v.eofflag) {
                            v.granulepos += movementW - (v.centerW - v.eofflag);
                        }
                        else {
                            v.granulepos += movementW;
                        }
                    }
                    else {
                        v.granulepos += movementW;
                    }
                }
            }
            
            /* done */
            return 1;
        }
        
        static public int vorbis_synthesis_restart(ref vorbis_dsp_state v)
        {
            vorbis_info vi = v.vi;
            codec_setup_info ci;
            int hs;

            if (v.backend_state == null) {
                return -1;
            }
            
            if (vi == null) {
                return -1;
            }
            
            ci = vi.codec_setup as codec_setup_info;
            
            if (ci == null) {
                return -1;
            }
            
            hs = ci.halfrate_flag;

            v.centerW = ci.blocksizes[1] >> (hs + 1);
            v.pcm_current = v.centerW >> hs;

            v.pcm_returned = -1;
            v.granulepos = -1;
            v.sequence = -1;
            v.eofflag = 0;
            (v.backend_state as private_state).sample_count = -1;

            return 0;
        }
        
        static public int vorbis_synthesis_init(ref vorbis_dsp_state v, ref vorbis_info vi)
        {
            if (v == null)
            {
                return 1;
            }
            else if (vi == null)
            {
                return 1;
            }
            else if (_vds_shared_init(ref v, ref vi, 0) != 0)
            {
                vorbis_dsp_clear(ref v);
                return 1;
            }
            
            vorbis_synthesis_restart(ref v);
            return 0;
        }
        
        /* Unlike in analysis, the window is only partially applied for each block.  The time domain envelope is not yet handled at the point of calling (as it relies on the previous block). */

        static public int vorbis_synthesis_blockin(ref vorbis_dsp_state v, ref vorbis_block vb)
        {
            vorbis_info vi = v.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            private_state b = v.backend_state as private_state;
            
            int hs = ci.halfrate_flag;
            int i, j;

            if (v.pcm_current > v.pcm_returned && v.pcm_returned != -1) {
                return OV_EINVAL;
            }
            
            v.lW = v.W;
            v.W = vb.W;
            v.nW = -1;
            
            if (v.sequence == -1 || v.sequence + 1 != vb.sequence) {
                v.granulepos = -1; /* out of sequence; lose count */
                b.sample_count = -1;
            }
            
            v.sequence = vb.sequence;

            if (vb.pcm != null)  
            {
                int n = ci.blocksizes[v.W] >> (hs + 1);
                int n0 = ci.blocksizes[0] >> (hs + 1);
                int n1 = ci.blocksizes[1] >> (hs + 1);

                int thisCenter;
                int prevCenter;

                v.glue_bits += vb.glue_bits;
                v.time_bits += vb.time_bits;
                v.floor_bits += vb.floor_bits;
                v.res_bits += vb.res_bits;

                if (v.centerW != 0)
                {
                    thisCenter = n1;
                    prevCenter = 0;
                }
                else 
                {
                    thisCenter = 0;
                    prevCenter = n1;
                }
                
                /* v->pcm is now used like a two-stage double buffer.  We don't want to have to constantly shift *or* adjust memory usage.  Don't accept a new block until the old is shifted out */
                
                for (j = 0; j < vi.channels; j++) 
                {
                    /* the overlap/add section */
                    if (v.lW != 0) 
                    {
                        if (v.W != 0) 
                        {
                            /* large/large */
                            float[] w = _vorbis_window_get(b.window[1] - hs);
                            float* pcm = v.pcm[j] + prevCenter;
                            float* p = vb.pcm[j];
                            
                            for (i = 0; i < n1; i++) {
                                pcm[i] = pcm[i] * w[n1 - i - 1] + p[i] * w[i];
                            }
                        }
                        else
                        {
                            /* large/small */
                            float[] w = _vorbis_window_get(b.window[0] - hs);
                            float* pcm = v.pcm[j] + prevCenter + n1 / 2 - n0 / 2;
                            float* p = vb.pcm[j];
                            
                            for (i = 0; i < n0; i++) {
                                pcm[i] = pcm[i] * w[n0 - i - 1] + p[i] * w[i];
                            }
                        }
                    }
                    else
                    {
                        if (v.W != 0)
                        {
                            /* small/large */
                            float[] w = _vorbis_window_get(b.window[0] - hs);
                            float* pcm = v.pcm[j] + prevCenter;
                            float* p = vb.pcm[j] + n1 / 2 + n0 / 2; 
                            
                            for (i = 0; i < n0; i++) {
                                pcm[i] = pcm[i] * w[n0 - i - 1] + p[i] * w[i];
                            }
                            
                            for (; i < n1 / 2 + n0 / 2; i++) {
                                pcm[i] = p[i];
                            }
                        }
                        else
                        {
                            /* small/small */
                            float[] w = _vorbis_window_get(b.window[0] - hs);
                            float* pcm = v.pcm[j] + prevCenter;
                            float* p = vb.pcm[j];
                            
                            for (i = 0; i < n0; i++) {
                                pcm[i] = pcm[i] * w[n0 - i - 1] + p[i] * w[i];
                            }
                        }
                    }   
                    
                    /* the copy section */
                    {
                        float* pcm = v.pcm[j] + thisCenter;
                        float* p = vb.pcm[j] + n;
                        
                        for (i = 0; i < n; i++) {
                            pcm[i] = p[i];
                        }
                    }
                    
                    if (v.centerW != 0) {
                        v.centerW = 0;
                    }
                    else {
                        v.centerW = n1;
                    }
                    
                    /* deal with initial packet state; we do this using the explicit pcm_returned == -1 flag 
                     otherwise we're sensitive to first block being short or long */

                    if (v.pcm_returned == -1) {
                        v.pcm_returned = thisCenter;
                        v.pcm_current = thisCenter;
                    } 
                    else {
                        v.pcm_returned = prevCenter;
                        v.pcm_current = prevCenter + ((ci.blocksizes[v.lW] / 4 + ci.blocksizes[v.W] / 4) >> hs);
                    }
                }
                
                /* track the frame number... This is for convenience, but also making sure our last packet doesn't end with added padding.  If
                 the last packet is partial, the number of samples we'll have to return will be past the vb->granulepos.

                 This is not foolproof!  It will be confused if we begin decoding at the last page after a seek or hole.  In that case,
                 we don't have a starting point to judge where the last frame is.  For this reason, vorbisfile will always try to make sure
                 it reads the last two marked pages in proper sequence */

                if (b.sample_count == -1) {      
                    b.sample_count = 0;
                } 
                else {
                    b.sample_count += ci.blocksizes[v.lW] / 4 + ci.blocksizes[v.W] / 4;
                }
                
                if (v.granulepos == -1)
                {
                    if (vb.granulepos != -1) 
                    {
                        v.granulepos = vb.granulepos;
                        
                        /* is this a short page? */
                        if (b.sample_count > v.granulepos) {
                            /* corner case; if this is both the first and last audio page,then spec says the end is cut, not beginning */
                            long extra = b.sample_count - vb.granulepos;
                            
                            /* we use ogg_int64_t for granule positions because a uint64 isn't universally available.  Unfortunately,
                            that means granposes can be 'negative' and result in extra being negative */
                            if (extra < 0) {
                                extra = 0;
                            }
                            
                            if (vb.eofflag != 0) {
                                /* trim the end */
                                /* no preceding granulepos; assume we started at zero (we'd
                               have to in a short single-page stream) */
                                /* granulepos could be -1 due to a seek, but that would result
                               in a long count, not short count */

                                /* Guard against corrupt/malicious frames that set EOP and
                               a backdated granpos; don't rewind more samples than we
                               actually have */
                                if(extra > (v.pcm_current - v.pcm_returned) << hs) {
                                    extra = (v.pcm_current - v.pcm_returned) << hs;
                                }
                                
                                v.pcm_current -= (int)(extra >> hs);
                            }
                            else
                            {
                                /* trim the beginning */
                                v.pcm_returned += (int)(extra >> hs);
                                
                                if (v.pcm_returned > v.pcm_current) {
                                    v.pcm_returned = v.pcm_current;
                                }
                            }
                        }
                    }
                    else 
                    {
                        v.granulepos += ci.blocksizes[v.lW] / 4 + ci.blocksizes[v.W] / 4;
                        
                        if (v.granulepos != -1 && v.granulepos != vb.granulepos)
                        {
                            if (v.granulepos > vb.granulepos) 
                            {
                                long extra = v.granulepos - vb.granulepos;
                                
                                if (extra > 0)
                                {
                                    if (v.eofflag != 0) 
                                    {
                                        /* partial last frame.  Strip the extra samples off */

                                        /* Guard against corrupt/malicious frames that set EOP and a backdated granpos; don't rewind more samples than we
                                       actually have */
                                    
                                        if (extra > (v.pcm_current - v.pcm_returned) << hs) {
                                            extra = (v.pcm_current - v.pcm_returned) << hs;
                                        }
                                        
                                        /* we use ogg_int64_t for granule positions because a uint64 isn't universally available.  Unfortunately,
                                      that means granposes can be 'negative' and result in extra being negative */
                                        if (extra < 0) {
                                            extra = 0;
                                        }
                                        
                                        v.pcm_current -= (int)(extra >> hs);
                                    } /* else {Shouldn't happen *unless* the bitstream is out of spec.  Either way, believe the bitstream } */
                                } /* else {Shouldn't happen *unless* the bitstream is out of spec.  Either way, believe the bitstream } */
                            }                                
                        }
                        
                        v.granulepos = vb.granulepos;
                    }
                }
            }
            
            /* Update, Cleanup */
            if (vb.eofflag != 0) {
                v.eofflag = 1;
            }
            
            return 0;            
        }
        
        /* pcm==NULL indicates we just want the pending samples, no more */
        static public int vorbis_synthesis_pcmout(ref vorbis_dsp_state v, ref float **pcm)
        {
            vorbis_info vi = v.vi;
            
            if (v.pcm_returned > -1 && v.pcm_returned < v.pcm_current)
            {
                for (int i = 0; i < vi.channels; i++) {
                    v.pcmret[i] = v.pcm[i] + v.pcm_returned;
                }
                
                pcm = v.pcmret;
                return v.pcm_current - v.pcm_returned;
            }
            
            return 0;
        }
        
        static public int vorbis_synthesis_pcmout(ref vorbis_dsp_state v)
        {
            float** pcm_null = null;
            return vorbis_synthesis_pcmout(ref v, ref pcm_null);
        }
        
        static public int vorbis_synthesis_read(ref vorbis_dsp_state v, int n)
        {
            if (n != 0 && v.pcm_returned + n > v.pcm_current) {
                return OV_EINVAL;
            }
            else {
                v.pcm_returned += n;
                return 0;
            }
        }
        
        /* intended for use with a specific vorbisfile feature; we want access
          to the [usually synthetic/postextrapolated] buffer and lapping at
          the end of a decode cycle, specifically, a half-short-block worth.
          This funtion works like pcmout above, except it will also expose
          this implicit buffer data not normally decoded. */
          
        static public int vorbis_synthesis_lapout(ref vorbis_dsp_state v, ref float** pcm)
        {
            vorbis_info vi = v.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            
            int hs = ci.halfrate_flag;
            int n = ci.blocksizes[v.W] >> (hs + 1);
            int n0 = ci.blocksizes[0] >> (hs + 1);
            int n1 = ci.blocksizes[1] >> (hs + 1);
            int i, j;
            
            if (v.pcm_returned < 0) {
                return 0;
            }
            
            /* our returned data ends at pcm_returned; because the synthesis pcm buffer is a two-fragment ring, that means our data block may be
             fragmented by buffering, wrapping or a short block not filling out a buffer.  To simplify things, we unfragment if it's at all
             possibly needed. Otherwise, we'd need to call lapout more than once as well as hold additional dsp state.  Opt for simplicity. */
            
            if (v.centerW == n1) 
            {
                /* the data buffer wraps; swap the halves */
                /* slow, sure, small */
                for (j = 0; j < vi.channels; j++) 
                {
                    float* p = v.pcm[j];
                    
                    for (i = 0; i < n1; i++) 
                    {
                        float temp = p[i];
                        p[i] = p[i + n1];
                        p[i + n1] = temp;
                    }
                    
                    v.pcm_current -= n1;
                    v.pcm_returned -= n1;
                    v.centerW = 0;
                }
            }
            
            /* solidly buffer into contiguous space */
            if ((v.lW & v.W) == 1) 
            {
                /* long/short or short/long */
                for (j = 0; j < vi.channels; j++) 
                {
                    int s_ptr = 0; // instead of float *s = v->pcm[j];
                    int d_ptr = (n1 - n0) / 2; // instead of float *d = v->pcm[j] + (n1 - n0) /2;
                    
                    for (i = (n1 + n0) / 2 -1; i >= 0; --i) {
                        pcm[j][d_ptr + i] = pcm[j][s_ptr + i]; // instead of d[i] = s[i]
                    }
                }
                
                v.pcm_returned += (n1 - n0) / 2;
                v.pcm_current += (n1 - n0) / 2;
            }
            else
            {
                if (v.lW == 0)
                {
                    /* short/short */
                    for (j = 0; j < vi.channels; j++)
                    {
                        int s_ptr = 0; // instead of float *s = v->pcm[j];
                        int d_ptr = n1 - n0; // instead of float *d = v->pcm[j];

                        for (i = n0 - 1; i >= 0; --i)
                        {
                            pcm[j][d_ptr + i] = pcm[j][s_ptr + i]; // instead of d[i] = s[i]
                        }
                    }
                    
                    v.pcm_returned += (n1 - n0);
                    v.pcm_current += (n1 - n0);
                }
            }
            
            if (pcm != null) 
            {
                for (i = 0; i < vi.channels; i++) {
                    v.pcmret[i] = v.pcm[i] + v.pcm_returned;
                }
                
                pcm = v.pcmret;
            }
            
            return (n1 + n - v.pcm_returned);
        }
        
        static public float[] vorbis_window(ref vorbis_dsp_state v, int W)
        {
            vorbis_info vi = v.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            
            int hs = ci.halfrate_flag;
            private_state b = v.backend_state as private_state;
            
            if (b.window[W] - 1 < 0) {
                return null;
            }
            else {
                return _vorbis_window_get(b.window[W] - hs);
            }
        }
    }
}
