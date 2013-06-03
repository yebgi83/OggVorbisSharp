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

 function: single-block PCM synthesis
 last mod: $Id: synthesis.c 17474 2010-09-30 03:41:41Z gmaxwell $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Synthesis
    static public unsafe partial class Vorbis
    {
        static public int vorbis_synthesis(ref vorbis_block vb, ref Ogg.ogg_packet op)
        {
            vorbis_dsp_state vd = vb.vd;
            private_state b = vd.backend_state as private_state;
            vorbis_info vi = vd.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            Ogg.oggpack_buffer opb = vb.opb;

            int type, mode, i;

            if (vd == null || b == null || vi == null || ci == null || opb == null)
            {
                return OV_EBADHEADER;
            }

            /* first things first.  Make sure decode is ready */
            _vorbis_block_ripcord(ref vb);
            Ogg.oggpack_readinit(ref opb, op.packet, op.bytes);

            /* Check the packet type */
            if (Ogg.oggpack_read(ref opb, 1) != 0)
            {
                /* Oops.  This is not an audio data packet */
                return OV_ENOTAUDIO;
            }

            /* read our mode and pre/post windowsize */
            mode = Ogg.oggpack_read(ref opb, b.modebits);

            if (mode == -1)
            {
                return OV_EBADPACKET;
            }

            vb.mode = mode;
            
            if (ci.mode_param[mode] == null)
            {
                return OV_EBADPACKET;
            }
            
            vb.W = ci.mode_param[mode].blockflag;

            if (vb.W > 0)
            {
                /* this doesn't get mapped through mode selection as it's used only for window selection */
                vb.lW = Ogg.oggpack_read(ref opb, 1);
                vb.nW = Ogg.oggpack_read(ref opb, 1);

                if (vb.nW == -1)
                {
                    return OV_EBADPACKET;
                }
            }
            else
            {
                vb.lW = 0;
                vb.nW = 0;
            }

            /* more setup */
            vb.granulepos = op.granulepos;
            vb.sequence = op.packetno;
            vb.eofflag = op.e_o_s;

            /* alloc pcm passback storage */
            vb.pcmend = ci.blocksizes[vb.W];
            vb.pcm = (float**)_vorbis_block_alloc(ref vb, sizeof(float*) * vi.channels);

            for (i = 0; i < vi.channels; i++)
            {
                vb.pcm[i] = (float*)_vorbis_block_alloc(ref vb, sizeof(float) * vb.pcmend);
            }

            /* unpack_header enforces range checking */
            type = ci.map_type[ci.mode_param[mode].mapping];

            return _mapping_P[type].inverse(ref vb, ci.map_param[ci.mode_param[mode].mapping]);
        }
        
        /* used to track pcm position without actually performing decode. Useful for sequential 'fast forward' */
        static public int vorbis_synthesis_trackonly(ref vorbis_block vb, ref Ogg.ogg_packet op)
        {
            vorbis_dsp_state vd = vb.vd;
            private_state b = vd.backend_state as private_state;
            vorbis_info vi = vd.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            Ogg.oggpack_buffer opb = vb.opb;        
            int mode;

            /* first things first.  Make sure decode is ready */
            _vorbis_block_ripcord(ref vb);
            Ogg.oggpack_readinit(ref opb, op.packet, op.bytes);

            /* Check the packet type */
            if (Ogg.oggpack_read(ref opb, 1) != 0) 
            {
                /* Oops. This is not an audio data packet */
                return OV_ENOTAUDIO;
            }

            /* read our mode and pre/post windowsize */
            mode = Ogg.oggpack_read(ref opb, b.modebits);
           
            if (mode == -1) {
                return(OV_EBADPACKET);
            }
            
            vb.mode = mode;
  
            vb.W = ci.mode_param[mode].blockflag;
            if (vb.W > 0)
            {
                vb.lW = Ogg.oggpack_read(ref opb, 1);
                vb.nW = Ogg.oggpack_read(ref opb, 1);
    
                if (vb.nW == -1) {
                    return(OV_EBADPACKET);
                }
            }
            else
            {
                vb.lW = 0;
                vb.nW = 0;
            }
        
            /* more setup */
            vb.granulepos = op.granulepos;
            vb.sequence = op.packetno;
            vb.eofflag = op.e_o_s;

            /* no pcm */
            vb.pcmend = 0;
            vb.pcm = null;

            return 0;
        }
        
        static public int vorbis_packet_blocksize(ref vorbis_info vi, ref Ogg.ogg_packet op)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            Ogg.oggpack_buffer opb = new Ogg.oggpack_buffer();

            int mode;

            Ogg.oggpack_readinit(ref opb, op.packet, op.bytes);

            /* Check the packet type */
            if (Ogg.oggpack_read(ref opb, 1) !=0 )
            {
                /* Oops.  This is not an audio data packet */
                return OV_ENOTAUDIO;
            }

            {
                int modebits = 0;
                int v = ci.modes;
                
                while (v > 1)
                {
                    modebits++;
                    v >>= 1;
                }

                /* read our mode and pre/post windowsize */
                mode = Ogg.oggpack_read(ref opb, modebits);
            }
  
            if (mode == -1) {
                return(OV_EBADPACKET);
            }
            
            return ci.blocksizes[ci.mode_param[mode].blockflag];
        }
    }
}