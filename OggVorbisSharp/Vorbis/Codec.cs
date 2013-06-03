using System;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;

namespace OggVorbisSharp
{
    // Constants
    static public unsafe partial class Vorbis
    {
        public const int OV_FALSE = -1;
        public const int OV_EOF = -2;
        public const int OV_HOLE = -3;

        public const int OV_EREAD = -128;
        public const int OV_EFAULT = -129;
        public const int OV_EIMPL = -130;
        public const int OV_EINVAL = -131;
        public const int OV_ENOTVORBIS = -132;
        public const int OV_EBADHEADER = -133;
        public const int OV_EVERSION = -134;
        public const int OV_ENOTAUDIO = -135;
        public const int OV_EBADPACKET = -136;
        public const int OV_EBADLINK =  -137;
        public const int OV_ENOSEEK = -138; 
    }
    
    // Codec
    static public unsafe partial class Vorbis
    {
        public class vorbis_info
        {
            public int version;
            public int channels;
            public int rate;
            
            /* The below bitrate declarations are *hints*.
              Combinations of the three values carry the following implications:

              all three set to the same value:
                implies a fixed rate bitstream
              only nominal set:
                implies a VBR stream that averages the nominal bitrate.  No hard
                upper/lower limit
              upper and or lower set:
                implies a VBR bitstream that obeys the bitrate limits. nominal
                may also be set to give a nominal rate.
              none set:
                the coder does not care to speculate. */

            public int bitrate_upper;
            public int bitrate_nominal;
            public int bitrate_lower;
            public int bitrate_window;

            public object codec_setup;
        }
        
        /* vorbis_dsp_state buffers the current vorbis audio analysis/synthesis state.  
          The DSP state belongs to a specific logical bitstream */
        public class vorbis_dsp_state
        {
            public int analysisp;
            
            public vorbis_info vi;

            public float **pcm;
            public float **pcmret;
            public int pcm_storage;
            public int pcm_current;
            public int pcm_returned;

            public int preextrapolate;
            public int eofflag;

            public int lW;
            public int W;
            public int nW;
            public int centerW;

            public long granulepos;
            public long sequence;

            public long glue_bits;
            public long time_bits;
            public long floor_bits;
            public long res_bits;

            public object backend_state; 
        }
        
        public class vorbis_block
        {
            /* necessary stream state for linking to the framing abstraction */
            public float **pcm; 
            public Ogg.oggpack_buffer opb;

            public int lW;
            public int W;
            public int nW;
            public int pcmend;
            public int mode;
            
            public int eofflag;
            public long granulepos;
            public long sequence;
            public vorbis_dsp_state vd; /* For read-only access of configuration */

            /* local storage to avoid remallocing; it's up to the mapping to structure it */
            public void *localstore;
            public int localtop;
            public int localalloc;
            public int totaluse;
            public alloc_chain reap;

            /* bitmetrics for the frame */
            public int glue_bits;
            public int time_bits;
            public int floor_bits;
            public int res_bits;

            public object _internal;
        }
        
        /* vorbis_block is a single block of data to be processed as part of the analysis/synthesis stream; it belongs to a specific logical
          bitstream, but is independent from other vorbis_blocks belonging to that logical bitstream. */
        
        public class alloc_chain
        {
            public void *ptr;
            public alloc_chain next;
        };
        
        /* vorbis_info contains all the setup information specific to the specific compression/decompression mode in progress (eg, psychoacoustic settings, channel setup, options, codebook
          etc). vorbis_info and substructures are in backends.h. */

        public class vorbis_comment
        {
            /* unlimited user comment fields.  libvorbis writes 'libvorbis' whatever vendor is set to in encode */
            public char[][] user_comments; 
            public int[] comment_lengths; 
            public int comments;
            public char[] vendor; 
        }
    }
}
