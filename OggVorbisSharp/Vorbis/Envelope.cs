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

 function: PCM data envelope analysis
 last mod: $Id: envelope.c 16227 2009-07-08 06:58:46Z xiphmont $

 ********************************************************************/
  
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 
 
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        public const int VE_PRE = 16;
        public const int VE_WIN = 4;
        public const int VE_POST = 2;
        public const int VE_AMP = (VE_PRE + VE_POST - 1);
        
        public const int VE_BANDS = 7;
        public const int VE_NEARDC = 15;
        
        public const int VE_MINSTRETCH = 2; /* a bit less than short block */
        public const int VE_MAXSTRETCH = 12 ; /* one-third full block */
    }
    
    // Types
    static public unsafe partial class Vorbis
    {
        class envelope_filter_state
        {
            public float[] ampbuf = new float[VE_AMP];
            public int ampptr;
            
            public float[] nearDC = new float[VE_NEARDC];
            public float nearDC_acc;
            public float nearDC_partialacc;
            public int nearptr;
        }

        class envelope_band
        {
            public int begin;
            public int end;
              
            public float *window;
            public float total; 
        }
        
        class envelope_lookup
        {
            public int ch;
            public int winlength;
            public int searchstep;
            public float minenergy;

            public mdct_lookup mdct;
            public float* mdct_win;

            public envelope_band[] band = new envelope_band[VE_BANDS];
            public envelope_filter_state[] filter;
            public int stretch;

            public int* mark;

            public int storage;
            public int current;
            public int curmark;
            public int cursor;
        }
    }
    
    // Envelope
    static public unsafe partial class Vorbis
    {
        static void _ve_envelope_init(envelope_lookup e, ref vorbis_info vi)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            vorbis_info_psy_global gi = ci.psy_g_param;

            int ch = vi.channels;
            int i, j;
            int n = e.winlength = 128;
            
            e.searchstep = 64; /* not random */
            e.minenergy = gi.preecho_minenergy;
            e.ch = ch;
            e.storage = 128;
            e.cursor = ci.blocksizes[1] / 2;
            e.mdct_win = (float *)_ogg_calloc(n, sizeof(float));
            
            mdct_init(e.mdct, n);
            
            for (i = 0; i < n; i++) 
            {
                e.mdct_win[i] = (float)Math.Sin(i / (n - 1.0f) * Math.PI);
                e.mdct_win[i] *= e.mdct_win[i];
            }
            
            /* magic follows */
            e.band[0].begin = 2; e.band[0].end = 4;
            e.band[1].begin = 4; e.band[1].end = 5;
            e.band[2].begin = 6; e.band[2].end = 6;
            e.band[3].begin = 9; e.band[3].end = 8;
            e.band[4].begin = 13; e.band[4].end = 8;
            e.band[5].begin = 17; e.band[5].end = 8;
            e.band[6].begin = 22; e.band[6].end = 8; 
            
            for (j = 0; j < VE_BANDS; j++)
            {
                n = e.band[j].end;
                e.band[j].window = (float *)_ogg_malloc(n * sizeof(float));
                
                for (i = 0; i < n; i++) 
                {
                    e.band[j].window[i] = (float)Math.Sin((i + 0.5f) / n * Math.PI);
                    e.band[j].total += e.band[j].window[i];
                }
                
                e.band[j].total = 1.0f / e.band[j].total;
            }
            
            e.filter = new envelope_filter_state[VE_BANDS];
            e.mark = (int *)_ogg_calloc(e.storage, sizeof(int));
        }
        
        static void _ve_envelope_clear(envelope_lookup e)
        {
            mdct_clear(e.mdct);

            for (int i = 0; i < VE_BANDS; i++) {
                _ogg_free(e.band[i].window);
            }
            
            e.ch = 0;
            e.winlength = 0;
            e.searchstep = 0;
            e.minenergy = 0.0f;
            
            e.mdct = null;
            e.mdct_win = null;
            
            e.band = null;
            e.filter = null;
            e.storage = 0;
            
            e.mark = null;
            
            e.storage = 0;
            e.current = 0;
            e.curmark = 0;
            e.cursor = 0;
        }

        /* fairly straight threshhold-by-band based until we find something
          that works better and isn't patented. */

        static int _ve_amp(envelope_lookup ve, vorbis_info_psy_global gi, float* data, ref envelope_band[] bands, envelope_filter_state[] filters, int offset)
        {
            int n = ve.winlength;
            int ret = 0;
            int i, j;
            float decay;

            /* we want to have a 'minimum bar' for energy, else we're just
               basing blocks on quantization noise that outweighs the signal
               itself (for low power signals) */

            float minV = ve.minenergy;
            float* vec = (float*)_ogg_malloc(n * sizeof(float));

            /* stretch is used to gradually lengthen the number of windows
               considered prevoius-to-potential-trigger */
               
            int stretch = (int)Math.Max(VE_MINSTRETCH, ve.stretch / 2);
            float penalty = gi.stretch_penalty - (ve.stretch / 2.0f - VE_MINSTRETCH);

            if (penalty < 0.0f) {
                penalty = 0.0f;
            }

            if (penalty > gi.stretch_penalty) {
                penalty = gi.stretch_penalty;
            }

            /*_analysis_output_always("lpcm",seq2,data,n,0,0,
              totalshift+pos*ve->searchstep);*/

            /* window and transform */
            for (i = 0; i < n; i++) {
                vec[i] = data[i] * ve.mdct_win[i];
            }

            mdct_forward(ve.mdct, vec, vec);

            /*_analysis_output_always("mdct",seq2,vec,n/2,0,1,0); */

            /* near-DC spreading function; this has nothing to do with
               psychoacoustics, just sidelobe leakage and window size */
            {
                float temp = (float)(vec[0] * vec[0] + 0.7f * vec[1] * vec[1] + 0.2f * vec[2] * vec[2]);
                int ptr = filters[offset].nearptr;

                /* the accumulation is regularly refreshed from scratch to avoid
                   floating point creep */
                   
                if (ptr == 0)
                {
                    decay = filters[offset].nearDC_acc = filters[offset].nearDC_partialacc + temp;
                    filters[offset].nearDC_partialacc = temp;
                }
                else
                {
                    decay = filters[offset].nearDC_acc += temp;
                    filters[offset].nearDC_partialacc += temp;
                }
                
                filters[offset].nearDC_acc -= filters[0].nearDC[ptr];
                filters[offset].nearDC[ptr] = temp;

                decay *= (1.0f / (VE_NEARDC + 1));
                filters[offset].nearptr++;

                if (filters[offset].nearptr >= VE_NEARDC)
                {
                    filters[offset].nearptr = 0;
                }

                decay = (float)todB(decay) * 0.5f - 15.0f;
            }

            /* perform spreading and limiting, also smooth the spectrum. yes, the MDCT results in all real coefficients, but it still *behaves*
             like real/imaginary pairs */
              
            for (i = 0; i < n / 2; i += 2)
            {
                float val = vec[i] * vec[i] + vec[i + 1] * vec[i + 1];
                val = (float)todB(val) * 0.5f;

                if (val < decay) {
                    val = decay;
                }

                if (val < minV) {
                    val = minV;
                }

                vec[i >> 1] = val;
                decay -= 8.0f;
            }

            /*_analysis_output_always("spread",seq2++,vec,n/4,0,0,0);*/

            /* perform preecho/postecho triggering by band */
            for (j = 0; j < VE_BANDS; j++)
            {
                float acc = 0.0f;
                float valmax, valmin;

                /* accumulate amplitude */
                for (i = 0; i < bands[j].end; i++) {
                    acc += vec[i + bands[j].begin] * bands[j].window[i];
                }

                acc *= bands[j].total;

                /* convert amplitude to delta */
                {
                    int p, _this = filters[offset + j].ampptr;
                    float postmax, postmin, premax = -99999.0f, premin = 99999.0f;

                    p = _this;
                    p--;
                    
                    if (p < 0) {
                        p += VE_AMP;
                    }
                    
                    postmax = Math.Max(acc, filters[offset + j].ampbuf[p]);
                    postmin = Math.Min(acc, filters[offset + j].ampbuf[p]);

                    for (i = 0; i < stretch; i++)
                    {
                        p--;
                        
                        if (p < 0) {
                            p += VE_AMP;
                        }
                            
                        premax = Math.Max(premax, filters[offset + j].ampbuf[p]);
                        premin = Math.Min(premin, filters[offset + j].ampbuf[p]);
                    }

                    valmin = postmin - premin;
                    valmax = postmax - premax;

                    /* filters[j].markers[pos] = valmax; */
                    
                    filters[offset + j].ampbuf[_this] = acc;
                    filters[offset + j].ampptr++;
                    
                    if (filters[offset + j].ampptr >= VE_AMP) {
                        filters[offset + j].ampptr = 0;
                    }
                }

                /* look at min/max, decide trigger */
                if (valmax > gi.preecho_thresh[j] + penalty)
                {
                    ret |= 1;
                    ret |= 4;
                }

                if (valmin < gi.postecho_thresh[j] - penalty)
                {
                    ret |= 2;
                }
            }

            return ret;
        }
        
        static int _ve_envelope_search(ref vorbis_dsp_state v)
        {
            vorbis_info vi = v.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            vorbis_info_psy_global gi = ci.psy_g_param;
            envelope_lookup ve = (v.backend_state as private_state).ve;

            int i, j;
            int first = ve.current / ve.searchstep;
            int last = v.pcm_current / ve.searchstep - VE_WIN;

            if (first < 0) {
                first = 0;
            }

            /* make sure we have enough storage to match the PCM */
            if (last + VE_WIN + VE_POST > ve.storage)
            {
                ve.storage = last + VE_WIN + VE_POST; /* be sure */
                ve.mark = (int *)_ogg_realloc(ve.mark, ve.storage * sizeof(int));
            }

            for (j = first; j < last; j++)
            {
                int ret = 0;
                
                ve.stretch++;
                
                if (ve.stretch > VE_MAXSTRETCH * 2) {
                    ve.stretch = VE_MAXSTRETCH * 2;
                }

                for (i = 0; i < ve.ch; i++)
                {
                    float* pcm = v.pcm[i] + ve.searchstep * j;
                    ret |= _ve_amp(ve, gi, pcm, ref ve.band, ve.filter, i * VE_BANDS);
                }

                ve.mark[j + VE_POST] = 0;
                
                if ((ret & 1) != 0)
                {
                    ve.mark[j] = 1;
                    ve.mark[j + 1] = 1;
                }

                if ((ret & 2) != 0)
                {
                    ve.mark[j] = 1;
                    
                    if (j > 0) {
                        ve.mark[j - 1] = 1;
                    }
                }

                if ((ret & 4) != 0) 
                {
                    ve.stretch = -1;
                }
            }

            ve.current = last * ve.searchstep;

            {
                int centerW = v.centerW;
                int testW = centerW + ci.blocksizes[v.W] / 4 + ci.blocksizes[1] / 2 + ci.blocksizes[0] / 4;

                j = ve.cursor;

                while (j < ve.current - (ve.searchstep))
                {
                    /* account for postecho working back one window */
                    if (j >= testW) {
                        return 1;
                    }

                    ve.cursor = j;

                    if (ve.mark[j / ve.searchstep] != 0)
                    {
                        if (j > centerW)
                        {
                            ve.curmark = j;
                            
                            if (j >= testW) {
                                return 1;
                            }
                            else {
                                return 0;
                            }
                        }
                    }
                    
                    j += ve.searchstep;
                }
            }

            return -1;
        }

        static int _ve_envelope_mark(ref vorbis_dsp_state v)
        {
            envelope_lookup ve = (v.backend_state as private_state).ve;
            vorbis_info vi = v.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;

            int centerW = v.centerW;
            int beginW = centerW - ci.blocksizes[v.W] / 4;
            int endW = centerW + ci.blocksizes[v.W] / 4;

            if (v.W != 0)
            {
                beginW -= ci.blocksizes[v.lW] / 4;
                endW += ci.blocksizes[v.nW] / 4;
            }
            else
            {
                beginW -= ci.blocksizes[0] / 4;
                endW += ci.blocksizes[0] / 4;
            }

            if (ve.curmark >= beginW && ve.curmark < endW)
            {
                return 1;
            }
            else
            {
                int first = beginW / ve.searchstep;
                int last = endW / ve.searchstep;
                int i;

                for (i = first; i < last; i++)
                {
                    if (ve.mark[i] != 0) return 1;
                }
            }

            return 0;
        }

        static void _ve_envelope_shift(envelope_lookup e, int shift)
        {
            int smallsize = e.current / e.searchstep + VE_POST; /* adjust for placing marks ahead of ve->current */
            int smallshift = shift / e.searchstep;

            CopyMemory(e.mark, e.mark + smallshift, (smallsize - smallshift) * sizeof(int));

            e.current -= shift;

            if (e.curmark >= 0)
            {
                e.curmark -= shift;
            }
            else
            {
                e.cursor -= shift;
            }
        }
    }
}