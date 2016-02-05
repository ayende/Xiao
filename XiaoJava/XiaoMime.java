import java.lang.Math;

/*
*Custom Mimetype Encoding based on https://github.com/ayende/Xiao and in turn Smaz
*Ported to Java with modified MimeType terms table by Peter Rodgers 2016-01-22
*/
public class XiaoMime
{
    private static String[] _termsTable =
    {
    	"doc", "text/plain", "text/html", "text/xml", "text/css",
    	"application/javascript", "application/json", "application/xhtml+xml", "application/ld+json",
    	"application/vnd.", "application/x-",
    	"text/", "application/", "+xml", "+json", "soap",
    	"text", "plain", "xml", "json-ld", "json",
    	"html", "xhtml", "vnd.", "vnd", "x-",
    	"audio/", "midi", "mp3", "mp4", "aac", "wav", "mpeg",
    	"image/", "jpeg",	"jpg", "gif", "png", 
    	"video/", "h261", "h263","h264","m4v",
    	"multipart/", "model/", "message", "patch", "calendar", 
    	"ms-", "zip", "n3", "turtle",
    	"javascript", "java", "atom", "rss", "geo", "pdf", "css",
    	"/", "+", "ea", "ed", "oasis", "ss", "io", "ia", "on", "ml", "dvb", "etsi", "fuji"
    };

    private static byte[][] _termsTableBytes;

    private static byte[][] _hashTable;

    private static int _maxTermSize;
    private static int _maxVerbatimLen;
    private static int MAX_BYTE_SIZE=255;

    public XiaoMime() throws Exception
    {
    	if(_hashTable==null)
    	{
	        if (_termsTable.length + 8 > MAX_BYTE_SIZE)
	            throw new Exception("Too many terms defined");
	
	        _termsTableBytes = new byte[_termsTable.length][];
	        _maxVerbatimLen = MAX_BYTE_SIZE - _termsTable.length;
	        _hashTable = new byte[MAX_BYTE_SIZE][];
	        for (int i = 0; i < _termsTable.length; i++)
	        {
	            byte[] bytes = _termsTable[i].getBytes("UTF-8");
	            if (bytes.length > MAX_BYTE_SIZE)
	                throw new Exception("Term " + _termsTable[i] + " is too big");
	            _termsTableBytes[i] = bytes;
	            byte[] buffer = new byte[bytes.length + 2];// 1 for size, 1 for index
	            buffer[0] = (byte)bytes.length;
	            buffer[buffer.length - 1] = (byte)i;
	            System.arraycopy(bytes, 0, buffer, 1, bytes.length);
	            _maxTermSize = Math.max(_maxTermSize, bytes.length);
	
	            int h = bytes[0] << 3;
	            addToHash(h, buffer);
	            if (bytes.length == 1)
	                continue;
	            h += bytes[1];
	            addToHash(h, buffer);
	            if (bytes.length == 2)
	                continue;
	            h ^= bytes[2];
	            addToHash(h, buffer);
	        }
	        byte[] empty = new byte[0];
	
	        for (int i = 0; i < _hashTable.length; i++)
	        {
	            if (_hashTable[i] == null)
	                _hashTable[i] = empty;
	        }
    	}
    }

    private void addToHash(int hash, byte[] buffer)
    {
        int index = hash % _hashTable.length;
        if (_hashTable[index] == null)
        {
            _hashTable[index] = buffer;
            return;
        }
        byte[] newBuffer = new byte[_hashTable[index].length + buffer.length];
        System.arraycopy(_hashTable[index], 0, newBuffer, 0, _hashTable[index].length);
        System.arraycopy(buffer, 0, newBuffer, _hashTable[index].length, buffer.length);
        _hashTable[index] = newBuffer;
    }

    public int decompress(byte[] input, int inputLen, byte[] output)
    {
        int outPos = 0;
        for (int i = 0; i < inputLen; i++)
        {	int slot = input[i];
            if (slot >= _termsTable.length)
            {
                // verbatim entry
                int len = slot - _termsTable.length;
                System.arraycopy(input, i + 1, output, outPos, len);
                outPos += len;
                i += len;
            }
            else
            {
            	int len=_termsTableBytes[slot].length;
                System.arraycopy(_termsTableBytes[slot], 0, output, outPos, len);
                outPos += _termsTableBytes[slot].length;
            }
        }
        return outPos;
    }

    public int compress(byte[] input, byte[] output)
    {
        int outPos = 0;
        int verbatimStart = 0;
        int verbatimLength = 0;
        for (int i = 0; i < input.length; i++)
        {
            int size = _maxTermSize;
            int h1, h2 = 0, h3 = 0;
            h1 = input[i] << 3;
            if (i + 1 < input.length)
                h2 = h1 + input[i + 1];
            if (i + 2 < input.length)
                h3 = h2 ^ input[i + 2];
            if (i + size >= input.length)
                size = input.length - i;
            boolean foundMatch = false;
            for (; size > 0 && foundMatch == false; size--)
            {
                byte[] slot;
                switch (size)
                {
                    case 1: slot = _hashTable[h1 % _hashTable.length]; break;
                    case 2: slot = _hashTable[h2 % _hashTable.length]; break;
                    default: slot = _hashTable[h3 % _hashTable.length]; break;
                }
                int pos = 0;
                while (pos + 1 // has actual data, not length info
                    < slot.length)
                {
                    int termLegnth = slot[pos];
                    if (termLegnth != size ||
                        bufferEquals(slot, pos + 1, input, i, size) == false)
                    {
                        pos += termLegnth + 2;// skip len of string, the size and the index
                        continue;
                    }
                    if (verbatimLength > 0)
                    {
                        int[] result=flush(input, output, verbatimStart, verbatimLength, outPos);
                        outPos=result[0];
                        verbatimStart=result[1];
                        verbatimLength=result[2];
                    }
                    output[outPos++] = slot[termLegnth + pos + 1];// get the index to write there
                    verbatimStart = i + termLegnth;
                    i += termLegnth - 1;// skip the length we just compressed
                    foundMatch = true;
                    break;
                }
            }
            if (foundMatch == false)
                verbatimLength++;
        }
        int[] result=flush(input, output, verbatimStart, verbatimLength, outPos);
        outPos=result[0];
        verbatimStart=result[1];
        verbatimLength=result[2];
        return outPos;
    }

    private static int[] flush(byte[] input, byte[] output, int verbatimStart, int verbatimLength, int outPos)
    {
        while (verbatimLength > 0)
        {
            int len = Math.min(_maxVerbatimLen - 1, verbatimLength);
            output[outPos++] = (byte)(len + _termsTable.length);
            System.arraycopy(input, verbatimStart, output, outPos, len);
            verbatimStart += len;
            verbatimLength -= len;
            outPos += len;
        }
        return new int[]{outPos, verbatimStart, verbatimLength};	//Need to return these as Java doesn't support pass by reference integers
    }

    public static boolean bufferEquals(byte[] x, int xStart, byte[] y, int yStart, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (y[yStart + i] != x[xStart + i])
                return false;
        }
        return true;
    }
}
