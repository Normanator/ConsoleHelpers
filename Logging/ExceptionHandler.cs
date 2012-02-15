using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;


namespace My.Utilities
{
    public static class ExceptionHandler
    {
        private const string stackSuppressKey = "StackSuppress";

        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed" )]
        public static string FormatDetails( Exception ex, string context = null )
        {
            Exception     ix        = ex;
            StringBuilder sb        = new StringBuilder();
            bool          showStack = true;

            sb.Append( context ?? string.Empty );

            int    level  = 0;
            string indent = string.Empty;
            do
            {
                indent = new string( ' ', 3 * level );

                var detailLines = GetDetails( ix );
                sb.AppendFormat( indent + detailLines.First() );
                foreach( var line in detailLines.Skip(1) )
                {
                    sb.AppendFormat( "   {0}{1}\r\n", indent, line );
                }
                foreach( var dk in ix.Data.Keys )
                {
                    sb.AppendFormat( "      {0}Data[{1}]\t= {2}\r\n",
                        indent, dk.ToString(), ix.Data[ dk ].ToString() );

                    if( dk is string && string.CompareOrdinal( (string) dk, stackSuppressKey ) == 0 )
                    {
                        showStack = false;
                    }
                }
                if( level > 0 )
                {
                    sb.AppendFormat( "      @ {0}\r\n", ix.TargetSite );
                }

                ix   = ix.InnerException;
                ++level;

            } while( ix != null );

            if( showStack )
            {
                sb.AppendLine( ex.StackTrace );
            }

            return sb.ToString();
        }


        /// <summary>Marks an exception as not deserving stack-trace</summary>
        /// <remarks>For some user-interaction or runtime errors,
        /// perhaps a full stack-trace is not helpful.
        /// SetSuppressStack will set ex.Data[] values s.t. 
        /// <see cref="FormatDetails"/> will suppress stack output.</remarks>
        /// <param name="ex"></param>
        public static void SetSuppressStack( Exception ex )
        {
            ex.Data[ stackSuppressKey ] = true;
        }


        internal static IEnumerable<string>  GetDetails( Exception ex )
        {
            string [] lines = (ex.Message ?? string.Empty)
                    .Split( new string[] { "\r\n" }, StringSplitOptions.None );

            yield return string.Format( "[{0}] - {1}\r\n",
                    ex.GetType().ToString(),
                    lines[ 0 ] );

            for( int i = 1; i < lines.Length; ++i )
            {
                yield return lines[ i ];
            }

            // TODO: some double-dispatch, Open-Closed-adhereing specialization
            //       to avoid mod'ing the base-class just to extend to new types
            if( ex is System.Data.SqlClient.SqlException )
            {
                foreach( var line in
                         SpecificDetails( (System.Data.SqlClient.SqlException) ex ) )
                {
                    yield return line;
                }
            }
        }


        internal static IEnumerable<string> SpecificDetails( System.Data.SqlClient.SqlException sex )
        {
            yield return string.Format( "Procedure         : {0}", sex.Procedure ?? "<none>" );
            yield return string.Format( "error Number      : {0}", sex.Number.ToString() );
            yield return string.Format( "severity Class    : {0}", sex.Class.ToString() );
            yield return string.Format( "hresult ErrorCode : 0x{0}", sex.ErrorCode.ToString( "X" ) );
            yield return string.Format( "State             : {0}", sex.State.ToString() );
            yield return string.Format( "Server            : {0}", sex.Server ?? "<none>" );
        }

    } // end class ExceptionHandler

} // end namespace My.Utilities