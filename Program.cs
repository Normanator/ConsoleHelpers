using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using My.Utilities;

namespace Example
{
    class Program
    {
        static int Main( string[] args )
        {
            int retCode = 1;
            try
            {
                var progArgs = new MyArguments();
                progArgs.Parse( args );

                if( progArgs.Help ) { Console.WriteLine( progArgs.GetHelp() ); return 0; }
                if( progArgs.Blam ) { BadFunc(); }



                retCode = 0;
            }
            catch( Exception ex )
            {
                string msg = ExceptionHandler.FormatDetails( ex );
                Console.Error.WriteLine( msg );
            }

            if( System.Diagnostics.Debugger.IsAttached )
            {
                Console.WriteLine( "Hit Enter to stop debugging" );
                Console.ReadKey( true );
            }
            return retCode;
        }


        #region Demo Exception
        private static void BadFunc( ) { BadFunc2(); }
        private static void BadFunc2( )
        {
            try { BadFunc3(); }
            catch( Exception ex ) { throw new InvalidOperationException( "dang", ex ); }
        }
        private static void BadFunc3()
        {
            throw new IndexOutOfRangeException( "inner-dang!" ); 
        }
        #endregion
    }

    // ----------------------------------

    internal class MyArguments : ProgramArguments
    {
        public MyArguments() : base( caseSensitive: false )
        {
            var blam = new BoolArgDef( "Blam!", "!", "Blam",
                "Inject a fault to test ExceptionHandler display" );
            Add( blam );


        }

        public bool Blam { get { return this["Blam!"].GetBool(); } }
    }

}
