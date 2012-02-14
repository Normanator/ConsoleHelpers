﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace My.Utilities
{
    class CsvUtilLight
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


        /// <summary>
        /// Reads an input line-oriented, deimited-column file,
        /// extracts the requested columns, and writes to an output file
        /// using the same column-delimiter.
        /// </summary>
        /// <param name="inFile">Filename to read (null for StdIn)</param>
        /// <param name="columns">Array of 0-based column #s to take</param>
        /// <param name="outFile">Filename to write (null for StdOut)</param>
        /// <param name="delim">Delimiter character</param>
        /// <param name="skipLines">Number of inFile lines to skip</param>
        /// <param name="append">Should we append to outFile? (false to truncate)</param>
        /// <param name="forceCRLF">Emit CRLF? (false uses inputFile's line-endings)</param>
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
                var inPair    = FileOps.OpenInput( inFile );
                reader        = inPair.Item1;
                endOfLineMark = inPair.Item2;
                outEncoding   = inPair.Item3;


                var outPair = FileOps.OpenOutput( outFile, outEncoding, append );
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

    }
}
