using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OggVorbisSharp
{
    public class OggDecompressor
    {
        public enum Status
        {
            Idle,
            Error,
            GettingFirstHeader,
            GettingSecondaryHeader,
            FinishedGettingHeader,
            GettingBlock,
            SendingResult
        }
        
        public enum Error
        {
            NoError,
            NotOggVorbisFile,
            VersionMismatch,
            NotContainAudioData,
            HeaderCorrupted,
            DataCorrupted
        };

        private Ogg.ogg_sync_state oggSyncState = new Ogg.ogg_sync_state();
        private Ogg.ogg_stream_state oggStreamState = new Ogg.ogg_stream_state();
        private Ogg.ogg_page oggPage = new Ogg.ogg_page();
        private Ogg.ogg_packet oggPacket = new Ogg.ogg_packet();

        private Vorbis.vorbis_info vorbisInfo = new Vorbis.vorbis_info();
        private Vorbis.vorbis_comment vorbisComment = new Vorbis.vorbis_comment();
        private Vorbis.vorbis_dsp_state vorbisDspState = new Vorbis.vorbis_dsp_state();
        private Vorbis.vorbis_block vorbisBlock = new Vorbis.vorbis_block();

        private Status status;
        private Error lastError;

        public int SampleRate
        {
            get
            {
                return this.vorbisInfo.rate;
            }
        }
        
        public int Channels
        {
            get
            {
                return this.vorbisInfo.channels;
            }
        }
        
        public Status CurrentStatus
        {
            get
            {
                return status;
            }
        }
        
        public Error LastError
        {
            get
            {
                return lastError;
            }
        }
        
        public OggDecompressor()
        {
            status = Status.Idle;
            lastError = Error.NoError;
        }
        
        public bool Decode(byte[] buffer, ref byte[] resultBuffer)
        {
            if (status == Status.Error)
            {
                return false;
            }
            
            IntPtr syncBuffer = GetSyncBuffer(buffer.Length);
            
            WriteSyncBuffer
            (
                buffer, 
                syncBuffer
            );
            
            switch(status)
            {
                case Status.Idle:
                {
                    status = Status.GettingFirstHeader;
                }
                goto case Status.GettingFirstHeader;
                
                case Status.GettingFirstHeader:
                {
                    if (GetFirstHeader() == false && status == Status.Error)
                    {
                        return false;
                    }
                    
                    if (status == Status.GettingSecondaryHeader)
                    {
                        goto case Status.GettingSecondaryHeader;
                    }
                }
                break;
                
                case Status.GettingSecondaryHeader:
                {
                    if (GetSecondaryHeader() == false && status == Status.Error) 
                    {
                        return false;
                    }
                    
                    if (status == Status.FinishedGettingHeader)
                    {
                        goto case Status.FinishedGettingHeader;
                    }
                }
                break;
                
                case Status.FinishedGettingHeader:
                {
		            // OK, got and parsed all there headers. initialize the vorbis packet -> PCM decoder.
		            if (VorbisSynthesisInitialize() == 0) 
		            {
		                try
		                {
	                        VorbisBlockInitialize();
	                    }
	                    finally
	                    {
	                        status = Status.GettingBlock;
	                    }
	                }
	                else
	                {
                        SetError(Error.HeaderCorrupted);
                        return false;
	                }
	                        
	                if (status == Status.GettingBlock)
	                {
	                    goto case Status.GettingBlock;
	                }
                }
                break;
                
                case Status.GettingBlock:
                {
                    Boolean isSuccess = GetBlock
                    (
                        syncBuffer,
                        buffer.Length,
                        ref resultBuffer
                    );
                    
                    if (isSuccess == false)
                    {
                        SetError(Error.DataCorrupted);
                        return false;
                    }
                }
                break;
            }
        
            return true;
        }
        
        private void SetError(Error error)
        {
            status = Status.Error;
            lastError = error;
        }
        
        public void Finish()
        {
            try
            {
                // ogg_page and ogg_packet structs always point to storage in libvorbis.
	            // They're never freed or manipulated directly
                VorbisBlockClear();
                VorbisDspClear();
                VorbisCommentClear();
                VorbisInfoClear();
	        }
	        finally
	        {
	            this.status = Status.Idle; 
	        } 
        }
        
        private IntPtr GetSyncBuffer(int syncBufferSize)
        {
            return Ogg.ogg_sync_buffer
            (
                ref oggSyncState,
                syncBufferSize
            );
        }
        
        private int WriteSyncBuffer(Byte[] bufferFrom, IntPtr syncBuffer)
        {
            Marshal.Copy
            (
                bufferFrom,
                0,
                syncBuffer,
                bufferFrom.Length 
            );
        
            return Ogg.ogg_sync_wrote
            (
                ref oggSyncState,
                bufferFrom.Length
            );
        }
        
        private int OggSyncInitialize()
        {
            return Ogg.ogg_sync_init
            (
                ref oggSyncState
            );
        }
        
        private int OggStreamInitialize()
        {
            return Ogg.ogg_stream_init
            (
                ref oggStreamState,
                Ogg.ogg_page_serialno(ref oggPage)
            );
        }
        
        private void VorbisInfoInitialize()
        {
            Vorbis.vorbis_info_init(ref vorbisInfo);
        }
        
        private void VorbisCommentInitialize()
        {
            Vorbis.vorbis_comment_init(ref vorbisComment);
        }
        
        private int VorbisBlockInitialize()
        {
            return Vorbis.vorbis_block_init
            (
                ref vorbisDspState,
                ref vorbisBlock
            );
        }
        
        private int VorbisSynthesisInitialize()
        {
            return Vorbis.vorbis_synthesis_init
            (
                ref vorbisDspState,
                ref vorbisInfo
            );
        }
        
        private int OggStreamClear()
        {
            return Ogg.ogg_stream_clear
            (
                ref oggStreamState
            );
        }
        
        private int OggSyncClear()
        {
            return Ogg.ogg_sync_clear
            (
                ref oggSyncState
            );
        }
        
        private int VorbisBlockClear()
        {
            return Vorbis.vorbis_block_clear
            (
                ref vorbisBlock
            );
        }
        
        private void VorbisDspClear()
        {
            Vorbis.vorbis_dsp_clear
            (
                ref vorbisDspState
            );
        }
        
        private void VorbisCommentClear()
        {
            Vorbis.vorbis_comment_clear
            (
                ref vorbisComment
            );
        }
        
        private void VorbisInfoClear()
        {
            Vorbis.vorbis_info_clear
            (
                ref vorbisInfo
            );
        }
        
        private int VorbisSynthesis()
        {
            return Vorbis.vorbis_synthesis
            (
                ref vorbisBlock,
                ref oggPacket
            );
        }
        
        private int VorbisSynthesisRead(int samples)
        {
            return Vorbis.vorbis_synthesis_read
            (
                ref vorbisDspState,
                samples
            );
        }
        
        private int PageIn()
        {
            return Ogg.ogg_stream_pagein
            (
                ref oggStreamState,
                ref oggPage
            );        
        }
        
        private int PageOut()
        {
            return Ogg.ogg_sync_pageout
            (
                ref oggSyncState,
                ref oggPage
            );
        }
        
        private long PageSeek()
        {
            return Ogg.ogg_sync_pageseek
            (
                ref oggSyncState,
                ref oggPage
            );
        }
        
        private int PageEOS()
        {
            return Ogg.ogg_page_eos 
            (
                ref oggPage
            );
        }
        
        private int PacketIn()
        {
            return Ogg.ogg_stream_packetin
            (
                ref oggStreamState,
                ref oggPacket 
            );
        }
        
        private int PacketOut()
        {
            return Ogg.ogg_stream_packetout
            (
                ref oggStreamState,
                ref oggPacket 
            );        
        }
        
        private int HeaderIn()
        {
            return Vorbis.vorbis_synthesis_headerin
            (
                ref vorbisInfo,
                ref vorbisComment,
                ref oggPacket
            );
        }
        
        private int BlockIn()
        {
            return Vorbis.vorbis_synthesis_blockin
            (
                ref vorbisDspState,
                ref vorbisBlock
            );
        }
        
        private int BlockOut()
        {
            throw new NotImplementedException();
        }
         
        private long BlockSize()
        {
            return Vorbis.vorbis_packet_blocksize
            (
                ref vorbisInfo,
                ref oggPacket
            );
        }
        
        private int PcmIn()
        {
            throw new NotImplementedException();
        }
        
        private int PcmOut(ref IntPtr pcm)
        {   
            return Vorbis.vorbis_synthesis_pcmout
            (
                ref vorbisDspState,
                ref pcm
            );
        }
        
        private Boolean GetFirstHeader()
        {
	        // grab some data at the head of the stream. We want the first page
            // (which is guaranteed to be small and only contain the Vorbis
            // stream initial header) We need the first page to get the stream
            // serialno. 
            
            // get the first page
            if (PageOut() != 1)
            {
                // error case, must not be vorbis data.
                SetError(Error.NotOggVorbisFile);
                return false;
            }
            
            // get the serial number and set up the rest of decode.
	        // serialno first; use it to set up a local stream 
	        OggStreamInitialize();
	        
        	// extract the initial header from the first page and verify that the Ogg bitstream is in face Vorbis data
	        // I handle the initial header first instead of just having the code read all three vorbis headers at once
	        // because reading the initial header is an easy way to indentify a vorbis bitstream and it's useful to see
	        // that functionality seperated out. 
	        VorbisInfoInitialize();
	        VorbisCommentInitialize();
	        
	        if (PageIn() < 0)
	        {
		        // error; stream version mismatch perhaps 
		        SetError(Error.VersionMismatch);
		        return false;
	        }

	        if (PacketOut() != 1)
	        {
		        // no page? must not be vorbis
		        SetError(Error.NotOggVorbisFile);
		        return false;
	        }

	        if (HeaderIn() < 0)
	        {
		        // not exists vorbis header
		        SetError(Error.NotContainAudioData);
		        return false;
	        }
	
	        // At this point, we're sute that this's vorbis. we've set up the logical (Ogg) bitstream decoder.
	        // get the comment and codebook headers and set up vorbis decoder 
	        this.status = Status.GettingSecondaryHeader;
	        return true;
        }
        
        private Boolean GetSecondaryHeader()
        {
            // the next two packets in order are the comment and codebook headers. they're likely large and may
	        // span multiple pages. thus we read and submit data until we get our two packets, watching that no 
	        // pages are missing. if a page is missing, error out; losing a header page is the only place were
	        // missing data is fatal.
	        int progress = 0;
	        int result = 0 ;
	        
	        while (progress < 2)
	        {
	            result = PageOut();
	            
	            if (result == 0) // need more data.
	            {
	                break;
	            }
	            
	            // Don't complain about missing or corrupt data yet. 
		        // we'll catch it at the packet output phase
		        if (result == 1)
		        {
		            PageIn();
		            
		            while (progress < 2)
		            {
		                result = PacketOut();
		                
		                if (result == 0) // need more data.
		                {
		                    break;
		                }
		                
		                if (result < 0)
		                {
                            // Uh oh; data at some point was corrupted or missing!
					        // We can't tolerate that in a header. Die! 
					        SetError(Error.HeaderCorrupted);
					        return false;
					    }
					    
					    result = HeaderIn();
					    
					    if (result < 0)
					    {
					        SetError(Error.HeaderCorrupted);
					        return false;
					    }
					    else
					    {
					        progress++;
					    }
					}
				}
			}
			
			if (progress == 2)
			{
			    status = Status.FinishedGettingHeader;
			    return true;
			}
			else
			{
			    return false;
			}
		}
		
		private bool GetBlock(IntPtr buffer, int bufferLength, ref Byte[] resultBuffer)
		{
        	MemoryStream resultBufferStream = new MemoryStream();
        
            try
            {
	            // The rest is just a straight decode loop until end of stream.
	            int result;
	            int eos = 0;
    	        
                while (eos == 0) 
	            {
 	                result = PageOut();
    	            
	                if (result == 0) // Need more data.
	                {
	                    break;
	                }

 	                if (result < 0) // Missing or corrupt data at this page position
	                {
	                    SetError(Error.DataCorrupted);
	                    return false;
	                }
	                else
	                {
	                    PageIn(); // Can safely ignore errors at this point.
    	                
	                    while (true)
	                    {
                            result = PacketOut();
    	                    
	                        if (result == 0) 
	                        {
	                            break;
	                        }
    	                    
	                        if (result < 0) 
	                        {
	                            SetError(Error.DataCorrupted);
	                            return false;
	                        }
	                        else
	                        {
	                            // We have a packet. Decode it!
	                            IntPtr pcm = IntPtr.Zero;
                                
                                int samples;
                                int channels = this.Channels;
                                int conversionSize = bufferLength / channels;
                                
                                if (VorbisSynthesis() == 0) 
                                {
                                    BlockIn(); // Test for success!
                                }
                                
                                while ((samples = PcmOut(ref pcm)) > 0)
                                {
                                    Byte[] resultBlock = null;
                                    
                                    int outputLength = (samples < conversionSize) ? samples : conversionSize;
                                    
                                    status = Status.SendingResult;
                                    
                                    Interleave
                                    (
                                        pcm,
                                        channels,
                                        outputLength,
                                        ref resultBlock
                                    );
                                 
                                    resultBufferStream.Write
                                    (
                                        resultBlock,
                                        0,
                                        resultBlock.Length
                                    );
                                    
                                    status = Status.GettingBlock;

		                            // Tell libvorbis how many samples we actually consumed.
                                    VorbisSynthesisRead (outputLength); 
                                }
                            }
                            
                            if (PageEOS() != 0) 
                            {
                                eos = 1;
                            }
                        }
	                }
	            }
	        }
            finally
            {
	            resultBuffer = resultBufferStream.ToArray();
                resultBufferStream.Dispose();
            }
	        
	        return true;
		}
		
		private void Interleave(IntPtr pcm, int channels, int sampleSize, ref Byte[] resultBlock)
		{
		    // lpppsz_pcm_buffer is a multichannel float vector. In stereo, for example, pcm[0] is left
	        // pcm[1] is right. Samples is the size of each channel. Convert the float values
	        // ( -1.0 <= range <= 1.0 ) to whatever PCM format and write it out 		
	        int blockSize = sizeof(short) * sampleSize * channels;
	        
	        IntPtr resultBlockPtr = Marshal.AllocHGlobal(blockSize);
	        
	        try
	        {
	            // Convert floats to 16 bit signed ints (host order) and interleave
	            for (int channel = 0; channel < channels; channel++)
	            {
	                IntPtr bufferPtr = Marshal.ReadIntPtr(pcm, IntPtr.Size * channel);
    	            
	                int resultBlockOffset = sizeof(short) * sampleSize * channel;
    	            
	                for (int position = 0; position < sampleSize; position++)
	                {
	                    Byte[] _value = new Byte[sizeof(float)];
    	                
	                    int bufferOffset = _value.Length * position;
    	            
	                    for (int index = 0; index < _value.Length; index++)
	                    {
                            _value[index] = Marshal.ReadByte(bufferPtr, bufferOffset + index);
	                    }
    	                
	                    int value = (int)Math.Floor(BitConverter.ToSingle(_value, 0) * 32767.0f + 0.5f);
    	                
	                    if (value > 32767) 
	                    {
	                        value = 32767;
	                    }
    	                
	                    if (value < -32768)
	                    {
	                        value = -32768;
	                    }
    	                
                        Marshal.WriteInt16
                        (
                            resultBlockPtr,
                            resultBlockOffset,
                            (short)value
                        );
    	                
    	                resultBlockOffset += (sizeof(short) * channels);
	                }
	            }
	            
	            resultBlock = new Byte[blockSize];
	            
                Marshal.Copy
                (
                    resultBlockPtr,
                    resultBlock,
                    0,
                    resultBlock.Length
                );
	        }
	        finally
	        {
	            Marshal.FreeHGlobal(resultBlockPtr);
	        }
		}
    }
}