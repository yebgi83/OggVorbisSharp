/********************************************************************
 *                                                                  *
 * THIS FILE IS PART OF THE OggVorbis SOFTWARE CODEC SOURCE CODE.   *
 * USE, DISTRIBUTION AND REPRODUCTION OF THIS LIBRARY SOURCE IS     *
 * GOVERNED BY A BSD-STYLE SOURCE LICENSE INCLUDED WITH THIS SOURCE *
 * IN 'COPYING'. PLEASE READ THESE TERMS BEFORE DISTRIBUTING.       *
 *                                                                  *
 * THE OggVorbis SOURCE CODE IS (C) COPYRIGHT 1994-2007             *
 * by the Xiph.Org Foundation http://www.xiph.org/                  *
 *                                                                  *
 ********************************************************************

 function: single-block PCM analysis mode dispatch
 last mod: $Id: analysis.c 16226 2009-07-08 06:43:49Z xiphmont $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Analysis
    static public unsafe partial class Vorbis
    {
        static public int vorbis_analysis(ref vorbis_block vb, ref Ogg.ogg_packet op)
        {
            int ret, i;
            
            vorbis_block_internal vbi = vb._internal as vorbis_block_internal;
            
            vb.glue_bits = 0;
            vb.time_bits = 0;
            vb.floor_bits = 0;
            vb.res_bits = 0;
            
            /* first things first.  Make sure encode is ready */
            
            for (i = 0; i < PACKETBLOBS; i++) {
                Ogg.oggpack_reset(ref vbi.packetblob[i]);
            }
            
            /* we only have one mapping type (0), and we let the mapping code itself figure out what soft mode to use.  
             This allows easier bitrate management */
            
            if ((ret = _mapping_P[0].forward(ref vb)) != 0) {
                return ret;
            }
            
            if (vorbis_bitrate_managed(ref vb) != 0) 
            {
                /* The app is using a bitmanaged mode... but not using the
                 bitrate management interface. */
                return OV_EINVAL;
            }
            
            op.packet = Ogg.oggpack_get_buffer(ref vb.opb);
            op.bytes = Ogg.oggpack_bytes(ref vb.opb);
            op.b_o_s = 0;
            op.granulepos = vb.granulepos;
            op.packetno = vb.sequence; /* for sake of completeness */
            
            return 0;
        }
    }
}
