using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace My.Utilities
{

    public class  FileOps
    {

        public static string ChooseCsvColumns( string input,
                                               char   delim=',',
                                               params int [] columns )
        {
            if( input == null || columns == null || columns.Length <=0 )
                return input;

            char []        inChars = input.ToCharArray();
            int            len     = inChars.Length;

            int            start    = 0;
            int            end      = -1;
            int            colCur   = 0;
            int            tgtIdx   = 0;
            StringBuilder  sb       = new StringBuilder( len );

            for( ; tgtIdx < columns.Length; ++tgtIdx )
            {
                int  colTgt    = columns[ tgtIdx ];
                for( ; colCur <= colTgt; ++colCur )
                {
                    start = end + 1;
                    if( start >= len )
                    {
                        goto append_empties;
                    }

                    end   = Array.IndexOf( inChars, delim, start );

                    if( end == -1 )
                    {
                        end = len;
                    };
                }

                if( tgtIdx != 0 )
                {
                    // For 2nd+ extracted columns, add delimiter
                    sb.Append( delim );
                }

                sb.Append( inChars, start, end - start );

            }

            append_empties:
            while( tgtIdx++ < columns.Length )
            {
                // Missing input columns are realized as empty output columns
                sb.Append( delim );
            }

            return sb.ToString();
        }



        public static IEnumerable<string>   ChooseCsvColumns( IEnumerable<string> inputs,
                                                              char delim = ',',
                                                              params int [] columns )
        {
            if( null != columns )
            {
                Array.Sort( columns );
                columns = columns.Distinct().ToArray();
            }

            foreach( string input in inputs )
            {
                yield return ChooseCsvColumns( input, delim, columns );
            }
        }



        public static IEnumerable<string>  LinesOf( TextReader reader )
        {
            for(;;)
            {
                string line = reader.ReadLine( );
                if( null == line )
                    break;

                yield return line;
            }
        }


        #region Interpreting files

        public static string GuessEndOfLineMark( System.IO.FileStream  fs, string defaultEoL )
        {
            if( !fs.CanSeek )
                return defaultEoL;

            string eol = defaultEoL;
            long   pos = fs.Position;
            try
            {
                fs.Seek( 0L, System.IO.SeekOrigin.Begin );
                var reader = new System.IO.StreamReader( fs, Encoding.UTF8, true, 4096 );

                char [] buf    = new char[ 512 ];
                int     offset = 0;
                for(int i = 0;  i < 20; ++i )
                {
                    int     ctRead = reader.Read( buf, offset, 512 - offset );
                    offset = 1;

                    int     foundAt = Array.IndexOf( buf, '\n' );
                    if( foundAt == -1 )
                        continue;

                    eol = (foundAt > 0 && buf[ foundAt - 1 ] == '\r')
                            ? "\r\n"
                            : "\n";
                    break;
                }
            }
            finally
            {
                fs.Seek( pos, System.IO.SeekOrigin.Begin );
            }

            return eol;
        }



        public static bool StreamEndsWithNewLine( System.IO.Stream stream )
        {
            if( !stream.CanSeek )
                return false;

            long pos = stream.Position;

            try
            {
                long filesize = stream.Seek( 0L, SeekOrigin.End );
                if( filesize == 0 )
                    return true;

                // Unicode endian-ness could be 0A00 or 000A
                // Probably not definitive, but
                // most of my files (ascii, utf-8, or utf-16) should work
                long ctToTest = Math.Min( 2L, filesize );

                stream.Seek( -1L * ctToTest, SeekOrigin.End );
                while( ctToTest-- > 0L )
                {
                    byte b = (byte) stream.ReadByte();
                    if( b == 0x0A )
                        return true;
                }
                return false;
            }
            finally
            {
                stream.Seek( pos, System.IO.SeekOrigin.Begin );
            }
        }

        #endregion




        public static void ProjectFields( 
                        string  inFile,
                        int []  columns,
                        string  outFile,
                        char    delim     = ',',
                        int     skipLines = 0,
                        bool    append    = false,
                        bool    forceCRLF = false )
        {
            Encoding              outEncoding     = Encoding.UTF8;
            string                endOfLineMark   = Environment.NewLine;
            string                prewrite        = string.Empty;

            System.IO.TextReader  reader = null;
            System.IO.TextWriter  writer = null;

            try
            {
                var inPair = OpenInput( inFile );
                reader        = inPair.Item1;
                endOfLineMark = inPair.Item2;
                outEncoding   = inPair.Item3;


                var outPair = OpenOutput( outFile, outEncoding, append );
                writer   = outPair.Item1;
                prewrite = outPair.Item2
                            ? endOfLineMark
                            : string.Empty;

                // TODO: replace # with user-supplied comment-char
                var lines = from line in FileOps.LinesOf( reader ).Skip( skipLines )
                            where !line.StartsWith( "#" )
                            select line;
                foreach( string outline in ChooseCsvColumns( lines, delim, columns ) )
                {
                    writer.Write( prewrite );
                    writer.Write( outline );

                    prewrite = endOfLineMark;
                }
            }
            finally
            {
                if( writer != null ) { writer.Dispose(); }
                if( reader != null ) { reader.Dispose(); }
            }
        }


        public static Tuple<TextWriter,bool> OpenOutput(
            string   outFile,
            Encoding outEncoding,
            bool     append = false )
        {
            TextWriter  writer       = null;
            bool        needsNewline = false;

            if( string.IsNullOrEmpty( outFile ) )
            {
                writer          = Console.Out;
            }
            else
            {
                System.IO.FileMode  openMode = append
                        ? System.IO.FileMode.OpenOrCreate
                        : System.IO.FileMode.Create;

                var fs = new System.IO.FileStream( outFile,
                            openMode,
                            System.IO.FileAccess.ReadWrite,
                            System.IO.FileShare.ReadWrite,
                            8192 );
                if( append && fs.CanSeek )
                {
                    var altstream = new FileStream( outFile,
                        FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
                    using( var sr = new StreamReader( altstream, outEncoding, true ) )
                    {
                        sr.Read();
                        outEncoding = sr.CurrentEncoding;
                    }

                    fs.Seek( 0L, System.IO.SeekOrigin.End );
                }
                writer       = new System.IO.StreamWriter( fs, outEncoding );
                needsNewline = append && !StreamEndsWithNewLine( fs );
            }

            return Tuple.Create( writer, needsNewline );
        }


        public static Tuple<TextReader,string,Encoding> OpenInput(
            string   inFile )
        {
            TextReader  reader        = null;
            string      endOfLineMark = Environment.NewLine;
            Encoding    encoding      = Encoding.ASCII;

            if( string.IsNullOrEmpty( inFile ) )
            {
                reader          = Console.In;

                //TODO: Detect stdin encoding.  Cmd /U should open Unicode pipes.
            }
            else
            {
                var fs =new System.IO.FileStream( inFile,
                            System.IO.FileMode.Open,
                            System.IO.FileAccess.Read,
                            System.IO.FileShare.ReadWrite,
                            32767 );

                StreamReader sr = new System.IO.StreamReader( fs, true );

                encoding        = sr.CurrentEncoding;
                endOfLineMark   = GuessEndOfLineMark( fs, endOfLineMark );
                reader = sr;
            }

            return Tuple.Create( reader, endOfLineMark, encoding );
        }

    }


}