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

 function: linear scale -> dB, Bark and Mel scales
 last mod: $Id: scales.h 16227 2009-07-08 06:58:46Z xiphmont $

 ********************************************************************/
 
/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */ 

using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    static public unsafe partial class Vorbis
    {
        /* 20log(10(x) */
        
        static float unitnorm(float x) 
        {
            byte[] _x;
            uint i;
            
            _x = BitConverter.GetBytes(x);
            
            i =  BitConverter.ToUInt32(_x, 0);
            i = (i & 0x80000000) | 0x3f800000;
            
            _x = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(_x, 0);
        }
    
        /* The bark scale equations are approximations, since the original
          table was somewhat hand rolled.  The below are chosen to have the
          best possible fit to the rolled tables, thus their somewhat odd
          appearance (these are more accurate and over a longer range than
          the oft-quoted bark equations found in the texts I have).  The
          approximations are valid from 0 - 30kHz (nyquist) or so.
          all f in Hz, z in Bark */

        static float toBARK(float n)
        {
            return (float)(13.1f * Math.Atan(.00074f * n) + 2.24f * Math.Atan(n * n * 1.85e-8f) + 1e-4f * n);
        }
        
        static float fromBARK(float z)
        {
            return (float)(102.0f * z - 2.0f * Math.Pow(z, 2.0f) + 0.4f * Math.Pow(z ,3.0f) + Math.Pow(1.46f , z) - 1.0f);
        }
        
        static float toMEL(float n)
        {
            return (float)(Math.Log(1.0f + n * .001f) * 1442.695f);
        }
        
        static float fromMEL(float m)
        {
            return (float)(1000.0f * Math.Exp(m / 1442.695f) - 1000.0f);
        }
        
        /* Frequency to octave.  We arbitrarily declare 63.5 Hz to be octave 0.0 */
        
        static float toOC(float n)
        {
            return (float)(Math.Log(n) * 1.442695f - 5.965784f);
        }
                
        static float fromOC(float o)
        {
            return (float)(Math.Exp((o + 5.965784f) * 0.693147f));
        }
        
        /* dB */
        
        static float todB(float x)
        {
            return (float)(x * 7.17711438e-7f - 764.6161886f);
        }
        
        static float fromdB(float x)
        {
            return (float)(Math.Exp(x * 0.11512925f));
        }
    }
}
