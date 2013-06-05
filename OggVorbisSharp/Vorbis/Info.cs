/********************************************************************
 *                                                                  *
 * THIS FILE IS PART OF THE OggVorbis SOFTWARE CODEC SOURCE CODE.   *
 * USE, DISTRIBUTION AND REPRODUCTION OF THIS LIBRARY SOURCE IS     *
 * GOVERNED BY A BSD-STYLE SOURCE LICENSE INCLUDED WITH THIS SOURCE *
 * IN 'COPYING'. PLEASE READ THESE TERMS BEFORE DISTRIBUTING.       *
 *                                                                  *
 * THE OggVorbis SOURCE CODE IS (C) COPYRIGHT 1994-2010             *
 * by the Xiph.Org Foundation http://www.xiph.org/                  *
 *                                                                  *
 ********************************************************************

 function: maintain the info structure, info <-> header packets
 last mod: $Id: info.c 18186 2012-02-03 22:08:44Z xiphmont $

 ********************************************************************/

/* This C# source is ported by Lee kang-yong (yebgi83@gmail.com) */  

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

/* general handling of the header and the vorbis_info structure (and
   substructures) */

namespace OggVorbisSharp
{
    // Info
    static public unsafe partial class Vorbis
    {
        /* helpers */

        static void _v_writestring(ref Ogg.oggpack_buffer o, char[] s, int bytes)
        {
            for(int i = 0; i < bytes; i++) {
                Ogg.oggpack_write(ref o, s[i], 8);
            }
        }

        static void _v_readstring(ref Ogg.oggpack_buffer o, ref char[] buf, int bytes)
        {
            for(int i = 0; i < bytes; i++) {
                buf[i] = (char)Ogg.oggpack_read(ref o, 8);
            }
        }
        
        static public int vorbis_comment_init(ref vorbis_comment vc)
        {
            return vorbis_comment_clear(ref vc);
        }
        
        static public void vorbis_comment_add(ref vorbis_comment vc, ref char[] comment)
        {
            Array.Resize(ref vc.user_comments, vc.comments + 2);
            Array.Resize(ref vc.comment_lengths, vc.comments + 2);
            
            vc.comment_lengths[vc.comments] = comment.Length;
            vc.user_comments[vc.comments] = new char[vc.comment_lengths[vc.comments] + 1];
            
            Array.Copy(comment, 0, vc.user_comments, vc.comments, comment.Length);
            
            vc.comments++;
            vc.user_comments[vc.comments] = null;
        }
        
        static public void vorbis_comment_add_tag(ref vorbis_comment vc, char[] tag, char[] contents)
        {
            char[] comment = new char[tag.Length + contents.Length + 2]; /* +2 for = and \0 */
            
            Array.Copy(tag, 0, comment, 0, tag.Length);
            comment[tag.Length] = '=';
            Array.Copy(contents, 0, comment, tag.Length + 1, contents.Length);
            
            vorbis_comment_add(ref vc, ref comment);
        }
        
        /* This is more or less the same as strncasecmp - but that doesn't exist everywhere, and this is a fairly trivial function, so we include it */
        static public int tagcompare(ref char[] s1, ref char[] s2, int n)
        {
            int c = 0;
            
            while (c < n) 
            {
                if (Char.ToUpper(s1[c]) != Char.ToUpper(s2[c])) {
                    return 1;
                }
                
                c++;
            }
            
            return 0;
        }
        
        static public char[] vorbis_comment_query(ref vorbis_comment vc, ref char[] tag, int count) 
        {
            int i;
            int found = 0;
            int taglen = tag.Length + 1; /* +1 for the = we append */
            char[] fulltag = new char[taglen + 1];
            
            Array.Copy(tag, 0, fulltag, 0, tag.Length);
            fulltag[tag.Length] = '=';
            
            for (i = 0; i < vc.comments; i++)
            {
                if (tagcompare(ref vc.user_comments[i], ref fulltag, taglen) != 0) 
                {
                    if (count == found) 
                    {
                        char[] ret = new char[taglen - i];
                        Array.Copy(vc.user_comments, i, ret, 0, ret.Length);
                        
                        return ret;
                    } 
                    else 
                    {
                        found ++;
                    }
                }
            }
            
            return null; /* didn't find anything */
        }
        
        static public int vorbis_comment_query_count(ref vorbis_comment vc, ref char[] tag)
        {
            int i;
            int count = 0;
            int taglen = tag.Length + 1; /* +1 for the = we append */
            char[] fulltag = new char[taglen + 1];
            
            Array.Copy(tag, 0, fulltag, 0, tag.Length);
            fulltag[tag.Length] = '=';
            
            for (i = 0; i < vc.comments; i++)
            {
                if (tagcompare(ref vc.user_comments[i], ref fulltag, taglen) != 0) {
                    count++;
                }
            }
            
            return count;
        }

        static public int vorbis_comment_clear(ref vorbis_comment vc)
        {
            if (vc == null)
            {
                return 1;
            }
        
            vc.user_comments = null;
            vc.comment_lengths = null;
            vc.comments = 0;
            vc.vendor = null;
            return 0;
        }
        
        /* blocksize 0 is guaranteed to be short, 1 is guaranteed to be long. They may be equal, but short will never ge greater than long */
        
        static public int vorbis_info_blocksize(ref vorbis_info vi, int zo)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;
            return (ci != null) ? ci.blocksizes[zo] : -1;
        }
        
        /* used by synthesis, which has a full, alloced vi */
        
        static public int vorbis_info_init(ref vorbis_info vi) 
        {
            if (vorbis_info_clear(ref vi) != 0)
            {
                return 1;
            }
            
            vi.codec_setup = new codec_setup_info();
            return 0;            
        }
        
        static public int vorbis_info_clear(ref vorbis_info vi)
        {
            if (vi == null)
            {
                return 1;
            }
            
            vi.version = 0;
            vi.channels = 0;
            vi.rate = 0;
            
            vi.bitrate_upper = 0;
            vi.bitrate_nominal = 0;
            vi.bitrate_lower = 0;
            vi.bitrate_window = 0;

            vi.codec_setup = null;
            return 0;
        }

        /* Header packing/unpacking */

        static public int _vorbis_unpack_info(ref vorbis_info vi, ref Ogg.oggpack_buffer opb)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;

            if (ci == null)
            {
                return OV_EFAULT;
            }

            vi.version = Ogg.oggpack_read(ref opb, 32);

            if (vi.version != 0)
            {
                return OV_EVERSION;
            }

            vi.channels = Ogg.oggpack_read(ref opb, 8);
            vi.rate = Ogg.oggpack_read(ref opb, 32);

            vi.bitrate_upper = Ogg.oggpack_read(ref opb, 32);
            vi.bitrate_nominal = Ogg.oggpack_read(ref opb, 32);
            vi.bitrate_lower = Ogg.oggpack_read(ref opb, 32);

            ci.blocksizes[0] = 1 << Ogg.oggpack_read(ref opb, 4);
            ci.blocksizes[1] = 1 << Ogg.oggpack_read(ref opb, 4);

            if (vi.rate < 1)
            {
                goto err_out;
            }

            if (vi.channels < 1)
            {
                goto err_out;
            }

            if (ci.blocksizes[0] < 64)
            {
                goto err_out;
            }

            if (ci.blocksizes[1] < ci.blocksizes[0])
            {
                goto err_out;
            }

            if (ci.blocksizes[1] > 8192)
            {
                goto err_out;
            }

            if (Ogg.oggpack_read(ref opb, 1) != 1)
            {
                goto err_out; /* EOP check */
            }

            return 0;

        err_out:
            vorbis_info_clear(ref vi);
            return OV_EBADHEADER;
        }

        static int _vorbis_unpack_comment(ref vorbis_comment vc, ref Ogg.oggpack_buffer opb)
        {
            int i;
            int vendorlen = Ogg.oggpack_read(ref opb, 32);

            if (vendorlen < 0)
            {
                goto err_out;
            }

            if (vendorlen > opb.storage - 8)
            {
                goto err_out;
            }

            vc.vendor = new char[vendorlen + 1];
            _v_readstring(ref opb, ref vc.vendor, vendorlen);

            i = Ogg.oggpack_read(ref opb, 32);

            if (i < 0)
            {
                goto err_out;
            }

            if (i > ((opb.storage - Ogg.oggpack_bytes(ref opb) >> 2)))
            {
                goto err_out;
            }

            vc.comments = i;
            vc.user_comments = new char[vc.comments + 1][];
            vc.comment_lengths = new int[vc.comments + 1];

            for (i = 0; i < vc.comments; i++)
            {
                int len = Ogg.oggpack_read(ref opb, 32);

                if (len < 0)
                {
                    goto err_out;
                }

                if (len > opb.storage - Ogg.oggpack_bytes(ref opb))
                {
                    goto err_out;
                }

                vc.comment_lengths[i] = len;
                vc.user_comments[i] = new char[len + 1];

                _v_readstring(ref opb, ref vc.user_comments[i], len);
            }

            /* EOP check */
            if (Ogg.oggpack_read(ref opb, 1) != 1)
            {
                goto err_out;
            }

            return 0;

        err_out:
            vorbis_comment_clear(ref vc);
            return OV_EBADHEADER;
        }

        /* all of the real encoding details are here. The modes, books, everything */
        static public int _vorbis_unpack_books(ref vorbis_info vi, ref Ogg.oggpack_buffer opb)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;

            if (ci == null)
            {
                return OV_EFAULT;
            }

            /* code books */
            ci.books = Ogg.oggpack_read(ref opb, 8) + 1;

            if (ci.books <= 0)
            {
                goto err_out;
            }

            for (int i = 0; i < ci.books; i++)
            {
                ci.book_param[i] = vorbis_staticbook_unpack(ref opb);

                if (ci.book_param[i] == null)
                {
                    goto err_out;
                }
            }

            /* time backend settings; hooks are unused */
            {
                int times = Ogg.oggpack_read(ref opb, 6) + 1;

                if (times <= 0)
                {
                    goto err_out;
                }

                for (int i = 0; i < times; i++)
                {
                    int test = Ogg.oggpack_read(ref opb, 16);

                    if (test < 0 || test >= VI_TIMEB)
                    {
                        goto err_out;
                    }
                }
            }

            /* floor backend settings */
            ci.floors = Ogg.oggpack_read(ref opb, 6) + 1;

            if (ci.floors <= 0)
            {
                goto err_out;
            }

            for (int i = 0; i < ci.floors; i++)
            {
                ci.floor_type[i] = Ogg.oggpack_read(ref opb, 16);

                if (ci.floor_type[i] < 0 || ci.floor_type[i] >= VI_FLOORB)
                {
                    goto err_out;
                }

                ci.floor_param[i] = _floor_P[ci.floor_type[i]].unpack(ref vi, ref opb);

                if (ci.floor_param[i] == null)
                {
                    goto err_out;
                }
            }

            /* residue backend settings */
            ci.residues = Ogg.oggpack_read(ref opb, 6) + 1;

            if (ci.residues <= 0)
            {
                goto err_out;
            }

            for (int i = 0; i < ci.residues; i++)
            {
                ci.residue_type[i] = Ogg.oggpack_read(ref opb, 16);

                if (ci.residue_type[i] < 0 || ci.residue_type[i] >= VI_RESB)
                {
                    goto err_out;
                }

                ci.residue_param[i] = _residue_P[ci.residue_type[i]].unpack(ref vi, ref opb);

                if (ci.residue_param == null)
                {
                    goto err_out;
                }
            }

            /* map backed settings */
            ci.maps = Ogg.oggpack_read(ref opb, 6) + 1;

            if (ci.maps <= 0)
            {
                goto err_out;
            }

            for (int i = 0; i < ci.maps; i++)
            {
                ci.map_type[i] = Ogg.oggpack_read(ref opb, 16);

                if (ci.map_type[i] < 0 || ci.map_type[i] >= VI_MAPB)
                {
                    goto err_out;
                }

                ci.map_param[i] = _mapping_P[ci.map_type[i]].unpack(ref vi, ref opb);

                if (ci.map_param[i] == null)
                {
                    goto err_out;
                }
            }

            /* mode settings */
            ci.modes = Ogg.oggpack_read(ref opb, 6) + 1;

            if (ci.modes <= 0)
            {
                goto err_out;
            }

            for (int i = 0; i < ci.modes; i++)
            {
                ci.mode_param[i] = new vorbis_info_mode();
                ci.mode_param[i].blockflag = Ogg.oggpack_read(ref opb, 1);
                ci.mode_param[i].windowtype = Ogg.oggpack_read(ref opb, 16);
                ci.mode_param[i].transformtype = Ogg.oggpack_read(ref opb, 16);
                ci.mode_param[i].mapping = Ogg.oggpack_read(ref opb, 8);

                if (ci.mode_param[i].windowtype >= VI_WINDOWB)
                {
                    goto err_out;
                }

                if (ci.mode_param[i].transformtype >= VI_WINDOWB)
                {
                    goto err_out;
                }

                if (ci.mode_param[i].mapping >= ci.maps)
                {
                    goto err_out;
                }

                if (ci.mode_param[i].mapping < 0)
                {
                    goto err_out;
                }
            }

            if (Ogg.oggpack_read(ref opb, 1) != 1)
            {
                goto err_out;
            }

            return 0;

        err_out:
            vorbis_info_clear(ref vi);
            return OV_EBADHEADER;
        }

        /* Is this packet a vorbis ID header? */
        static public int vorbis_synthesis_idheader(ref Ogg.ogg_packet op)
        {
            Ogg.oggpack_buffer opb = new Ogg.oggpack_buffer();
            char[] buffer = new char[6];

            if (op == null)
            {
                return 0;
            }
            
            Ogg.oggpack_readinit(ref opb, op.packet, op.bytes);

            if (op.b_o_s == 0)
            {
                return 0; /* Not the initial packet */
            }

            if (Ogg.oggpack_read(ref opb, 8) != 1)
            {
                return 0; /* Not an ID header */
            }

            _v_readstring(ref opb, ref buffer, 6);

            if 
            (
                buffer[0] != 'v' ||
                buffer[1] != 'o' ||
                buffer[2] != 'r' ||
                buffer[3] != 'b' ||
                buffer[4] != 'i' ||
                buffer[5] != 's' 
            )
            {
                return 0; /* not vorbis */
            }

            return 1;
        }

        /* The Vorbis header is in three packets; the initial small packet in the first page that identifies basic parameters, a second packet
          with bitstream comments and a third packet that holds the codebook. */

        static public int vorbis_synthesis_headerin(ref vorbis_info vi, ref vorbis_comment vc, ref Ogg.ogg_packet op)
        {
            Ogg.oggpack_buffer opb = new Ogg.oggpack_buffer();
            Ogg.oggpack_readinit(ref opb, op.packet, op.bytes);

            /* Which of the three types of header is this? */
            /* Also verify header-ness, vorbis */
            {
                char[] buffer = new char[6];
                int packtype = Ogg.oggpack_read(ref opb, 8);

                _v_readstring(ref opb, ref buffer, 6);

                if 
                (
                    buffer[0] != 'v' ||
                    buffer[1] != 'o' ||
                    buffer[2] != 'r' ||
                    buffer[3] != 'b' ||
                    buffer[4] != 'i' ||
                    buffer[5] != 's' 
                )
                {
                    /* not a vorbis header */
                    return OV_ENOTVORBIS;
                }

                switch (packtype)
                {
                    case 0x01: /* least significant *bit* is read first */
                        {
                            if (op.b_o_s == 0)
                            {
                                /* Not the initial packet */
                                return OV_EBADHEADER;
                            }

                            if (vi.rate != 0)
                            {
                                /* previously initialized info header */
                                return OV_EBADHEADER;
                            }
                        }
                        return (_vorbis_unpack_info(ref vi, ref opb));

                    case 0x03: /* least significant *bit* is read first */
                        {
                            if (vi.rate == 0)
                            {
                                /* um... we didn't get the initial header */
                                return OV_EBADHEADER;
                            }
                        }
                        return (_vorbis_unpack_comment(ref vc, ref opb));

                    case 0x05: /* least significant *bit* is read first */
                        {
                            if (vi.rate == 0 || vc.vendor == null)
                            {
                                /* um... we didn;t get the initial header or comments yet */
                                return OV_EBADHEADER;
                            }
                        }
                        return (_vorbis_unpack_books(ref vi, ref opb));

                    default:
                        {
                            /* Not a valid vorbis header type */
                            return OV_EBADHEADER;
                        }
                }
            }
        }

        /* pack side */

        static public int _vorbis_pack_info(ref Ogg.oggpack_buffer opb, ref vorbis_info vi)
        {
            codec_setup_info ci = vi.codec_setup as codec_setup_info;

            if (ci == null)
            {
                return OV_EFAULT;
            }

            /* preamble */
            Ogg.oggpack_write(ref opb, 0x01, 8);
            _v_writestring(ref opb, new char[] { 'v', 'o', 'r', 'b', 'i', 's' }, 6);

            /* basic information about the stream */
            Ogg.oggpack_write(ref opb, 0x00, 32);
            Ogg.oggpack_write(ref opb, (uint)vi.channels, 8);
            Ogg.oggpack_write(ref opb, (uint)vi.rate, 32);

            Ogg.oggpack_write(ref opb, (uint)vi.bitrate_upper, 32);
            Ogg.oggpack_write(ref opb, (uint)vi.bitrate_nominal, 32);
            Ogg.oggpack_write(ref opb, (uint)vi.bitrate_lower, 32);

            Ogg.oggpack_write(ref opb, (uint)ilog2((uint)ci.blocksizes[0]), 4);
            Ogg.oggpack_write(ref opb, (uint)ilog2((uint)ci.blocksizes[1]), 4);
            Ogg.oggpack_write(ref opb, 1, 1);

            return 0;
        }
    }
}
