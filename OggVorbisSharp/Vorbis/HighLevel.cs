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

 function: highlevel encoder setup struct separated out for vorbisenc clarity
 last mod: $Id: highlevel.h 17195 2010-05-05 21:49:51Z giles $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    static public unsafe partial class Vorbis
    {
        public class highlevel_byblocktype 
        {
            public double tone_mask_setting;
            public double tone_peaklimit_setting;
            public double noise_bias_setting;
            public double noise_compand_setting;
        }
        
        public class highlevel_encode_setup 
        {
            public int set_in_stone;
            public IntPtr setup;
            public double base_setting;

            public double impulse_noisetune;

            /* bitrate management below all settable */
            public float req;
            public int managed;
            public long bitrate_min;
            public long bitrate_av;
            public double bitrate_av_damp;
            public long bitrate_max;
            public long bitrate_reservoir;
            public double bitrate_reservoir_bias;

            public int impulse_block_p;
            public int noise_normalize_p;
            public int coupling_p;

            public double stereo_point_setting;
            public double lowpass_kHz;
            public int lowpass_altered;

            public double ath_floating_dB;
            public double ath_absolute_dB;

            public double amplitude_track_dBpersec;
            public double trigger_setting;

            public highlevel_byblocktype[] block = new highlevel_byblocktype[4]; /* padding, impulse, transition, long */
        } 
    }
}
