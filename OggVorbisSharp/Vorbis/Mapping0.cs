using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharp
{
    static public unsafe partial class Vorbis
    {
        /* simplistic, wasteful way of doing this (unique lookup for each mode/submapping); there should be a central repository for
          identical lookups.  That will require minor work, so I'm putting it off as low priority.

          Why a lookup for each backend in a given mode?  Because the blocksize is set by the mode, and low backend lookups may require
          parameters from other areas of the mode/mapping */

        static void mapping0_free_info(ref vorbis_info_mapping i)
        {
            vorbis_info_mapping0 info = i as vorbis_info_mapping0;
            
            if (info != null) {
                info = null;
            }
        }

        static void mapping0_pack(ref vorbis_info vi, vorbis_info_mapping vm, ref Ogg.oggpack_buffer opb)
        {
            int i;
            vorbis_info_mapping0 info = vm as vorbis_info_mapping0;

            /* another 'we meant to do it this way' hack...  up to beta 4, we packed 4 binary zeros here to signify one submapping in use.  We
             now redefine that to mean four bitflags that indicate use of deeper features; bit0:submappings, bit1:coupling,
             bit2,3: reserved. This is backward compatable with all actual uses of the beta code. */

            if (info.submaps > 1)
            {
                Ogg.oggpack_write(ref opb, 1, 1);
                Ogg.oggpack_write(ref opb, (uint)(info.submaps - 1), 4);
            }
            else
            {
                Ogg.oggpack_write(ref opb, 0, 1);
            }

            if (info.coupling_steps > 0)
            {
                Ogg.oggpack_write(ref opb, 1, 1);
                Ogg.oggpack_write(ref opb, (uint)(info.coupling_steps - 1), 8);

                for (i = 0; i < info.coupling_steps; i++)
                {
                    Ogg.oggpack_write(ref opb, (uint)info.coupling_mag[i], ilog((uint)vi.channels));
                    Ogg.oggpack_write(ref opb, (uint)info.coupling_ang[i], ilog((uint)vi.channels));
                }
            }
            else
            {
                Ogg.oggpack_write(ref opb, 0, 1);
            }

            Ogg.oggpack_write(ref opb, 0, 2); /* 2,3:reserved */

            /* we don't write the channel submappings if we only have one... */
            if (info.submaps > 1)
            {
                for (i = 0; i < vi.channels; i++)
                {
                    Ogg.oggpack_write(ref opb, (uint)info.chmuxlist[i], 4);
                }
            }

            for (i = 0; i < info.submaps; i++)
            {
                Ogg.oggpack_write(ref opb, 0, 8); /* time submap unused */
                Ogg.oggpack_write(ref opb, (uint)info.floorsubmap[i], 8);
                Ogg.oggpack_write(ref opb, (uint)info.residuesubmap[i], 8);
            }
        }

        /* also responsible for range checking */
        static vorbis_info_mapping mapping0_unpack(ref vorbis_info vi, ref Ogg.oggpack_buffer opb)
        {
            int i, b;

            vorbis_info_mapping _info = new vorbis_info_mapping0();
            vorbis_info_mapping0 info = _info as vorbis_info_mapping0;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;

            b = Ogg.oggpack_read(ref opb, 1);

            if (b < 0)
            {
                goto err_out;
            }

            if (b != 0)
            {
                info.submaps = Ogg.oggpack_read(ref opb, 4) + 1;

                if (info.submaps <= 0)
                {
                    goto err_out;
                }
            }
            else
            {
                info.submaps = 1;
            }

            b = Ogg.oggpack_read(ref opb, 1);

            if (b < 0)
            {
                goto err_out;
            }

            if (b != 0)
            {
                info.coupling_steps = Ogg.oggpack_read(ref opb, 8) + 1;

                if (info.coupling_steps <= 0)
                {
                    goto err_out;
                }

                for (i = 0; i < info.coupling_steps; i++)
                {
                    int testM = info.coupling_mag[i] = Ogg.oggpack_read(ref opb, ilog((uint)vi.channels));
                    int testA = info.coupling_ang[i] = Ogg.oggpack_read(ref opb, ilog((uint)vi.channels));

                    if (testM < 0 || testA < 0 || testM == testA || testM >= vi.channels || testA >= vi.channels)
                    {
                        goto err_out;
                    }
                }

            }

            if (Ogg.oggpack_read(ref opb, 2) != 0) /* 2,3:reserved */
            {
                goto err_out;
            }

            if (info.submaps > 1)
            {
                for (i = 0; i < vi.channels; i++)
                {
                    info.chmuxlist[i] = Ogg.oggpack_read(ref opb, 4);

                    if (info.chmuxlist[i] >= info.submaps || info.chmuxlist[i] < 0)
                    {
                        goto err_out;
                    }
                }
            }

            for (i = 0; i < info.submaps; i++)
            {
                Ogg.oggpack_read(ref opb, 8); /* time submap unused */
                info.floorsubmap[i] = Ogg.oggpack_read(ref opb, 8);

                if (info.floorsubmap[i] >= ci.floors || info.floorsubmap[i] < 0)
                {
                    goto err_out;
                }

                info.residuesubmap[i] = Ogg.oggpack_read(ref opb, 8);

                if (info.residuesubmap[i] >= ci.residues || info.residuesubmap[i] < 0)
                {
                    goto err_out;
                }
            }

            return info;
        
        err_out:
            mapping0_free_info(ref _info);
            return null;
        }

        static int mapping0_forward(ref vorbis_block vb)
        {
            vorbis_dsp_state vd = vb.vd;
            vorbis_info vi = vd.vi;
            
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            private_state b = vb.vd.backend_state as private_state;
            vorbis_block_internal vbi = vb._internal as vorbis_block_internal;
            
            int n = vb.pcmend;
            int i, j, k;

            int* nonzero = stackalloc int[vi.channels];
            float** gmdct = (float**)_vorbis_block_alloc(ref vb, vi.channels * sizeof(float*));
            int** iwork = (int**)_vorbis_block_alloc(ref vb, vi.channels * sizeof(int*));
            int*** floor_posts = (int***)_vorbis_block_alloc(ref vb, vi.channels * sizeof(int**));

            float global_ampmax = vbi.ampmax;
            float* local_ampmax = stackalloc float[vi.channels];

            int blocktype = vbi.blocktype;
            int modenumber = vb.W;

            vorbis_info_mapping0 info = ci.map_param[modenumber] as vorbis_info_mapping0;
            vorbis_look_psy psy_look = b.psy[blocktype + (vb.W != 0 ? 2 : 0)];

            vb.mode = modenumber;

            for (i = 0; i < vi.channels; i++)
            {
                float scale = 4.0f / n;
                float scale_dB;

                float* pcm = vb.pcm[i];
                float* logfft = pcm;

                iwork[i] = (int*)_vorbis_block_alloc(ref vb, (n / 2) * sizeof(int));
                gmdct[i] = (float*)_vorbis_block_alloc(ref vb, (n / 2) * sizeof(float));

                /* + .345 is a hack; the original todB estimation used on IEEE 754 compliant machines had a bug that
                  returned dB values about a third of a decibel too high.  The bug was harmless because tunings
                  implicitly took that into account.  However, fixing the bug in the estimator requires changing all the tunings as well.
                  For now, it's easier to sync things back up here, and recalibrate the tunings in the next major model upgrade. */
                  
                scale_dB = todB(scale) + 0.345f;

                /* window the PCM data */
                _vorbis_apply_window(pcm, ref b.window, ref ci.blocksizes, vb.lW, vb.W, vb.nW);

                /* transform the PCM data */
                /* only MDCT right now.... */
                mdct_forward(b.transform[vb.W][0] as mdct_lookup, pcm, gmdct[i]);

                /* FFT yields more accurate tonal estimation (not phase sensitive) */
                drft_forward(ref b.fft_look[vb.W], pcm);

                /* + .345 is a hack; the original todB estimation used on IEEE 754 compliant machines had a bug that
                  returned dB values about a third of a decibel too high.  The bug was harmless because tunings
                  implicitly took that into account.  However, fixing the bug in the estimator requires changing all the tunings as well.
                  For now, it's easier to sync things back up here, and recalibrate the tunings in the next major model upgrade. */
                  
                logfft[0] = scale_dB + todB(*pcm) + 0.345f;
                local_ampmax[i] = logfft[0];

                for (j = 1; j < n - 1; j += 2)
                {
                    float temp = pcm[j] * pcm[j] + pcm[j + 1] * pcm[j + 1];

                    /* + .345 is a hack; the original todB estimation used on IEEE 754 compliant machines had a bug that
                      returned dB values about a third of a decibel too high.  The bug was harmless because tunings
                      implicitly took that into account.  However, fixing the bug in the estimator requires changing all the tunings as well.
                      For now, it's easier to sync things back up here, and recalibrate the tunings in the next major model upgrade. */
                      
                    temp = logfft[(j + 1) >> 1] = scale_dB + 0.5f * todB(temp) + 0.345f;

                    if (temp > local_ampmax[i])
                    {
                        local_ampmax[i] = temp;
                    }
                }

                if (local_ampmax[i] > 0.0f)
                {
                    local_ampmax[i] = 0.0f;
                }

                if (local_ampmax[i] > global_ampmax)
                {
                    global_ampmax = local_ampmax[i];
                }
            }

            {
                float* noise = (float*)_vorbis_block_alloc(ref vb, n / 2 * sizeof(float));
                float* tone = (float*)_vorbis_block_alloc(ref vb, n / 2 * sizeof(float));

                for (i = 0; i < vi.channels; i++)
                {
                    /* the encoder setup assumes that all the modes used by any
                       specific bitrate tweaking use the same floor */

                    int submap = info.chmuxlist[i];

                    /* the following makes things clearer to *me* anyway */

                    float* mdct = gmdct[i];
                    float* logfft = vb.pcm[i];

                    float* logmdct = logfft + n / 2;
                    float* logmask = logfft;

                    vb.mode = modenumber;

                    floor_posts[i] = (int**)_vorbis_block_alloc(ref vb, PACKETBLOBS * sizeof(int*));
                    ZeroMemory(floor_posts[i], sizeof(int*) * PACKETBLOBS);

                    for (j = 0; j < n / 2; j++)
                    {
                        /* + .345 is a hack; the original todB estimation used on IEEE 754 compliant machines had a bug that
                        returned dB values about a third of a decibel too high.  The bug was harmless because tunings
                        implicitly took that into account.  However, fixing the bug in the estimator requires changing all the tunings as well.
                        For now, it's easier to sync things back up here, and recalibrate the tunings in the next major model upgrade. */
            
                        logmdct[j] = todB(mdct[j]) + 0.345f;

                        /* first step; noise masking.  Not only does 'noise masking' give us curves from which we can decide how much resolution
                        to give noise parts of the spectrum, it also implicitly hands us a tonality estimate (the larger the value in the
                        'noise_depth' vector, the more tonal that area is) */

                        _vp_noisemask(ref psy_look, logmdct, noise); /* noise does not have by-frequency offset bias applied yet */

                        /* second step: 'all the other crap'; all the stuff that isn't computed/fit for bitrate management goes in the second psy
                        vector.  This includes tone masking, peak limiting and ATH */

                        _vp_tonemask(ref psy_look, logfft, tone, global_ampmax, local_ampmax[i]);

                        /* third step; we offset the noise vectors, overlay tone masking.  We then do a floor1-specific line fit.  If we're
                        performing bitrate management, the line fit is performed multiple times for up/down tweakage on demand. */

                        _vp_offset_and_mix(ref psy_look, noise, tone, 1, logmask, mdct, logmdct);

                        /* this algorithm is hardwired to floor 1 for now; abort out if  we're *not* floor1.  This won't happen unless someone has
                        broken the encode setup lib.  Guard it anyway. */
            
                        if (ci.floor_type[info.floorsubmap[submap]] != 1)
                        {
                            return -1;
                        }

                        floor_posts[i][PACKETBLOBS / 2] = floor1_fit(ref vb, b.flr[info.floorsubmap[submap]] as vorbis_look_floor1, logmdct, logmask);

                        /* are we managing bitrate?  If so, perform two more fits for later rate tweaking (fits represent hi/lo) */
                        if (vorbis_bitrate_managed(ref vb) != 0 && floor_posts[i][PACKETBLOBS / 2] != null)
                        {
                            /* higher rate by way of lower noise curve */
                            _vp_offset_and_mix(ref psy_look, noise, tone, 2, logmask, mdct, logmdct);

                            floor_posts[i][PACKETBLOBS - 1] = floor1_fit(ref vb, b.flr[info.floorsubmap[submap]] as vorbis_look_floor1, logmdct, logmask);

                            /* lower rate by way of higher noise curve */
                            _vp_offset_and_mix(ref psy_look, noise, tone, 0, logmask, mdct, logmdct);

                            floor_posts[i][0] = floor1_fit(ref vb, b.flr[info.floorsubmap[submap]] as vorbis_look_floor1, logmdct, logmask);

                            /* we also interpolate a range of intermediate curves for
                               intermediate rates */
                            for (k = 1; k < PACKETBLOBS / 2; k++)
                            {
                                floor_posts[i][k] = floor1_interpolate_fit(ref vb, b.flr[info.floorsubmap[submap]] as vorbis_look_floor1, floor_posts[i][0], floor_posts[i][PACKETBLOBS / 2], k * 65536 / (PACKETBLOBS / 2));
                            }

                            for (k = PACKETBLOBS / 2 + 1; k < PACKETBLOBS - 1; k++)
                            {
                                floor_posts[i][k] = floor1_interpolate_fit(ref vb, b.flr[info.floorsubmap[submap]] as vorbis_look_floor1, floor_posts[i][PACKETBLOBS / 2], floor_posts[i][PACKETBLOBS - 1], (k - PACKETBLOBS / 2) * 65536 / (PACKETBLOBS / 2));
                            }
                        }
                    }
                }
                
                vbi.ampmax = global_ampmax;

                /*
                  the next phases are performed once for vbr-only and PACKETBLOB
                  times for bitrate managed modes.

                  1) encode actual mode being used
                  2) encode the floor for each channel, compute coded mask curve/res
                  3) normalize and couple.
                  4) encode residue
                  5) save packet bytes to the packetblob vector
              */

                /* iterate over the many masking curve fits we've created */

                {
                    int** couple_bundle = stackalloc int*[vi.channels];
                    int* zerobundle = stackalloc int[vi.channels];

                    for (k = (vorbis_bitrate_managed(ref vb) != 0 ? 0 : PACKETBLOBS / 2); k <= (vorbis_bitrate_managed(ref vb) != 0 ? PACKETBLOBS - 1 : PACKETBLOBS / 2); k++)
                    {
                        Ogg.oggpack_buffer opb = vbi.packetblob[k];

                        /* start out our new packet blob with packet type and mode */
                        /* Encode the packet type */
                        Ogg.oggpack_write(ref opb, 0, 1);

                        /* Encode the modenumber */
                        /* Encode frame mode, pre,post windowsize, then dispatch */
                        Ogg.oggpack_write(ref opb, (uint)modenumber, b.modebits);

                        if (vb.W != 0)
                        {
                            Ogg.oggpack_write(ref opb, (uint)vb.lW, 1);
                            Ogg.oggpack_write(ref opb, (uint)vb.nW, 1);
                        }

                        /* encode floor, compute masking curve, sep out residue */
                        for (i = 0; i < vi.channels; i++)
                        {
                            int submap = info.chmuxlist[i];
                            int* ilogmask = iwork[i];

                            nonzero[i] = floor1_encode(ref opb, ref vb, b.flr[info.floorsubmap[submap]] as vorbis_look_floor1, floor_posts[i][k], ilogmask);
                        }

                        /* our iteration is now based on masking curve, not prequant and coupling.  Only one prequant/coupling step */

                        /* quantize/couple */
                        /* incomplete implementation that assumes the tree is all depth one, or no tree at all */
                        _vp_couple_quantize_normalize(k, ci.psy_g_param, ref psy_look, info, gmdct, iwork, nonzero, ci.psy_g_param.sliding_lowpass[vb.W, k], vi.channels);

                        /* classify and encode by submap */
                        for (i = 0; i < info.submaps; i++)
                        {
                            int ch_in_bundle = 0;
                            int** classifications;
                            int resnum = info.residuesubmap[i];

                            for (j = 0; j < vi.channels; j++)
                            {
                                if (info.chmuxlist[j] == i)
                                {
                                    zerobundle[ch_in_bundle] = 0;

                                    if (nonzero[j] != 0)
                                    {
                                        zerobundle[ch_in_bundle] = 1;
                                    }

                                    couple_bundle[ch_in_bundle++] = iwork[j];
                                }
                            }

                            classifications = _residue_P[ci.residue_type[resnum]]._class(ref vb, b.residue[resnum], couple_bundle, zerobundle, ch_in_bundle);
                            ch_in_bundle = 0;
                            
                            for (j = 0; j < vi.channels; j++)
                            {
                                if (info.chmuxlist[j] == i)
                                {
                                    couple_bundle[ch_in_bundle++] = iwork[j];
                                }
                            }

                            _residue_P[ci.residue_type[resnum]].forward(ref opb, ref vb, b.residue[resnum], couple_bundle, zerobundle, ch_in_bundle, classifications, i);
                        }

                        /* ok, done encoding.  Next protopacket. */
                    }
                }

                return 0;
            }
        }

        static int mapping0_inverse(ref vorbis_block vb, vorbis_info_mapping l)
        {
            vorbis_dsp_state vd = vb.vd;
            vorbis_info vi = vd.vi;
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            private_state b = vd.backend_state as private_state;
            vorbis_info_mapping0 info = l as vorbis_info_mapping0;

            int i, j;
            int n = vb.pcmend = ci.blocksizes[vb.W];

            float** pcmbundle = stackalloc float*[vi.channels];
            int* zerobundle = stackalloc int[vi.channels];

            int* nonzero = stackalloc int[vi.channels];
            void** floormemo = stackalloc void*[vi.channels];

            /* recover the spectral envelope; store it in the PCM vector for now */
            for (i = 0; i < vi.channels; i++)
            {
                int submap = info.chmuxlist[i];

                floormemo[i] = _floor_P[ci.floor_type[info.floorsubmap[submap]]].inverse1(ref vb, b.flr[info.floorsubmap[submap]]);

                if (floormemo[i] != null)
                {
                    nonzero[i] = 1;
                }
                else
                {
                    nonzero[i] = 0;
                }

                ZeroMemory(vb.pcm[i], sizeof(float) * n / 2);
            }

            /* channel coupling can 'dirty' the nonzero listing */
            for (i = 0; i < info.coupling_steps; i++)
            {
                if (nonzero[info.coupling_mag[i]] != 0 || nonzero[info.coupling_ang[i]] != 0)
                {
                    nonzero[info.coupling_mag[i]] = 1;
                    nonzero[info.coupling_ang[i]] = 1;
                }
            }

            /* recover the residue into our working vectors */
            for (i = 0; i < info.submaps; i++)
            {
                int ch_in_bundle = 0;
                
                for (j = 0; j < vi.channels; j++)
                {
                    if (info.chmuxlist[j] == i)
                    {
                        if (nonzero[j] != 0)
                        {
                            zerobundle[ch_in_bundle] = 1;
                        }
                        else
                        {
                            zerobundle[ch_in_bundle] = 0;
                        }
                        
                        pcmbundle[ch_in_bundle++] = vb.pcm[j];
                    }
                }

                _residue_P[ci.residue_type[info.residuesubmap[i]]].inverse(ref vb, b.residue[info.residuesubmap[i]], pcmbundle, zerobundle, ch_in_bundle);
            }

            /* channel coupling */
            for (i = info.coupling_steps - 1; i >= 0; i--)
            {
                float* pcmM = vb.pcm[info.coupling_mag[i]];
                float* pcmA = vb.pcm[info.coupling_ang[i]];

                for (j = 0; j < n / 2; j++)
                {
                    float mag = pcmM[j];
                    float ang = pcmA[j];

                    if (mag > 0)
                    {
                        if (ang > 0)
                        {
                            pcmM[j] = mag;
                            pcmA[j] = mag - ang;
                        }
                        else
                        {
                            pcmA[j] = mag;
                            pcmM[j] = mag + ang;
                        }
                    }
                    else
                    {
                        if (ang > 0)
                        {
                            pcmM[j] = mag;
                            pcmA[j] = mag + ang;
                        }
                        else
                        {
                            pcmA[j] = mag;
                            pcmM[j] = mag - ang;
                        }
                    }
                }
            }

            /* compute and apply spectral envelope */
            for (i = 0; i < vi.channels; i++)
            {
                float* pcm = vb.pcm[i];
                int submap = info.chmuxlist[i];

                _floor_P[ci.floor_type[info.floorsubmap[submap]]].inverse2(ref vb, b.flr[info.floorsubmap[submap]], floormemo[i], pcm);
            }

            /* transform the PCM data; takes PCM vector, vb; modifies PCM vector */
            /* only MDCT right now.... */
            for (i = 0; i < vi.channels; i++)
            {
                float* pcm = vb.pcm[i];
                mdct_backward(b.transform[vb.W][0] as mdct_lookup, pcm, pcm);
            }

            /* all done! */
            return 0;
        }
        
        /* export hooks */
        static readonly vorbis_func_mapping mapping0_exportbundle = new vorbis_func_mapping()
        {
            pack = mapping0_pack,
            unpack = mapping0_unpack, 
            free_info = mapping0_free_info, 
            forward = mapping0_forward,
            inverse = mapping0_inverse
        };        
    }
}