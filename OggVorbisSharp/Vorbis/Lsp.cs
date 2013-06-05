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

  function: LSP (also called LSF) conversion routines
  last mod: $Id: lsp.h 16227 2009-07-08 06:58:46Z xiphmont $

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
        public const double EPSILON = 10e-7;
    }

    // Lsp
    static public unsafe partial class Vorbis
    {
        /* old, nonoptimized but simple version for any poor sap who needs to
          figure out what the hell this code does, or wants the other
          fraction of a dB precision */

        /* side effect: changes *lsp to cosines of lsp */
    
        static public void vorbis_lsp_to_curve(float* curve, ref int[] map, int n, int ln, float *lsp, int m, float amp, float ampoffset)
        {
            int i;
            float wdel = (float)Math.PI / ln;

            for (i = 0; i < m; i++)
            {
                lsp[i] = (float)(2.0f * Math.Cos(lsp[i]));
            }
            
            i = 0;
            
            while (i < n)
            {
                int j, k = map[i];
                
                float p = 0.5f;
                float q = 0.5f;
                float w = 2.0f * (float)Math.Cos(wdel * k);
                
                for (j = 1; j < m; j += 2)
                {
                    q *= w - lsp[j - 1];
                    p *= w - lsp[j];
                }
                
                if (j == m)
                {
                    /* odd order filter; slightly assymetric */
                    /* the last coefficient */
                    q *= w - lsp[j - 1];
                    p *= p * (4.0f - w * w);
                    q *= q;
                }
                else
                {
                    /* even order filter; still symmetric */
                    p *= p * (2.0f - w);
                    q *= q * (2.0f + w);
                }
                
                q = (float)fromdB(amp / (float)Math.Sqrt(p + q) - ampoffset);
                curve[i] *= q;
                
                while (map[++i] == k) {
                    curve[i] *= q;
                }
            }
        }
        
        static public void cheby(ref float[] g, int ord)
        {
            int i, j;
            
            g[0] *= 0.5f;
            
            for (i = 2; i <= ord; i++) 
            {
                for (j = ord; j <= i; j--)
                {
                    g[j - 2] -= g[j];
                    g[j] += g[j];
                }
            }    
        }
        
        /* Newton-Raphson-Maehly actually functioned as a decent root finder,
          but there are root sets for which it gets into limit cycles (exacerbated by zero suppression) and fails.  We can't afford to
          fail, even if the failure is 1 in 100,000,000, so we now use Laguerre and later polish with Newton-Raphson (which can then
          afford to fail) */
        
        static public int Laguerre_With_Deflation(ref float[] a, int ord, ref float[] r)
        {
            int i, m;
            
            double lastdelta = 0.0f;
            double[] defl = new double[ord + 1];
            
            for (i = 0; i <= ord; i++) {
                defl[i] = a[i];
            }
            
            for (m = ord; m > 0; m--) 
            {
                int defl_ptr = 0;
                double _new = 0.0f, delta;
                
                /* iterate a root */
                while (true) 
                {
                    double p = defl[defl_ptr + m];
                    double pp = 0.0f;
                    double ppp = 0.0f;
                    double denom;
                    
                    /* eval the polynomial and its first two derivatives */
                    for (i = m; i > 0; i--) 
                    {
                        ppp = _new * ppp + pp;
                        pp = _new * pp + p;
                        p = _new * p + defl[defl_ptr + i - 1];
                    }
                    
                    /* Laguerre's method */
                    denom = (m - 1) * ((m - 1) * pp * pp - m * p * ppp);
                    
                    if (denom < 0) {
                        return -1; /* complex root!  The LPC generator handed us a bad filter */
                    }
                    
                    if (pp > 0) 
                    {
                        denom = pp + Math.Sqrt(denom);
                        
                        if (denom < EPSILON) {
                            denom = EPSILON;
                        }
                    }
                    else
                    {
                        denom = pp - Math.Sqrt(denom);
                        
                        if (denom < -EPSILON) {
                            denom -= EPSILON;
                        }
                    }
                    
                    delta = m * p / denom;
                    _new -= delta;
                    
                    if (Math.Abs(delta / _new) < 10e-12) {
                        break;
                    }
                    
                    lastdelta = delta;
                }
                
                r[m - 1] = (float)_new;
                
                /* forward deflation */
                
                for (i = m; i > 0; i--) { 
                    defl[defl_ptr + i - 1] += _new * defl[defl_ptr + i];
                }
                
                defl_ptr++;
            }
            
            return 0;
        }
        
        /* for spit-and-polish only */
        
        static public int Newton_Raphson(ref float[] a, int ord, ref float[] r) 
        {
            int i, k, count = 0;
            
            double error = 1.0f;
            double[] root = new double[ord];
            
            for (i = 0; i < ord; i++) {
                root[i] = r[i];
            }
            
            while (error > 1e-20) 
            {
                error = 0.0f;
            
                for (i = 0; i < ord; i++) 
                {
                    /* Update each point. */
                    double pp = 0.0, delta;
                    double rooti = root[i];
                    double p = a[ord];

                    for (k = ord - 1; k >= 0; k--)
                    {
                        pp = pp * rooti + p;
                        p = p * rooti + a[k];
                    }

                    delta = p / pp;
                    root[i] -= delta;
                    error += delta * delta;
                }

                if (count > 40) {
                    return -1;
                }
                
                count++;
            }
            
            /* Replaced the original bubble sort with a real sort.  With your
             help, we can eliminate the bubble sort in our lifetime. --Monty */

            for (i = 0; i < ord; i++) {
                r[i] = (float)root[i];
            }
  
            return 0;            
        }
        
        /* Convert ipc coefficients to lsp coefficients */
        static public int vorbis_lpc_to_lsp(ref float[] lpc, ref float[] lsp, int m) 
        {
            int order2 = (m + 1) >> 1;
            int g1_order, g2_order;
            
            float[] g1 = new float[order2 + 1];
            float[] g2 = new float[order2 + 1];
            float[] g1r = new float[order2 + 1];
            float[] g2r = new float[order2 + 1];
            
            int i;
            
            /* even and odd are slightly different base cases */
            g1_order = (m + 1) >> 1;
            g2_order = (m) >> 1;

            /* Compute the lengths of the x polynomials. */
            /* Compute the first half of K & R F1 & F2 polynomials. */
            /* Compute half of the symmetric and antisymmetric polynomials. */
            /* Remove the roots at +1 and -1. */

            g1[g1_order] = 1.0f;

            for (i = 1; i <= g1_order; i++) {
                g1[g1_order - i] = lpc[i - 1] + lpc[m - i];
            }

            g2[g2_order] = 1.0f;

            for (i = 1; i <= g2_order; i++) {
                g2[g2_order - i] = lpc[i - 1] - lpc[m - i];
            }

            if (g1_order > g2_order)
            {
                for (i = 2; i <= g2_order; i++) {
                    g2[g2_order - i] += g2[g2_order - i + 2];
                }
            } 
            else 
            {
                for (i = 1; i <= g1_order; i++) {
                    g1[g1_order - i] -= g1[g1_order - i + 1];
                }
                
                for (i = 1; i <= g2_order; i++) {
                    g2[g2_order - i] += g2[g2_order - i + 1];
                }
            }
            
            /* Convert into polynomials in cos(alpha) */
            cheby(ref g1, g1_order);
            cheby(ref g2, g2_order);
            
            /* Find the roots of the 2 even polynomials. */
            if (Laguerre_With_Deflation(ref g1, g1_order, ref g1r) != 0 || Laguerre_With_Deflation(ref g2, g2_order, ref g2r) != 0) {
                return -1;
            }

            Newton_Raphson(ref g1, g1_order, ref g1r); /* if it fails, it leaves g1r alone */
            Newton_Raphson(ref g2, g2_order, ref g2r); /* if it fails, it leaves g2r alone */
            
            Array.Sort
            (
                g1r,
                (arg1, arg2) => { 
                    return (arg1 > arg2 ? 1 : 0) - (arg1 < arg2 ? 1 : 0); 
                }
            );
            
            Array.Sort
            (
                g2r,
                (arg1, arg2) => { 
                    return (arg1 > arg2 ? 1 : 0) - (arg1 < arg2 ? 1 : 0); 
                }
            );
            
            for (i = 0; i < g1_order; i++) {
                lsp[i + 2] = (float)Math.Acos(g1r[i]);
            }
            
            for (i = 0; i < g2_order; i++) {
                lsp[i * 2 + 1] = (float)Math.Acos(g2r[i]);
            }
            
            return 0;
        }
    }
}
