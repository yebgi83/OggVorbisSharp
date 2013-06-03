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

 function: bitrate tracking and management
 last mod: $Id: bitrate.h 13293 2007-07-24 00:09:47Z xiphmont $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Type
	static public unsafe partial class Vorbis
	{
	    public class bitrate_manager_state 
	    {
	        public int managed;
	        
            public int avg_reservoir;
            public int minmax_reservoir;
            public int avg_bitsper;
            public int min_bitsper;
            public int max_bitsper;

            public int short_per_long;
            public double avgfloat;

            public vorbis_block vb;
            public int choice;
        }
        
        public class bitrate_manager_info
        {
            public int avg_rate;
            public int min_rate;
            public int max_rate;
            public int reservoir_bits;
            public double reservoir_bias;

            public double slew_damp;            
        }
	}
	
	// Bitrate
	static public unsafe partial class Vorbis
	{
	    /* compute bitrate tracking setup  */
	    static public void vorbis_bitrate_init(ref vorbis_info vi, ref bitrate_manager_state bm)
	    {
	        if (bm == null)
	        {
	            return;
	        }
	        
	        codec_setup_info ci = vi.codec_setup as codec_setup_info;
	        bitrate_manager_info bi = ci.bi;
	        
	        vorbis_bitrate_clear(ref bm);
	        
	        if (bi.reservoir_bias > 0) 
	        {
	            int ratesamples = vi.rate;
	            int halfsamples = ci.blocksizes[0] >> 1;
	            
	            bm.short_per_long = ci.blocksizes[1] / ci.blocksizes[0];
	            bm.managed = 1;
	            
	            bm.avg_bitsper = rint(1.0f * bi.avg_rate * halfsamples / ratesamples);
	            bm.min_bitsper = rint(1.0f * bi.min_rate * halfsamples / ratesamples);
	            bm.max_bitsper = rint(1.0f * bi.max_rate * halfsamples / ratesamples);
	            
	            bm.avgfloat = PACKETBLOBS / 2;
	            
	            /* not a necessary fix, but one that leads to a more balanced typical initialization */
                {
                    int desired_fill = (int)(bi.reservoir_bits * bi.reservoir_bias);
                    
                    bm.minmax_reservoir = desired_fill;
                    bm.avg_reservoir = desired_fill;
                }
	        }
	    }
	    
	    static public void vorbis_bitrate_clear(ref bitrate_manager_state bm) 
	    {
	        if (bm != null)
	        {
	            bm.managed = 0;
	            
                bm.avg_reservoir = 0;
                bm.minmax_reservoir = 0;
                bm.avg_bitsper = 0;
                bm.min_bitsper = 0;
                bm.max_bitsper = 0;

                bm.short_per_long = 0;
                bm.avgfloat = 0.0f;
                
                bm.vb = null;
                bm.choice = 0;
            }
	    }
	    
	    static public int vorbis_bitrate_managed(ref vorbis_block vb)
	    {
            vorbis_dsp_state vd = vb.vd;
            private_state b = vd.backend_state as private_state;
            bitrate_manager_state bm = b.bms;

            if (bm.managed != 0)
            {
                return 1;
            }
            else
            {
                return 0;
            }
	    }
	    
	    /* finish taking in the block we just processed */
	    static public int vorbis_bitrate_addblock(ref vorbis_block vb)
	    {
            vorbis_block_internal vbi = vb._internal as vorbis_block_internal;
            vorbis_dsp_state  vd = vb.vd;
            private_state b = vd.backend_state as private_state;
            bitrate_manager_state bm = b.bms;
            vorbis_info vi = vd.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            bitrate_manager_info bi = ci.bi;

            int choice = rint(bm.avgfloat);
            int this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
            int min_target_bits = (vb.W != 0 ? bm.min_bitsper * bm.short_per_long : bm.min_bitsper);
            int max_target_bits = (vb.W != 0 ? bm.max_bitsper * bm.short_per_long : bm.max_bitsper);
            int samples = ci.blocksizes[vb.W] >> 1;
            int desired_fill = (int)(bi.reservoir_bits * bi.reservoir_bias);

            if (bm.managed != 0)
            {
                /* not a bitrate managed stream, but for API simplicity, we'll buffer the packet to keep the code path clean */
                bm.vb = vb;
                return 0;
            }
                 
            bm.vb = vb;

            /* look ahead for avg floater */
            if(bm.avg_bitsper>0)
            {
                double slew = 0.0;
                int avg_target_bits = (vb.W > 0 ? bm.avg_bitsper * bm.short_per_long : bm.avg_bitsper);
                double slewlimit= 15.0 / bi.slew_damp;

                /* choosing a new floater:
                 if we're over target, we slew down
                 if we're under target, we slew up

                 choose slew as follows: look through packetblobs of this frame and set slew as the first in the appropriate direction that
                 gives us the slew we want.  This may mean no slew if delta is already favorable.

                 Then limit slew to slew max */
                
                if (bm.avg_reservoir + (this_bits - avg_target_bits) > desired_fill)
                {
                    while (choice > 0 && this_bits > avg_target_bits && bm.avg_reservoir + (this_bits - avg_target_bits) > desired_fill)
                    {
                        choice--;
                        this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
                    }
                }
                else if (bm.avg_reservoir + (this_bits - avg_target_bits) < desired_fill)
                {
                    while (choice + 1 < PACKETBLOBS && this_bits < avg_target_bits && bm.avg_reservoir + (this_bits - avg_target_bits) < desired_fill)
                    {
                        choice++;
                        this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
                    }
                }
                
                slew = rint(choice - bm.avgfloat) / samples * vi.rate;
                
                if (slew < -slewlimit) {
                    slew -= slewlimit;
                }
                
                if (slew > slewlimit) {
                    slew = slewlimit;
                }
                
                bm.avgfloat += slew / vi.rate * samples;
                choice = rint(bm.avgfloat);
                this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
            }

            /* enforce min(if used) on the current floater (if used) */
            if (bm.min_bitsper > 0)
            {
                /* do we need to force the bitrate up? */
                if (this_bits < min_target_bits)
                {
                    while (bm.minmax_reservoir - (min_target_bits - this_bits) < 0)
                    {
                        choice++;
                        
                        if (choice >= PACKETBLOBS) {
                            break;
                        }
                        
                        this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
                    }
                }
            }
            
            /* Choice of packetblobs now made based on floater, and min/max requirements. Now boundary check extreme choices */
            if (choice < 0)
            {
                /* choosing a smaller packetblob is insufficient to trim bitrate. frame will need to be truncated */
                int maxsize = (max_target_bits + (bi.reservoir_bits * bm.minmax_reservoir)) / 8;
                
                choice = 0;
                bm.choice = choice;
                
                if (Ogg.oggpack_bytes(ref vbi.packetblob[choice]) > maxsize) 
                {
                    Ogg.oggpack_writetrunc(ref vbi.packetblob[choice], maxsize * 8);
                    this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
                }
            }
            else
            {
                int minsize = (min_target_bits - bm.minmax_reservoir + 7) / 8;
                
                if (choice >= PACKETBLOBS) {
                    choice = PACKETBLOBS - 1;
                }
                
                bm.choice = choice;
                
                /* prop up bitrate according to demand. pad this frame out with zeroes */
                minsize -= Ogg.oggpack_bytes(ref vbi.packetblob[choice]);
            
                while(minsize-- > 0) {
                    Ogg.oggpack_write(ref vbi.packetblob[choice], 0, 8);
                }
                
                this_bits = Ogg.oggpack_bytes(ref vbi.packetblob[choice]) * 8;
            }
            
            /* now we have the final packet and the final packet size.  Update statistics */
            /* min and max reservoir */
            if (bm.min_bitsper > 0 || bm.max_bitsper > 0)
            {
                if (max_target_bits > 0 && this_bits > max_target_bits) {
                    bm.minmax_reservoir += (this_bits - max_target_bits);
                }
                else if (min_target_bits > 0 && this_bits < min_target_bits) {
                    bm.minmax_reservoir += (this_bits - min_target_bits);
                }
                else
                {
                    /* inbetween; we want to take reservoir toward but not past desired_fill */
                    if (bm.minmax_reservoir > desired_fill)
                    {
                        if (max_target_bits > 0)
                        {
                            /* logical bulletproofing against initialization state */
                            bm.minmax_reservoir += (this_bits - max_target_bits);
                            
                            if (bm.minmax_reservoir < desired_fill) {
                                bm.minmax_reservoir = desired_fill;
                            }
                        }
                        else
                        {
                            bm.minmax_reservoir = desired_fill;
                        }
                    }
                    else
                    {
                        if (min_target_bits > 0)
                        { 
                            /* logical bulletproofing against initialization state */
                            bm.minmax_reservoir += (this_bits - min_target_bits);
                            
                            if (bm.minmax_reservoir > desired_fill) {
                                bm.minmax_reservoir = desired_fill;
                            }
                        }
                        else
                        {
                            bm.minmax_reservoir = desired_fill;
                        }
                    }
                }
            }
            
            /* avg reservoir */
            if (bm.avg_bitsper > 0)
            {
                int avg_target_bits = (vb.W != 0 ? bm.avg_bitsper * bm.short_per_long : bm.avg_bitsper);
                bm.avg_reservoir += this_bits - avg_target_bits;
            }

            return 0;
        }
        
        static public int vorbis_bitrate_flushpacket(ref vorbis_dsp_state vd, ref Ogg.ogg_packet op)
        {
            private_state b = vd.backend_state as private_state;
            bitrate_manager_state bm = b.bms;
            vorbis_block vb = bm.vb;
            
            int choice = PACKETBLOBS / 2;

            vorbis_block_internal vbi = vb._internal as vorbis_block_internal;

            if (vorbis_bitrate_managed(ref vb) != 0) {
                choice = bm.choice;
            }

            op.packet = Ogg.oggpack_get_buffer(ref vbi.packetblob[choice]);
            op.bytes = Ogg.oggpack_bytes(ref vbi.packetblob[choice]);
            op.b_o_s = 0;
            op.e_o_s = vb.eofflag;
            op.granulepos = vb.granulepos;
            op.packetno = vb.sequence; /* for sake of completeness */
  
            bm.vb = new vorbis_block();
            return 1;
        }
    }
}