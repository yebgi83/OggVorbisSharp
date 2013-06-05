using System;
using System.Collections.Generic;
using System.Text;

namespace OggVorbisSharpTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // OggTest_Bitwises.Test();
                // OggTest_Flaming.Test();
                VorbisFileTest.Test();
            }
            finally
            {
                Console.WriteLine("Press any key to terminate.");
                Console.ReadKey();
            }
        }
    }
}
