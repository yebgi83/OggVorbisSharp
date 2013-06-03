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

 function: psychoacoustics not including preecho
 last mod: $Id: psy.c 18077 2011-09-02 02:49:00Z giles $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */  

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        public const int EHMER_MAX = 56;

        public const int P_BANDS = 17; /* 62Hz to 16kHz */
        public const int P_LEVELS = 8; /* 30dB to 100dB */
        public const float P_LEVEL_0 = 30.0f; /* 30 dB */
        public const int P_NOISECURVES = 3;

        public const int NOISE_COMPAND_LEVELS = 40;
    }

    // Types
    static public unsafe partial class Vorbis
    {
        public class vorbis_info_psy
        {
            public int blockflag;

            public float ath_adjatt;
            public float ath_maxatt;

            public float[] tone_masteratt = new float[P_NOISECURVES];
            public float tone_centerboost;
            public float tone_decay;
            public float tone_abs_limit;
            public float[] toneatt = new float[P_BANDS];

            public int noisemaskp;
            public float noisemaxsupp;
            public float noisewindowlo;
            public float noisewindowhi;
            public int noisewindowlomin;
            public int noisewindowhimin;
            public int noisewindowfixed;
            public float[,] noiseoff = new float[P_NOISECURVES, P_BANDS];
            public float[] noisecompand = new float[NOISE_COMPAND_LEVELS];

            public float max_curve_dB;

            public int normal_p;
            public int normal_start;
            public int normal_partition;
            public double normal_thresh;
        }

        public class vorbis_info_psy_global
        {
            public int eighth_octave_lines;

            /* for block long/short tuning; encode only */
            public float[] preecho_thresh = new float[VE_BANDS];
            public float[] postecho_thresh = new float[VE_BANDS];
            public float stretch_penalty;
            public float preecho_minenergy;

            public float ampmax_att_per_sec;

            /* channel coupling config */
            public int[] coupling_pkHz = new int[PACKETBLOBS];
            public int[,] coupling_pointlimit = new int[2, PACKETBLOBS];
            public int[] coupling_prepointamp = new int[PACKETBLOBS];
            public int[] coupling_postpointamp = new int[PACKETBLOBS];
            public int[,] sliding_lowpass = new int[2, PACKETBLOBS];
        }

        public class vorbis_look_psy_global
        {
            public float ampmax;
            public int channels;

            public vorbis_info_psy_global gi;
            public int[,] coupling_pointlimit = new int[2, P_NOISECURVES];
        }

        public class vorbis_look_psy
        {
            public int n;
            public vorbis_info_psy vi;

            public float*** tonecurves;
            public float** noiseoffset;

            public float* ath;
            public int* octave;
            public int* bark;

            public int firstoc;
            public int shiftoc;
            public int eighth_octave_lines; /* power of two, please */
            public int total_octave_lines;
            public int rate; /* cache it */

            public float m_val; /* Masking compensation value */
        }
    }

    // Psy
    static public unsafe partial class Vorbis
    {
        public const float NEGINF = -9999.0f;

        static public readonly double[] stereo_threshholds = 
        {
            0.0, 0.5, 1.0, 1.5, 2.5, 4.5, 8.5, 16.5, 9e10
        };

        static public readonly double[] stereo_threshholds_limited =
        {
            0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 4.5, 8.5, 9e10
        };

        static public vorbis_look_psy_global _vp_global_look(ref vorbis_info vi)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            vorbis_info_psy_global gi = ci.psy_g_param;
            vorbis_look_psy_global look = new vorbis_look_psy_global();

            look.channels = vi.channels;
            look.ampmax = -9999.0f;
            look.gi = gi;

            return look;
        }

        static public void _vp_global_free(ref vorbis_look_psy_global look)
        {
            if (look != null) {
                look = null;
            }
        }

        static public void _vi_gpsy_free(ref vorbis_info_psy_global i)
        {
            if (i != null) {
                i = null;
            }
        }

        static public void _vi_psy_free(ref vorbis_info_psy i)
        {
            if (i != null) {
                i = null;
            }
        }

        static public void min_curve(float* c1, float *c2)
        {
            for (int i = 0; i < EHMER_MAX; i++)
            {
                if (c2[i] < c1[i]) {
                    c1[i] = c2[i];
                }
            }
        }

        static public void max_curve(float* c1, float* c2)
        {
            for (int i = 0; i < EHMER_MAX; i++)
            {
                if (c2[i] > c1[i]) {
                    c1[i] = c2[i];
                }
            }
        }

        static public void attenuate_curve(float* c, float att)
        {
            for (int i = 0; i < EHMER_MAX; i++) {
                c[i] += att;
            }
        }
        
        static public float*** setup_tone_curves(ref float[] curveatt_dB, float binHz, int n, float center_boost, float center_decay_rate)
        {
            int i, j, k, m;
            float[] ath = new float[EHMER_MAX];
            float[, ,] workc = new float[P_BANDS, P_LEVELS, EHMER_MAX];
            float[,] athc = new float[P_LEVELS, EHMER_MAX];
            float* brute_buffer = stackalloc float[n];

            float*** ret = (float ***)_ogg_malloc(P_BANDS * sizeof(float **));

            for (i = 0; i < P_BANDS; i++)
            {
                /* we add back in the ATH to avoid low level curves falling off to
                 -infinity and unnecessarily cutting off high level curves in the
                 curve limiting (last step). */

                /* A half-band's settings must be valid over the whole band, and
                 it's better to mask too little than too much */

                int ath_offset = i * 4;

                for (j = 0; j < EHMER_MAX; j++)
                {
                    float min = 999.0f;

                    for (k = 0; k < 4; k++)
                    {
                        if (j + k + ath_offset < MAX_ATH)
                        {
                            if (min > ATH[j + k + ath_offset]) {
                                min = ATH[j + k + ath_offset];
                            }
                        }
                        else
                        {
                            if (min > ATH[MAX_ATH - 1]) {
                                min = ATH[MAX_ATH - 1];
                            }
                        }
                    }

                    ath[j] = min;
                }

                /* copy curves into working space, replicate the 50dB curve to 30 and 40, replicate the 100dB curve to 110 */

                for (j = 0; j < 6; j++)
                {
                    for (k = 0; k < EHMER_MAX; k++) {
                        workc[i, j + 2, k] = tonemasks[i, j, k];
                    }
                }

                for (k = 0; k < EHMER_MAX; k++)
                {
                    workc[i, 0, k] = tonemasks[i, 0, k];
                    workc[i, 1, k] = tonemasks[i, 0, k];
                }

                /* apply centered curve boost/decay */

                for (j = 0; j < P_LEVELS; j++)
                {
                    for (k = 0; k < EHMER_MAX; k++)
                    {
                        float adj = center_boost + Math.Abs(EHMER_OFFSET - k) * center_decay_rate;

                        if (adj < 0.0f && center_boost > 0.0f) {
                            adj = 0.0f;
                        }

                        if (adj > 0.0f && center_boost < 0.0f) {
                            adj = 0.0f;
                        }

                        workc[i, j, k] += adj;
                    }
                }

                fixed (float *fixed_workc = workc, fixed_athc = athc)
                {
                    /* normalize curves so the driving amplitude is 0dB */
                    /* make temp curves with the ATH overlayed */
                    
                    for (j = 0; j < P_LEVELS; j++)
                    {
                        fixed (float *fixed_workc_i_j = &workc[i, j, 0])
                        {
                            fixed (float *fixed_athc_j = &athc[j, 0])
                            {
                                attenuate_curve(fixed_workc, (curveatt_dB[i] + 100.0f - (j < 2 ? 2 : j) * 10.0f - P_LEVEL_0));

                                for (k = 0; k < EHMER_MAX; k++) {
                                    athc[j, k] = ath[k];
                                }

                                attenuate_curve(fixed_athc, 100.0f - j * 10.0f - P_LEVEL_0);
                                max_curve(fixed_athc_j, fixed_workc_i_j);
                            }
                        }
                    }

                    /* Now limit the louder curves.

                     the idea is this: We don't know what the playback attenuation will be; 0dB SL moves every time the user twiddles the volume
                     knob. So that means we have to use a single 'most pessimal' curve for all masking amplitudes, right?  Wrong.  The *loudest* sound
                     can be in (we assume) a range of ...+100dB] SL.  However, sounds 20dB down will be in a range ...+80], 40dB down is from ...+60],
                     etc... */
                     
                    for (j = 1; j < P_LEVELS; j++)
                    {
                        min_curve(fixed_athc, fixed_athc);
                        min_curve(fixed_workc, fixed_athc);
                    }
                }
            }

            for (i = 0; i < P_BANDS; i++)
            {
                int hi_curve, lo_curve, bin;

                ret[i] = (float **)_ogg_malloc(sizeof(float) * P_LEVELS);

                /* low frequency curves are measured with greater resolution than the MDCT/FFT will actually give us; we want the curve applied
                 to the tone data to be pessimistic and thus apply the minimum masking possible for a given bin.  That means that a single bin
                 could span more than one octave and that the curve will be a composite of multiple octaves.  It also may mean that a single
                 bin may span > an eighth of an octave and that the eighth octave values may also be composited. */

                /* which octave curves will we be compositing? */
                
                bin = floor(fromOC(i * 0.5f) / binHz);
                lo_curve = (int)Math.Ceiling(toOC(bin * binHz + 1) * 2.0f);
                hi_curve = floor(toOC((bin + 1) * binHz) * 2);

                if (lo_curve > i) {
                    lo_curve = i;
                }

                if (lo_curve < 0) {
                    lo_curve = 0;
                }

                if (hi_curve >= P_BANDS) {
                    hi_curve = P_BANDS - 1;
                }

                for (m = 0; m < P_LEVELS; m++)
                {
                    ret[i][m] = (float *)_ogg_malloc(sizeof(float) * (EHMER_MAX + 2));

                    for (j = 0; j < n; j++) {
                        brute_buffer[j] = 999.0f;
                    }

                    /* render the curve into bins, then pull values back into curve. The point is that any inherent subsampling aliasing results in
                     a safe minimum */
                    for (k = lo_curve; k <= hi_curve; k++)
                    {
                        int l = 0;

                        for (j = 0; j < EHMER_MAX; j++)
                        {
                            int lo_bin = (int)(fromOC(j * 0.125f + k * 0.5f - 2.0625f) / binHz);
                            int hi_bin = (int)(fromOC(j * 0.125f + k * 0.5f - 1.9375f) / binHz) + 1;

                            if (lo_bin < 0) {
                                lo_bin = 0;
                            }

                            if (lo_bin > n) {
                                lo_bin = n;
                            }

                            if (lo_bin < l) {
                                l = lo_bin;
                            }

                            if (hi_bin < 0) {
                                hi_bin = 0;
                            }

                            if (hi_bin > n) {
                                hi_bin = n;
                            }

                            for (; l < hi_bin && l < n; l++)
                            {
                                if (brute_buffer[l] < workc[k, m, j]) {
                                    brute_buffer[l] = workc[k, m, j];
                                }
                            }
                        }

                        for (; l < n; l++)
                        {
                            if (brute_buffer[l] > workc[k, m, EHMER_MAX - 1]) {
                                brute_buffer[l] = workc[k, m, EHMER_MAX - 1];
                            }
                        }
                    }

                    /* be equally paranoid about being valid up to next half ocatve */
                    if (i + 1 < P_BANDS)
                    {
                        int l = 0;

                        k = i + 1;

                        for (j = 0; j < EHMER_MAX; j++)
                        {
                            int lo_bin = (int)(fromOC(j * 0.125f + i * 0.5f - 2.0625f) / binHz);
                            int hi_bin = (int)(fromOC(j * 0.125f + i * 0.5f - 1.9375f) / binHz) + 1;

                            if (lo_bin < 0) {
                                lo_bin = 0;
                            }

                            if (lo_bin > n) {
                                lo_bin = n;
                            }

                            if (lo_bin < l) {
                                l = lo_bin;
                            }

                            if (hi_bin < 0) {
                                hi_bin = 0;
                            }

                            if (hi_bin > n) {
                                hi_bin = n;
                            }

                            for (; l < hi_bin && l < n; l++)
                            {
                                if (brute_buffer[l] > workc[k, m, j]) {
                                    brute_buffer[l] = workc[k, m, j];
                                }
                            }

                            if (lo_bin < 0) {
                                lo_bin = 0;
                            }

                            if (lo_bin > n) {
                                lo_bin = n;
                            }

                            if (lo_bin < l) {
                                l = lo_bin;
                            }

                            if (hi_bin < 0) {
                                hi_bin = 0;
                            }

                            if (hi_bin > n) {
                                hi_bin = n;
                            }

                            for (; l < hi_bin && l < n; l++)
                            {
                                if (brute_buffer[l] > workc[k, m, j]) {
                                    brute_buffer[l] = workc[k, m, j];
                                }
                            }
                        }
                    }

                    for (j = 0; j < EHMER_MAX; j++)
                    {
                        bin = (int)(fromOC(j * 0.125f + i * 0.5f - 2.0f) / binHz);

                        if (bin < 0) {
                            ret[i][m][j + 2] = -999.0f;
                        }
                        else
                        {
                            if (bin >= n) {
                                ret[i][m][j + 2] = -999.0f;
                            }
                            else {
                                ret[i][m][j + 2] = brute_buffer[bin];
                            }
                        }
                    }

                    /* add fenceposts */
                    for (j = 0; j < EHMER_OFFSET; j++)
                    {
                        if (ret[i][m][j + 2] > -200.0f) {
                            break;
                        }
                    }

                    ret[i][m][0] = j;

                    for (j = EHMER_MAX - 1; j > EHMER_OFFSET + 1; j--)
                    {
                        if (ret[i][m][j + 2] > -200.0f) {
                            break;
                        }
                    }
                    
                    ret[i][m][1] = j;
                }
            }
            
            return ret;
        }

        static public void _vp_psy_init(ref vorbis_look_psy p, vorbis_info_psy vi, vorbis_info_psy_global gi, int n, int rate)
        {
            int i, j, lo = -99, hi = 1;
            int maxoc;

            p = default(vorbis_look_psy);
            
            p.shiftoc = (int)rint(Math.Log(gi.eighth_octave_lines * 8.0f) / Math.Log(2.0f)) - 1;
            p.eighth_octave_lines = gi.eighth_octave_lines;

            p.firstoc = (int)(toOC(0.25f * rate * 0.5f / n) * (1 << (p.shiftoc + 1)) - gi.eighth_octave_lines);
            maxoc = (int)(toOC((n + 0.25f) * rate * 0.5f / n) * (1 << (p.shiftoc + 1)) + 0.5f);
            p.total_octave_lines = maxoc - p.firstoc + 1;
            p.ath = (float *)_ogg_malloc(n * sizeof(float));

            p.octave = (int *)_ogg_malloc(n * sizeof(int));
            p.bark = (int *)_ogg_malloc(n * sizeof(int));
            p.vi = vi;
            p.n = n;
            p.rate = rate;

            /* AoTuV HF weighting */
            p.m_val = 1.0f;

            if (rate < 26000)
            {
                p.m_val = 0;
            }
            else if (rate < 38000)
            {
                p.m_val = 0.94f;   /* 32kHz */
            }
            else if (rate > 46000)
            {
                p.m_val = 1.275f; /* 48kHz */
            }

            /* set up the lookups for a given blocksize and sample rate */
            for (i = 0, j = 0; i < MAX_ATH - 1; i++)
            {
                int endpos = (int)rint(fromOC((i + 1) * 0.125f - 2.0f) * 2.0f * n / rate);
                float _base = ATH[i];

                if (j < endpos)
                {
                    float delta = (ATH[i + 1] - _base) / (endpos - j);

                    for (; j < endpos && j < n; j++)
                    {
                        p.ath[j] = _base + 100.0f;
                        _base += delta;
                    }
                }
            }

            for (; j < n; j++)
            {
                p.ath[j] = p.ath[j - 1];
            }

            for (i = 0; i < n; i++)
            {
                float bark = (float)toBARK(rate / (2.0f * n) * i);

                for (; lo + vi.noisewindowlomin < i && toBARK(rate / (2 * n) * lo) < (bark - vi.noisewindowlo); lo++) ;
                for (; hi <= n && (hi < i + vi.noisewindowhimin || toBARK(rate / (2 * n) * hi) < (bark + vi.noisewindowhi)); hi++) ;

                p.bark[i] = ((lo - 1) << 16) + (hi - 1);
            }

            for (i = 0; i < n; i++)
            {
                p.octave[i] = (int)(toOC((i + 0.25f) * 0.5f * rate / n) * (1 << (p.shiftoc + 1)) + 0.5f);
                p.tonecurves = setup_tone_curves(ref vi.toneatt, rate * 0.5f / n, n, vi.tone_centerboost, vi.tone_decay);

                /* set up rolling noise median */
                p.noiseoffset = (float **)_ogg_malloc(P_NOISECURVES * sizeof(float *));

                for (i = 0; i < P_NOISECURVES; i++)
                {
                    p.noiseoffset[i] = (float *)_ogg_malloc(n * sizeof(float));
                }

                for (i = 0; i < n; i++)
                {
                    float halfoc = (float)toOC((i + 0.5f) * rate / (2.0f * n)) * 2.0f;
                    int inthalfoc;
                    float del;

                    if (halfoc < 0)
                    {
                        halfoc = 0;
                    }

                    if (halfoc >= P_BANDS - 1)
                    {
                        halfoc = P_BANDS - 1;
                    }

                    inthalfoc = (int)halfoc;
                    del = halfoc - inthalfoc;

                    for (j = 0; j < P_NOISECURVES; j++)
                    {
                        p.noiseoffset[j][i] = p.vi.noiseoff[j, inthalfoc] * (1.0f - del) + p.vi.noiseoff[j, inthalfoc + 1] * del;
                    }
                }
            }
        }
        
        static void _vp_psy_clear(ref vorbis_look_psy p)
        {
            p = default(vorbis_look_psy);
        }
        
        /* octave / (8*eighth_octave_lines) x scale and dB y scale */
        static void seed_curve(float* seed, float** curves, float amp, int oc, int n, int linesper, float dBoffset)
        {
            int i,post1;
            int seedptr;
            float* posts, curve;

            int choice = (int)((amp + dBoffset - P_LEVEL_0) * 0.1f);
            choice = Math.Max(choice, 0);
            choice = Math.Min(choice, P_LEVELS - 1);
            posts = curves[choice];
            curve = posts + 2;
            post1 = (int)posts[1];
            seedptr = (int)(oc + (posts[0] - EHMER_OFFSET) * linesper) - (linesper >> 1);

            for (i = (int)posts[0]; i < post1; i++)
            {
                if (seedptr > 0)
                {
                    float lin = amp + curve[i];
                    
                    if (seed[seedptr] < lin) {
                        seed[seedptr] = lin;
                    }
                }
                
                seedptr += linesper;
                
                if (seedptr >= n) {
                    break;
                }
            }
        }
        
        static void seed_loop(ref vorbis_look_psy p, float ***curves, float *f, float *flr, float *seed, float specmax)
        {
            vorbis_info_psy vi = p.vi;
            int i, n = p.n;
            float dBoffset = vi.max_curve_dB - specmax;
            
            /* prime the working vector with peak values */

            for(i=0;i<n;i++)
            {
                float max=f[i];
                int oc=p.octave[i];
    
                while(i+1<n && p.octave[i+1]==oc)
                {
                    i++;
                    
                    if(f[i]>max) {
                        max=f[i];
                    }
                }

                if(max+6.0f > flr[i]) 
                {
                    oc=oc>>p.shiftoc;

                    if(oc>=P_BANDS) {
                        oc=P_BANDS-1;
                    }
                    
                    if(oc<0) {
                        oc=0;
                    }
                    
                    seed_curve
                    (
                        seed, 
                        curves[oc], 
                        max, 
                        p.octave[i] - p.firstoc,
                        p.total_octave_lines,
                        p.eighth_octave_lines,
                        dBoffset
                    );
                }
            }
        }
        
        static void seed_chase(float *seeds, int linesper, int n)
        {
            int* posstack = stackalloc int[n];
            float* ampstack = stackalloc float[n];
            
            int stack = 0;
            int pos = 0;
            int i;

            for(i = 0; i < n; i++)
            {
                if (stack < 2)
                {
                    posstack[stack]=i;
                    ampstack[stack++]=seeds[i];
                }
                else
                {
                    while(true)
                    {
                        if(seeds[i]<ampstack[stack-1])
                        {
                            posstack[stack]=i;
                            ampstack[stack++]=seeds[i];
                            break;
                        }
                        else 
                        {
                            if(i<posstack[stack-1]+linesper)
                            {
                                if(stack>1 && ampstack[stack-1]<=ampstack[stack-2] && i<posstack[stack-2]+linesper)
                                {
                                    /* we completely overlap, making stack-1 irrelevant.  pop it */
                                    stack--;
                                    continue;
                                }
                            }
                            
                            posstack[stack]=i;
                            ampstack[stack++]=seeds[i];
                            break;
                        }
                    }
                }
            }

            /* the stack now contains only the positions that are relevant. Scan
               'em straight through */

            for (i = 0; i < stack; i++)
            {
                int endpos;
                
                if (i < stack - 1 && ampstack[i + 1] > ampstack[i]) {
                    endpos = posstack[i + 1];
                }
                else {
                    /* +1 is important, else bin 0 is discarded in short frames */
                    endpos = posstack[i] + linesper + 1; 
                }
                
                if (endpos > n) {
                    endpos = n;
                }

                for (; pos < endpos; pos++) {
                    seeds[pos] = ampstack[i];
                }
            }

            /* there.  Linear time.  I now remember this was on a problem set I
               had in Grad Skool... I didn't solve it at the time ;-) */
        }
        
        static void max_seeds(ref vorbis_look_psy p, float *seed, float *flr)
        {
            int n=p.total_octave_lines;
            int linesper=p.eighth_octave_lines;
            int linpos=0;
            int pos;

            seed_chase(seed,linesper,n); /* for masking */

            pos=p.octave[0]-p.firstoc-(linesper>>1);

            while (linpos + 1 < p.n)
            {
                float minV = seed[pos];
                int end = ((p.octave[linpos] + p.octave[linpos + 1]) >> 1) - p.firstoc;
                
                if (minV > p.vi.tone_abs_limit) {
                    minV = p.vi.tone_abs_limit;
                }
                
                while (pos + 1 <= end)
                {
                    pos++;
                
                    if ((seed[pos] > NEGINF && seed[pos] < minV) || minV == NEGINF) {
                        minV = seed[pos];
                    }
                }

                end = pos + p.firstoc;
                
                for (; linpos < p.n && p.octave[linpos] <= end; linpos++) {
                    if (flr[linpos] < minV) {
                        flr[linpos] = minV;
                    }
                }
            }

            {
                float minV = seed[p.total_octave_lines - 1];
                
                for (; linpos < p.n; linpos++) {
                    if (flr[linpos] < minV) flr[linpos] = minV;
                }
            }
        }
        
        static void bark_noise_hybridmp(int n, int *b, float *f, float *noise, float offset, int _fixed)
        {
            float* N = stackalloc float[n];
            float* X = stackalloc float[n];
            float* XX = stackalloc float[n];
            float* Y = stackalloc float[n];
            float* XY = stackalloc float[n];
            float tN, tX, tXX, tY, tXY;

            int i;
            int lo, hi;

            float R = 0.0f;
            float A = 0.0f;
            float B = 0.0f;
            float D = 1.0f;
            float w, x, y;

            tN = tX = tXX = tY = tXY = 0.0f;
            y = f[0] + offset;

            if (y < 1.0f)
            {
                y = 1.0f;
            }

            w = y * y * 0.5f;

            tN += w;
            tX += w;
            tY += w * y;

            N[0] = tN;
            X[0] = tX;
            XX[0] = tXX;
            Y[0] = tY;
            XY[0] = tXY;

            for (i = 1, x = 1.0f; i < n; i++, x += 1.0f)
            {
                y = f[i] + offset;

                if (y < 1.0f)
                {
                    y = 1.0f;
                }

                w = y * y;

                tN += w;
                tX += w * x;
                tXX += w * x * x;
                tY += w * y;
                tXY += w * x * y;

                N[i] = tN;
                X[i] = tX;
                XX[i] = tXX;
                Y[i] = tY;
                XY[i] = tXY;
            }

            for (i = 0, x = 0.0f; ; i++, x += 1.0f)
            {
                lo = b[i] >> 16;

                if (lo >= 0)
                {
                    break;
                }

                hi = b[i] & 0xffff;

                tN = N[hi] + N[-lo];
                tX = X[hi] - X[-lo];
                tXX = XX[hi] + XX[-lo];
                tY = Y[hi] + Y[-lo];
                tXY = XY[hi] - XY[-lo];

                A = tY * tXX - tX * tXY;
                B = tN * tXY - tX * tY;
                D = tN * tXX - tX * tX;
                R = (A + x * B) / D;

                if (R < 0.0f)
                {
                    R = 0.0f;
                }

                noise[i] = R - offset;
            }

            for (; ; i++, x += 1.0f)
            {
                lo = b[i] >> 16;
                hi = b[i] & 0xffff;

                if (hi >= n)
                {
                    break;
                }

                tN = N[hi] - N[lo];
                tX = X[hi] - X[lo];
                tXX = XX[hi] - XX[lo];
                tY = Y[hi] - Y[lo];
                tXY = XY[hi] - XY[lo];

                A = tY * tXX - tX * tXY;
                B = tN * tXY - tX * tY;
                D = tN * tXX - tX * tX;
                R = (A + x * B) / D;

                if (R < 0.0f)
                {
                    R = 0.0f;
                }

                noise[i] = R - offset;
            }

            for (; i < n; i++, x += 1.0f)
            {
                R = (A + x * B) / D;

                if (R < 0.0f)
                {
                    R = 0.0f;
                }

                noise[i] = R - offset;
            }

            if (_fixed <= 0)
            {
                return;
            }

            for (i = 0, x = 0.0f; ; i++, x += 1.0f)
            {
                hi = i + _fixed / 2;
                lo = hi - _fixed;

                if (lo >= 0)
                {
                    break;
                }

                tN = N[hi] + N[-lo];
                tX = X[hi] - X[-lo];
                tXX = XX[hi] + XX[-lo];
                tY = Y[hi] + Y[-lo];
                tXY = XY[hi] - XY[-lo];

                A = tY * tXX - tX * tXY;
                B = tN * tXY - tX * tY;
                D = tN * tXX - tX * tX;
                R = (A + x * B) / D;

                if (R - offset < noise[i])
                {
                    noise[i] = R - offset;
                }
            }

            for (; ; i++, x += 1.0f)
            {
                hi = i + _fixed / 2;
                lo = hi - _fixed;

                if (hi >= n)
                {
                    break;
                }

                tN = N[hi] - N[lo];
                tX = X[hi] - X[lo];
                tXX = XX[hi] - XX[lo];
                tY = Y[hi] - Y[lo];
                tXY = XY[hi] - XY[lo];

                A = tY * tXX - tX * tXY;
                B = tN * tXY - tX * tY;
                D = tN * tXX - tX * tX;
                R = (A + x * B) / D;

                if (R - offset < noise[i])
                {
                    noise[i] = R - offset;
                }
            }

            for (; i < n; i++, x += 1.0f)
            {
                R = (A + x * B) / D;

                if (R - offset < noise[i])
                {
                    noise[i] = R - offset;
                }
            }
        }
        
        static void _vp_noisemask(ref vorbis_look_psy p, float* logmdct, float* logmask)
        {
            int i, n = p.n;
            float* work = stackalloc float[n];

            bark_noise_hybridmp(n, p.bark, logmdct, logmask, 140.0f, -1);

            for (i = 0; i < n; i++)
            {
                work[i] = logmdct[i] - logmask[i];
            }

            bark_noise_hybridmp(n, p.bark, work, logmask, 0.0f, p.vi.noisewindowfixed);

            for (i = 0; i < n; i++) 
            {
                work[i] = logmdct[i] - work[i];
            }

            for (i = 0; i < n; i++)
            {
                int dB = (int)(logmask[i] + 0.5f);

                if (dB >= NOISE_COMPAND_LEVELS)
                {
                    dB = NOISE_COMPAND_LEVELS - 1;
                }

                if (dB < 0)
                {
                    dB = 0;
                }

                logmask[i] = work[i] + p.vi.noisecompand[dB];
            }
        }
        
        static void _vp_tonemask(ref vorbis_look_psy p, float *logfft, float *logmask, float global_specmax, float local_specmax)
        {
            int i, n = p.n;

            float* seed = stackalloc float[p.total_octave_lines];
            float att = local_specmax + p.vi.ath_adjatt;

            for (i = 0; i < p.total_octave_lines; i++)
            {
                seed[i] = NEGINF;
            }

            /* set the ATH (floating below localmax, not global max by a specified att) */
            if (att < p.vi.ath_maxatt)
            {
                att = p.vi.ath_maxatt;
            }

            for (i = 0; i < n; i++)
            {
                logmask[i] = p.ath[i] + att;
            }

            /* tone masking */
            seed_loop(ref p, p.tonecurves, logfft, logmask, seed, global_specmax);
            max_seeds(ref p, seed, logmask);
        }
        
        static void _vp_offset_and_mix(ref vorbis_look_psy p, float *noise, float *tone, int offset_select, float *logmask, float *mdct, float *logmdct)
        {
            int i, n = p.n;
            
            float de, coeffi, cx;/* AoTuV */
            float toneatt = p.vi.tone_masteratt[offset_select];

            cx = p.m_val;

            for (i = 0; i < n; i++)
            {
                float val = noise[i] + p.noiseoffset[offset_select][i];
                
                if (val > p.vi.noisemaxsupp) {
                    val = p.vi.noisemaxsupp;
                }
                
                logmask[i] = Math.Max(val, tone[i] + toneatt);

                /* AoTuV */
                /** @ M1 **
                The following codes improve a noise problem.
                A fundamental idea uses the value of masking and carries out
                the relative compensation of the MDCT.
                However, this code is not perfect and all noise problems cannot be solved.
                by Aoyumi @ 2004/04/18
              */

                if (offset_select == 1)
                {
                    coeffi = -17.2f; /* coeffi is a -17.2dB threshold */
                    val = val - logmdct[i];  /* val == mdct line value relative to floor in dB */

                    if (val > coeffi)
                    {
                        /* mdct value is > -17.2 dB below floor */
                        de = 1.0f - ((val - coeffi) * 0.005f * cx);

                        /* pro-rated attenuation:
                        -0.00 dB boost if mdct value is -17.2dB (relative to floor)
                        -0.77 dB boost if mdct value is 0dB (relative to floor)
                        -1.64 dB boost if mdct value is +17.2dB (relative to floor)
                        etc... */

                        if (de < 0) {
                            de = 0.0001f;
                        }
                    }
                    else
                    {
                        /* mdct value is <= -17.2 dB below floor */
                        de = 1.0f - ((val - coeffi) * 0.0003f * cx);
                        
                        /* pro-rated attenuation:
                        +0.00 dB atten if mdct value is -17.2dB (relative to floor)
                        +0.45 dB atten if mdct value is -34.4dB (relative to floor)
                        etc... */
                    }
                    
                    mdct[i] *= de;
                }
            }            
        }
    
        static float _vp_ampmax_decay(float amp, ref vorbis_dsp_state vd)
        {
            vorbis_info vi = vd.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            vorbis_info_psy_global gi = ci.psy_g_param;

            int n = ci.blocksizes[vd.W] / 2;
            float secs = (float)n / vi.rate;

            amp += secs * gi.ampmax_att_per_sec;

            if (amp < -9999)
            {
                amp = -9999;
            }

            return amp;
        }
        
        static void flag_lossless(int limit, float prepoint, float postpoint, float *mdct, float *floor, int *flag, int i, int jn)
        {
            int j;

            for (j = 0; j < jn; j++)
            {
                float point = (j >= limit - i) ? postpoint : prepoint;
                float r = Math.Abs(mdct[j]) / floor[j];

                if (r < point)
                {
                    flag[j] = 0;
                }
                else
                {
                    flag[j] = 1;
                }
            }
        }
        
        /* Overload/Side effect: On input, the *q vector holds either the quantized energy (for elements with the flag set) or the absolute
          values of the *r vector (for elements with flag unset).  On output, *q holds the quantized energy for all elements */
        static float noise_normalize(ref vorbis_look_psy p, int limit, float *r, float *q, float *f, int *flags, float acc, int i, int n, int *_out)
        {
            vorbis_info_psy vi = p.vi;
            IntPtr[] sort = new IntPtr[n];
            
            int j,count=0;
            int start = (vi.normal_p != 0) ? vi.normal_start - i : n;
            
            if (start > n) {
                start=n;
            }

            /* force classic behavior where only energy in the current band is considered */
            acc = 0.0f;

            /* still responsible for populating *out where noise norm not in effect.  There's no need to [re]populate *q in these areas */
            for (j = 0; j < start; j++)
            {
                if (flags == null || flags[j] == 0)
                {
                    /* lossless coupling already quantized. Don't touch; requantizing based on energy would be incorrect. */
                    float ve = q[j] / f[j];

                    if (r[j] < 0)
                    {
                        _out[j] = -rint(Math.Sqrt(ve));
                    }
                    else
                    {
                        _out[j] = rint(Math.Sqrt(ve));
                    }
                }
            }

            /* sort magnitudes for noise norm portion of partition */
            for (; j < n; j++)
            {
                if (flags == null || flags[j] != 0)
                {
                    /* can't noise norm elements that have already been loslessly coupled; we can only account for their energy error */
                    float ve = q[j] / f[j];

                    /* Despite all the new, more capable coupling code, for now we implement noise norm as it has been up to this point. Only
                      consider promotions to unit magnitude from 0.  In addition the only energy error counted is quantizations to zero. */

                    /* also-- the original point code only applied noise norm at > pointlimit */
                    if (ve < 0.25f && (flags == null || j >= limit - i))
                    {
                        acc += ve;
                        sort[count++] = (IntPtr)(q + j); /* q is fabs(r) for unflagged element */
                    }
                    else
                    {
                        /* For now: no acc adjustment for nonzero quantization.  populate *out and q as this value is final. */
                        if (r[j] < 0)
                        {
                            _out[j] = -rint(Math.Sqrt(ve));
                        }
                        else
                        {
                            _out[j] = rint(Math.Sqrt(ve));
                            q[j] = _out[j] * _out[j] * f[j];
                        }
                    }
                }
                /* else {
                  again, no energy adjustment for error in nonzero quant-- for now
              }*/
            }

            if (count != 0)
            {
                /* noise norm to do */
                Array.Sort
                (
                    sort,
                    (arg1, arg2) => 
                    { 
                        float f1 = *(float *)arg1;
                        float f2 = *(float *)arg2;
                        return (f1 < f2 ? 1 : 0) - (f1 > f2 ? 1 : 0);
                    }
                );
                
                for (j = 0; j < count; j++)
                {
                    int k = (int)((float *)sort[j].ToPointer() - q);

                    if (acc >= vi.normal_thresh)
                    {
                        _out[k] = (int)unitnorm(r[k]);
                        acc -= 1.0f;
                        q[k] = f[k];
                    }
                    else
                    {
                        _out[k] = 0;
                        q[k] = 0.0f;
                    }
                }
            }

            return acc;
        }
        
        /* Noise normalization, quantization and coupling are not wholly seperable processes in depth>1 coupling. */
        static void _vp_couple_quantize_normalize(int blobno, vorbis_info_psy_global g, ref vorbis_look_psy p, vorbis_info_mapping0 vi, float** mdct, int** iwork, int* nonzero, int sliding_lowpass, int ch)
        {
            int i;
            int n = p.n;
            int partition = (p.vi.normal_p != 0 ? p.vi.normal_partition : 16);
            int limit = g.coupling_pointlimit[p.vi.blockflag, blobno];
            float prepoint = (float)stereo_threshholds[g.coupling_prepointamp[blobno]];
            float postpoint = (float)stereo_threshholds[g.coupling_postpointamp[blobno]];

            /* mdct is our raw mdct output, floor not removed. */
            /* inout passes in the ifloor, passes back quantized result */

            /* unquantized energy (negative indicates amplitude has negative sign) */
            float** raw = stackalloc float*[ch];

            /* dual pupose; quantized energy (if flag set), othersize fabs(raw) */
            float** quant = stackalloc float*[ch];

            /* floor energy */
            float** floor = stackalloc float*[ch];

            /* flags indicating raw/quantized status of elements in raw vector */
            int** flag = stackalloc int*[ch];

            /* non-zero flag working vector */
            int* nz = stackalloc int[ch];

            /* energy surplus/defecit tracking */
            float* acc = stackalloc float[ch + vi.coupling_steps];

            /* The threshold of a stereo is changed with the size of n */
            if (n > 1000)
            {
                postpoint = (float)stereo_threshholds_limited[g.coupling_postpointamp[blobno]];
            }

            float* raw_0 = stackalloc float[ch * partition];
            float* quant_0 = stackalloc float[ch * partition];
            float* floor_0 = stackalloc float[ch * partition];
            int* flag_0 = stackalloc int[ch * partition];

            raw[0] = raw_0;
            quant[0] = quant_0;
            floor[0] = floor_0;
            flag[0] = flag_0;

            for (i = 1; i < ch; i++)
            {
                raw[i] = &raw[0][partition * i];
                quant[i] = &quant[0][partition * i];
                floor[i] = &floor[0][partition * i];
                flag[i] = &flag[0][partition * i];
            }

            for (i = 0; i < ch + vi.coupling_steps; i++)
            {
                acc[i] = 0.0f;
            }

            for (i = 0; i < n; i += partition)
            {
                int k, j, jn = (partition > n - i) ? n - i : partition;
                int step, track = 0;

                CopyMemory((IntPtr)nz, (IntPtr)nonzero, sizeof(int) * ch);

                /* prefill */
                ZeroMemory((IntPtr)flag[0], ch * partition * sizeof(int));

                for (k = 0; k < ch; k++)
                {
                    int* iout = &iwork[k][i];

                    if (nz[k] != 0)
                    {
                        for (j = 0; j < jn; j++)
                        {
                            floor[k][j] = FLOOR1_fromdB_LOOKUP[iout[j]];
                        }

                        flag_lossless(limit, prepoint, postpoint, &mdct[k][i], floor[k], flag[k], i, jn);

                        for (j = 0; j < jn; j++)
                        {
                            quant[k][j] = raw[k][j] = mdct[k][i + j] * mdct[k][i + j];

                            if (mdct[k][i + j] < 0.0f)
                            {
                                raw[k][j] *= -1.0f;
                            }

                            floor[k][j] *= floor[k][j];
                        }

                        acc[track] = noise_normalize(ref p, limit, raw[k], quant[k], floor[k], null, acc[track], i, jn, iout);
                    }
                    else
                    {
                        for (j = 0; j < jn; j++)
                        {
                            floor[k][j] = 1e-10f;
                            raw[k][j] = 0.0f;
                            quant[k][j] = 0.0f;
                            flag[k][j] = 0;
                            iout[j] = 0;
                        }

                        acc[track] = 0.0f;
                    }

                    track++;
                }

                /* coupling */
                for (step = 0; step < vi.coupling_steps; step++)
                {
                    int Mi = vi.coupling_mag[step];
                    int Ai = vi.coupling_ang[step];
                    int* iM = &iwork[Mi][i];
                    int* iA = &iwork[Ai][i];
                    float* reM = raw[Mi];
                    float* reA = raw[Ai];
                    float* qeM = quant[Mi];
                    float* qeA = quant[Ai];
                    float* floorM = floor[Mi];
                    float* floorA = floor[Ai];
                    int* fM = flag[Mi];
                    int* fA = flag[Ai];

                    if (nz[Mi] != 0 || nz[Ai] != 0)
                    {
                        nz[Mi] = nz[Ai] = 1;

                        for (j = 0; j < jn; j++)
                        {
                            if (j < sliding_lowpass - i)
                            {
                                if (fM[j] != 0 || fA[j] != 0)
                                {
                                    /* lossless coupling */
                                    reM[j] = Math.Abs(reM[j]) + Math.Abs(reA[j]);
                                    qeM[j] = qeM[j] + qeA[j];
                                    fM[j] = fA[j] = 1;

                                    /* couple iM/iA */
                                    {
                                        int A = iM[j];
                                        int B = iA[j];

                                        if (Math.Abs(A) > Math.Abs(B))
                                        {
                                            iA[j] = (A > 0 ? A - B : B - A);
                                        }
                                        else
                                        {
                                            iA[j] = (B > 0 ? A - B : B - A);
                                            iM[j] = B;
                                        }

                                        /* collapse two equivalent tuples to one */
                                        if (iA[j] >= Math.Abs(iM[j]) * 2)
                                        {
                                            iA[j] = -iA[j];
                                            iM[j] = -iM[j];
                                        }
                                    }
                                }
                                else
                                {
                                    /* lossy (point) coupling */
                                    if (j < limit - i)
                                    {
                                        /* dipole */
                                        reM[j] += reA[j];
                                        qeM[j] = Math.Abs(reM[j]);
                                    }
                                    else
                                    {
                                        /* elliptical */
                                        if (reM[j] + reA[j] < 0)
                                        {
                                            reM[j] = -(qeM[j] = Math.Abs(reM[j]) + Math.Abs(reA[j]));
                                        }
                                        else
                                        {
                                            reM[j] = (qeM[j] = Math.Abs(reM[j]) + Math.Abs(reA[j]));
                                        }
                                    }

                                    reA[j] = qeA[j] = 0.0f;
                                    fA[j] = 1;
                                    iA[j] = 0;
                                }
                            }
                            
                            floorM[j] = floorA[j] = floorM[j] + floorA[j];
                        }

                        /* normalize the resulting mag vector */
                        acc[track] = noise_normalize(ref p, limit, raw[Mi], quant[Mi], floor[Mi], flag[Mi], acc[track], i, jn, iM);
                        track++;
                    }
                }
            }

            for (i = 0; i < vi.coupling_steps; i++)
            {
                /* make sure coupling a zero and a nonzero channel results in two
                   nonzero channels. */
                if (nonzero[vi.coupling_mag[i]] != 0 || nonzero[vi.coupling_ang[i]] != 0)
                {
                    nonzero[vi.coupling_mag[i]] = 1;
                    nonzero[vi.coupling_ang[i]] = 1;
                }
            }
        }
    }
}
