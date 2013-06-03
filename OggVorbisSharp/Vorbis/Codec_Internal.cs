using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        static int BLOCKTYPE_IMPULSE = 0;
        static int BLOCKTYPE_PADDING = 1;
        static int BLOCKTYPE_TRANSITION = 2;
        static int BLOCKTYPE_LONG = 3;
        static int PACKETBLOBS = 15;
    }
    
    // Types
    static public unsafe partial class Vorbis
    {
        class vorbis_look_floor {}
        class vorbis_look_residue {}
        class vorbis_look_transform {}
        class vorbis_info_floor {}
        class vorbis_info_residue {}
        class vorbis_info_mapping {}

        class vorbis_block_internal 
        {
            public float **pcmdelay; 
            public float ampmax;
            public int blocktype;
            
            public Ogg.oggpack_buffer[] packetblob = new Ogg.oggpack_buffer[PACKETBLOBS]; /* Initailized, must be freed; blob [PACKETBLOBS/2] points to the oggpack_buffer in the main vorbis_block */
        }
        
        class vorbis_info_mode
        {
            public int blockflag;
            public int windowtype;
            public int transformtype;
            public int mapping;
        }
        
        class private_state 
        {
            public envelope_lookup ve; // envelope_lookup *ve; /* envelop lookup */
            public int[] window = new int[2]; 
            public vorbis_look_transform[][] transform = new vorbis_look_transform[2][]; 
            public drft_lookup[] fft_look = new drft_lookup[2];
            
            public int modebits;
            public vorbis_look_floor[] flr; 
            public vorbis_look_residue[] residue; 
            public vorbis_look_psy[] psy;
            public vorbis_look_psy_global psy_g_look;
            
            /* local storage, only used on the encoding side.  This way the application does not need to worry about freeing some packets'
              memory and not others'; packet storage is always tracked. Cleared next call to a _dsp_ function */
            public IntPtr header; // byte *header;
            public IntPtr header1; // byte *header1;
            public IntPtr header2; // byte *header2;
            
            public bitrate_manager_state bms;

            public long sample_count;
        }
        
        /* codec_setup_info contains all the setup information specific to the specific compression/decompression mode in progress (eg,
          psychoacoustic settings, channel setup, options, codebook etc). */
        class codec_setup_info 
        {
            /* Vorbis supports only short and long blocks, but allows the encoder to choose the sizes */
            public int[] blocksizes = new int[2];
            
            /* modes are the primary means of supporting on-the-fly different blocksizes, different channel mappings (LR or M/A),
              different residue backends, etc.  Each mode consists of a blocksize flag and a mapping (along with the mapping setup */
            public int modes;
            public int maps;
            public int floors;
            public int residues;
            public int books;
            public int psys; /* encoded only */
            
            public vorbis_info_mode[] mode_param = new vorbis_info_mode[64]; 
            
            public int[] map_type = new int[64];
            public vorbis_info_mapping[] map_param = new vorbis_info_mapping[64];
            public int[] floor_type = new int[64];
            public vorbis_info_floor[] floor_param = new vorbis_info_floor[64];
            public int[] residue_type = new int[64];
            public vorbis_info_residue[] residue_param = new vorbis_info_residue[64]; 
            
            public static_codebook[] book_param = new static_codebook[256];
            public codebook[] fullbooks;

            public vorbis_info_psy[] psy_param = new vorbis_info_psy[4]; /* encode only */
            public vorbis_info_psy_global psy_g_param;

            public bitrate_manager_info bi;
            public highlevel_encode_setup hi; /* used only by vorbisenc.c.  It's ahighly redundant structure, but improves clarity of program flow. */
            
            public int halfrate_flag; /* painless downsample for decode */             
        }     
        
        class vorbis_look_floor1 : vorbis_look_floor
        {
            public int[] sorted_index = new int[VIF_POSIT+2];
            public int[] forward_index = new int[VIF_POSIT+2];
            public int[] reverse_index = new int[VIF_POSIT+2];

            public int[] hineighbor = new int[VIF_POSIT];
            public int[] loneighbor = new int[VIF_POSIT];
            public int posts;

            public int n;
            public int quant_q;
            public vorbis_info_floor1 vi;

            public int phrasebits;
            public int postbits;
            public int frames;
        }            
    }
}
