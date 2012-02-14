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

                if( progArgs["OutFile"] != null ) { Console.WriteLine( (string) progArgs["OutFile"] ); }


                int foo = 0;

                PerfCompare perfComp = new PerfCompare(
                    seed: 123, numberOfRuns: 10, scaleOverall: 1000 );
                perfComp.InitApplication = ( s, r ) => { foo = r.Next( s ) + s; };

                PerfCandidate c1 = new PerfCandidate( "Legacy" )
                {
                    InitCandidate = (i,r) => { Console.WriteLine( "os: {0}\t??: {1}\t{2}", i, r.Next(), "Legacy" ); },
                    InitRun = (j,s,r) => { if( j<2 ) { Console.WriteLine("run[{0}].rand={1}",j, r.Next() ); } },
                    ValidateRun   = () => { return true; },
                    Run = () => { System.Threading.Thread.Sleep( 300 ); },
                };

                perfComp.Add( c1 );

                int delay = 200;
                PerfCandidate c2 = new PerfCandidate( "ASM and SSE" )
                {
                    InitCandidate = (i,r) => { Console.WriteLine( "os: {0}\t??: {1}\t{2}", i, r.Next(), "SSE" ); foo = 101; },
                    InitRun = ( j, s, r ) => { if( j<2 ) { Console.WriteLine( "run[{0}].rand={1}", j, r.Next() ); }; delay = r.Next(200,300); },
                    Run     = () => { System.Threading.Thread.Sleep( delay ); },
                };
                perfComp.Add( c2 );

                PerfCandidate c3 = new PerfCandidate( "Alt memroy manager" )
                {
                    InitCandidate = (i,r) => { Console.WriteLine( "os: {0}\t??: {1}\t{2}  [{3}]", i, r.Next(), "Mem", foo ); },
                    InitRun = (j,s,r) => { if( j<2 ) { Console.WriteLine("run[{0}].rand={1}",j, r.Next() ); } },
                    Run = () => { System.Threading.Thread.Sleep( 315 ); },
                };
                perfComp.Add( c3 );

                perfComp.Start();
                foreach( var result in perfComp.FormatReport() )
                {
                    Console.WriteLine( result );
                }

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
            AddBool( "Blam!",
                     "Injects a fault to test the ExceptionHandler display",
                     "!", "Blam" );
            AddString( "OutFile",
                "The output file to use",
                "o", "outFile" )
                .UnSwitched = true;
        }

        public bool Blam { get { return this["Blam!"].GetBool(); } }
    }

}
