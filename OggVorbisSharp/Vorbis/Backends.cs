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

 function: libvorbis backend and mapping structures; needed for
           static mode headers
 last mod: $Id: backends.h 16962 2010-03-11 07:30:34Z xiphmont $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        const int VIF_POSIT = 63;
        const int VIF_CLASS = 16;
        const int VIF_PARTS = 31;
    }
    
    // Delegates
    static public unsafe partial class Vorbis
    {
        delegate void vorbis_func_floor_pack(vorbis_info_floor vif, ref Ogg.oggpack_buffer opb);
        delegate vorbis_info_floor vorbis_func_floor_unpack(ref vorbis_info vi, ref Ogg.oggpack_buffer opb);
        delegate vorbis_look_floor vorbis_func_floor_look(ref vorbis_dsp_state vd, vorbis_info_floor vif);
        delegate void vorbis_func_floor_free_info(ref vorbis_info_floor vif);
        delegate void vorbis_func_floor_free_look(ref vorbis_look_floor vif);
        delegate void *vorbis_func_floor_inverse1(ref vorbis_block vb, vorbis_look_floor vif);
        delegate int vorbis_func_floor_inverse2(ref vorbis_block vb, vorbis_look_floor vif, void *v1, float *v2);
        
        delegate void vorbis_func_residue_pack(vorbis_info_residue vir, ref Ogg.oggpack_buffer opb);
        delegate vorbis_info_residue vorbis_func_residue_unpack(ref vorbis_info vi, ref Ogg.oggpack_buffer opb);
        delegate vorbis_look_residue vorbis_func_residue_look(ref vorbis_dsp_state vd, vorbis_info_residue vir);
        delegate void vorbis_func_residue_free_info(ref vorbis_info_residue vir);
        delegate void vorbis_func_residue_free_look(ref vorbis_look_residue vlr);
        delegate int **vorbis_func_residue_class(ref vorbis_block vb, vorbis_look_residue vlr, int **v1, int *v2, int v3);
        delegate int vorbis_func_residue_forward(ref Ogg.oggpack_buffer opb, ref vorbis_block vb, vorbis_look_residue vlr, int **v1, int *v2, int v3, int **v4, int v5);
        delegate int vorbis_func_residue_inverse(ref vorbis_block vb, vorbis_look_residue vlr, float **v1, int *v2, int v3);
        
        delegate void vorbis_func_mapping_pack(ref vorbis_info vi, vorbis_info_mapping vim, ref Ogg.oggpack_buffer opb);
        delegate vorbis_info_mapping vorbis_func_mapping_unpack(ref vorbis_info vi, ref Ogg.oggpack_buffer opb);
        delegate void vorbis_func_mapping_free_info(ref vorbis_info_mapping vb);
        delegate int vorbis_func_mapping_forward(ref vorbis_block vb);
        delegate int vorbis_func_mapping_inverse(ref vorbis_block vb, vorbis_info_mapping vim);
    }
    
    // Types
    static public unsafe partial class Vorbis
    {
        class vorbis_func_floor
        {
            public vorbis_func_floor_pack pack;
            public vorbis_func_floor_unpack unpack;
            public vorbis_func_floor_look look;
            public vorbis_func_floor_free_info free_info;
            public vorbis_func_floor_free_look free_look;
            public vorbis_func_floor_inverse1 inverse1;
            public vorbis_func_floor_inverse2 inverse2;
        }
        
        class vorbis_info_floor0 : vorbis_info_floor
        {
            public int order;
            public int rate;
            public int barkmap;
            
            public int ampbits;
            public int ampdB;
            
            public int numbooks; /* <= 16 */
            public int[] books = new int[16];
            
            public float lessthan; /* encode-only config setting hacks for libvorbis */
            public float greaterthan;  /* encode-only config setting hacks for libvorbis */
        }
        
        class vorbis_info_floor1 : vorbis_info_floor
        {
            public int partitions; /* 0 to 31 */
            public int[] partitionclass = new int[VIF_PARTS]; /* 0 to 15 */

            public int[] class_dim = new int[VIF_CLASS]; /* 1 to 8 */
            public int[] class_subs = new int[VIF_CLASS]; /* 0,1,2,3 (bits: 1<<n poss) */
            public int[] class_book = new int[VIF_CLASS]; /* subs ^ dim entries */
            public int[,] class_subbook = new int[VIF_CLASS, 8]; /* [VIF_CLASS][subs] */

            public int mult; /* 1 2 3 or 4 */
            public int[] postlist = new int[VIF_POSIT + 2]; /* first two implicit */

            /* encode side analysis parameters */
            public float maxover;
            public float maxunder;
            public float maxerr;

            public float twofitweight;
            public float twofitatten;

            public int n;
        }
        
        /* Residue backend generic */
        class vorbis_func_residue
        {
            public vorbis_func_residue_pack pack;
            public vorbis_func_residue_unpack unpack;
            public vorbis_func_residue_look look;
            public vorbis_func_residue_free_info free_info;
            public vorbis_func_residue_free_look free_look;
            public vorbis_func_residue_class _class;
            public vorbis_func_residue_forward forward;
            public vorbis_func_residue_inverse inverse;
        }
        
        class vorbis_info_residue0 : vorbis_info_residue
        {
            /* block-partitioned VQ coded straight residue */
            public int begin;
            public int end;

            /* first stage (lossless partitioning) */
            public int grouping; /* group n vectors per partition */
            public int partitions; /* possible codebooks for a partition */
            public int partvals; /* partitions ^ groupbook dim */
            public int groupbook; /* huffbook for partitioning */
            public int[] secondstages = new int[64]; /* expanded out to pointers in lookup */
            public int[] booklist= new int[512];    /* list of second stage books */

            public int[] classmetric1 = new int[64];
            public int[] classmetric2 = new int[64];            
        }
        
        /* Mapping backend generic */
        class vorbis_func_mapping
        {
            public vorbis_func_mapping_pack pack;
            public vorbis_func_mapping_unpack unpack;
            public vorbis_func_mapping_free_info free_info;
            public vorbis_func_mapping_forward forward;
            public vorbis_func_mapping_inverse inverse;
        }
        
        class vorbis_info_mapping0 : vorbis_info_mapping 
        {
            public int submaps; /* <= 16 */
            public int[] chmuxlist = new int[256]; /* up to 256 channels in a Vorbis stream */

            public int[] floorsubmap = new int[16];   /* [mux] submap to floors */
            public int[] residuesubmap = new int[16]; /* [mux] submap to residue */

            public int coupling_steps;
            public int[] coupling_mag = new int[256];
            public int[] coupling_ang = new int[256];
        }
    }
}