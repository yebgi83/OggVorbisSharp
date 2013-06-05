using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using OggVorbisSharp;

namespace OggVorbisSharpTest
{
    static public unsafe class VorbisFileTest
    {
        static public void Test()
        {
            // OldTest();
            NewTest();
        }

        static public void NewTest()
        {
            VorbisFile.OggVorbis_File vf = new VorbisFile.OggVorbis_File();
            
            Console.WriteLine(VorbisFile.ov_fopen("stop.ogg", ref vf));
            
            Vorbis.vorbis_info vi = VorbisFile.ov_info(ref vf, -1);

            int buffersize = 44100;
            byte *buffer = stackalloc byte [buffersize];

            FileStream stream = File.Create
            (
                "C:\\" + Guid.NewGuid().ToString() + ".wav"
            );
            
            // while (true)
            try
            {
                int bitstream = 0;
                int ret = 0;
                
                while(true)
                {
                    ret = VorbisFile.ov_read(ref vf, buffer, buffersize, 0, 2, 1, ref bitstream);
                    
                    if (ret <= 0) 
                    {
                        return;
                    }
                    
                    Console.WriteLine(ret);
                    
                    byte[] buffer_managed = new byte [ret];
                    Marshal.Copy((IntPtr)buffer, buffer_managed, 0, buffer_managed.Length);
                    
                    stream.Write(buffer_managed, 0, buffer_managed.Length);
                }
            }
            finally
            {
                stream.Close();
                stream.Dispose();
            }
        }
        
        static public void OldTest()
        {
            OggVorbisDecoder decompressor = new OggVorbisDecoder();
            
            FileStream inputStream = File.OpenRead("intro.ogg");
            FileStream outputStream = File.Create("C:\\" + Guid.NewGuid().ToString() + ".raw");
            
            while(inputStream.Position < inputStream.Length)
            {
                byte[] buffer = new byte [4096];
                byte[] resultBuffer = null;
                
                inputStream.Read(buffer, 0, buffer.Length);
                
                if (decompressor.Decode(buffer, ref resultBuffer) == true)
                {
                    outputStream.Write(resultBuffer, 0, resultBuffer.Length);
                }
            }
        }
    }
}
