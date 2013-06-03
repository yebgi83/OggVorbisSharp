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

 function: registry for time, floor, res backends and channel mappings
 last mod: $Id: registry.h 15531 2008-11-24 23:50:06Z xiphmont $

 ********************************************************************/

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 
 
using System;
using System.Collections.Generic;
using System.Text;

/* seems like major overkill now; the backend numbers will grow into the infrastructure soon enough */

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        const int VI_TRANSFORMB = 1;
        const int VI_WINDOWB = 1;
        const int VI_TIMEB = 1;
        const int VI_FLOORB = 2;
        const int VI_RESB = 3;
        const int VI_MAPB = 1;
    }
    
    // Registry
    static public unsafe partial class Vorbis
    {
        static readonly vorbis_func_floor[] _floor_P;
        static readonly vorbis_func_residue[] _residue_P;
        static readonly vorbis_func_mapping[] _mapping_P;
        
        static Vorbis()
        {
            _floor_P = new vorbis_func_floor[]
            {
                floor0_exportbundle,
                floor1_exportbundle
            };
            
            _residue_P = new vorbis_func_residue[]
            {
                residue0_exportbundle,
                residue1_exportbundle,
                residue2_exportbundle
            };
        
            _mapping_P = new vorbis_func_mapping[]
            {
                mapping0_exportbundle
            };            
        }
    }
}
