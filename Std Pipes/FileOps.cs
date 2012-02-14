using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace My.Utilities
{

    public class  FileOps
    {

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

        public static string GuessEndOfLineMark( System.IO.Stream  stream, string defaultEoL )
        {
            if( !stream.CanSeek )
                return defaultEoL;

            string eol = defaultEoL;
            long   pos = stream.Position;
            try
            {
                stream.Seek( 0L, System.IO.SeekOrigin.Begin );
                var reader = new System.IO.StreamReader( stream, Encoding.UTF8, true, 4096 );

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
                stream.Seek( pos, System.IO.SeekOrigin.Begin );
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
                
                // Cmd /U opens Unicode pipes to files, but purely internally;
                // the codepage (e.g. chcp 437) stays the same.  
                // CMD does not support chcp 1200 (Unicode) 1201 (UTF-16 BigEndian).

                if( Console.InputEncoding.CodePage == 437 ) // Near certain it's IBM
                {
                    // TODO: Additional checks for 'cmd /U' to pick Unicode
                    encoding = Encoding.ASCII;
                }
                else if( Console.InputEncoding.CodePage == 65000 )
                {
                    encoding = Encoding.UTF8;
                }
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