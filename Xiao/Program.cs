using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xiao
{
    class Program
    {
        private static string[] strings =
        {
            "מה יגידו אזובי הקיר?",
            "This is a small string",
            "foobar",
            "the end",
            "not-a-g00d-Exampl333",
            "Smaz is a simple compression library",
            "Nothing is more difficult, and therefore more precious, than to be able to decide",
            "this is an example of what works very well with smaz",
            "1000 numbers 2000 will 10 20 30 compress very little",
            "and now a few italian sentences:",
            "Nel mezzo del cammin di nostra vita, mi ritrovai in una selva oscura",
            "Mi illumino di immenso",
            "L'autore di questa libreria vive in Sicilia",
            "try it against urls",
            "http://google.com",
            "http://programming.reddit.com",
            "https://github.com/antirez/smaz/tree/master",
            "/media/hdb1/music/Alben/The Bla",
        };

        static void Main(string[] args)
        {
            var k = new Xiao();

            foreach (var str in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                var output = new byte[256];
                var size = k.Compress(bytes, output);
                var decompressed = new byte[256];
                var newSize = k.Decompress(output, size, decompressed);
                var s = Encoding.UTF8.GetString(decompressed, 0, newSize);
                if (s != str)
                {
                    Console.WriteLine("Opps");
                    break;
                }
                Console.WriteLine($"{size} from {bytes.Length} = {1 - (size/(double)bytes.Length):p} - {str}");
            }
        }
    }
}
