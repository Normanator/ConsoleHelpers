using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;



namespace My.Utilities
{

    // PerfCompare is a dirt-simple scaffold to prep, run, and cleanup
    // your PerfCandidate implementations and to summarize the results.
    // It is used to provide 2+ implementations that are run against each other
    // (your job is to ensure they have basically identical conditioons so the 
    //  results are apples-to-apples).
    //
    // Core concepts:
    //    1) Provide to PerfCompare the following:
    //         a) a seed integer to from which all PerfCompare groups and runs 
    //            will use as the basis of the Random object.
    //         b) the NumberOfRuns N to evaluate each candidate over
    //         c) some 'scaleOverall' number you can use in InitCandidate calls
    //            e.g. to vary data-volumes across a few orders-of-magnitude,
    //            an entirely user-defined meaning.
    //         d) optionally some 'scalePerRun' as a hint to each Run call,
    //            e.g. in case selecting 1000 results differs in perf from selecting 10.
    //    2) Provide a list of PerfCandidates to PerfCompare
    //    3) InitCandidate allows you to create the durable data-structures 
    //       that will be used across all the runs.
    //    4) InitRun will be called each of the N runs to allow you to 
    //       pre-generate a block of inputs, etc. outside the timer.
    //       You get a Random object here to help you create loads free of bias.
    //    5) ValidateRun is called after each run outside the timer,
    //       allowing you to verify the correctness of results, etc.,
    //       or just to clean-up any input batches pre-gen'd in InitRun.
    //    6) After N executions of InitRun, Run, ValidateRun, the candidate is retired
    //       and ValidateFinal then CleanupFinal are called. 
    //    7) Ask PerfCompare for a Report. 


    /// <summary>
    /// A wrapper to setup, run, and tear-down your test implementation
    /// </summary>
    public class PerfCandidate
    {
        /// <summary>The method to test for performance</summary>
        public Action   Run           { get; set; }

        /// <summary>A short description why you've made this candidate</summary>
        public string   Summary       { get; set; }

        /// <summary>
        /// A chance to create durable data that will be used in later runs.
        /// You can prime-the-pump here, too.
        /// </summary>
        /// <remarks>The first param is the 'scale' value you provided,
        /// the second parameter is a pre-seeded Random object.</remarks>
        public Action<int, Random>   InitCandidate  { get; set; }

        /// <summary>A method you can use to initialize each run.</summary>
        /// <remarks>This will be called before each of the N Run calls.
        /// The first int is the run number (0..NumberOfRuns-1), 
        /// the second int is the scalePerRun value you specified,
        /// the Random object will have been pre-seeded (repeatably so,
        /// as a deterministic function of the initial seed to PerfCompar)</remarks>
        public Action<int,int,Random>    InitRun        { get; set; }

        /// <summary>A way to verify the run's results (optional)</summary>
        /// <remarks>Return false to mark the test as failed.</remarks>
        public Func<bool>            ValidateRun    { get; set; }

        /// <summary>A way to check state after all the runs</summary>
        /// <remarks>Return false to mark the candidate as failed.</remarks>
        public Func<bool>            ValidateFinal  { get; set; }

        /// <summary>Callback to tear-down the InitCandidate data</summary>
        public Action                CleanupFinal   { get; set; }


        internal void   ProvideDefaultBehaviors()
        {
            if( Run == null )
                throw new ArgumentNullException( "Run", "PerfCandidate must have something to test" );

            Summary       = Summary ?? string.Empty;

            if( InitCandidate == null )
                InitCandidate = (i,r) => {};

            if( InitRun == null )
                InitRun=(i,j,r) => {};

            if( ValidateRun == null )
                ValidateRun = () => true;

            if( ValidateFinal == null )
                ValidateFinal = () => true;

            if( CleanupFinal == null )
                CleanupFinal = () => {};
        }
    }



    public class PerfCompare
    {
        private int RandSeed     { get; set; }
        private int NumberOfRuns { get; set; }
        private int ScaleOverall { get; set; }
        private int ScalePerRun  { get; set; }
        private int [] RunSeeds  { get; set; }

        private List<PerfCandidate>   candidates = new List<PerfCandidate>();
        private List<PerfResult>      results    = new List<PerfResult>();


        public PerfCompare( int seed, int numberOfRuns, int scaleOverall, int scalePerRun = 1 )
        {
            this.RandSeed     = seed;
            this.NumberOfRuns = numberOfRuns;
            this.ScaleOverall = scaleOverall;
            this.ScalePerRun  = scalePerRun;

            Random   rand = new Random( RandSeed );
            RunSeeds      = MakeSeeds( NumberOfRuns );
        }


        public PerfCompare  Add( PerfCandidate candidate )
        {
            candidates.Add( candidate );
            return this;
        }


        public bool Start( )
        {
            int []  candSeeds = MakeSeeds( candidates.Count );

            bool overallSuccess = true;
            for( int i = 0; i < candidates.Count; ++i )
            {
                var  candidate = candidates[ i ];
                var  pr        = new PerfResult()
                { CandidateNumber = i, Summary = candidate.Summary };

                candidate.ProvideDefaultBehaviors();

                // Create any nifty initial data-volume to operate over.
                candidate.InitCandidate( ScaleOverall, new Random( candSeeds[ i ] ) );

                Stopwatch   stopWatch = new Stopwatch();
                for( int j = 0; j < NumberOfRuns; ++j )
                {
                    // Create any request-set to time
                    candidate.InitRun( j, ScalePerRun, new Random( RunSeeds[ j ] ) );

                    stopWatch.Start();

                    // Fire in the hole!
                    candidate.Run();

                    stopWatch.Stop();

                    // evaluate
                    pr.Ticks += stopWatch.ElapsedTicks;

                    bool ok = candidate.ValidateRun();
                    if( !ok )
                    {
                        pr.RunFailures.Add( j );
                        pr.IsValid = false; 
                    }

                } // end for j

                if( !candidate.ValidateFinal() )
                {
                    pr.IsValid = false;
                }
                overallSuccess = overallSuccess && pr.IsValid;
                results.Add( pr );

                candidate.CleanupFinal();

            } // end for i

            return overallSuccess;
        }


        public IEnumerable<PerfResult>   Report( )
        {
            long Tbest = (from pr in results
                          orderby pr.Ticks
                          select pr.Ticks).First();
            long Tfirst = results[ 0 ].Ticks;

            for( int i = 0; i < results.Count; ++i )
            {
                var   res        = results[ i ];
                float relToFirst = res.Ticks != Tfirst
                                    ? (Tfirst * 100.0f) / res.Ticks
                                    : 100.0f;
                float relToBest  = res.Ticks != Tbest
                                    ? (Tbest * 100.0f) / res.Ticks
                                    : 100.0f;

                results[ i ].RelativePerf = Tuple.Create( relToFirst, relToBest );
            }
            return results.AsEnumerable();
        }


        public IEnumerable<string> FormatReport()
        {
            //                           0   1      2     3
            string [] header = new[]
            { 
                "#\tValid\tMsec        \tbase\tbest\tsummary"
            };
            var fmt = from pr in Report()
                      select string.Format( "{0}\t{1}\t{2:12}\t{3}%\t{4}%\t{5}",
                          pr.CandidateNumber,
                          pr.IsValid ? "ok" : "fail",
                          pr.Msecs.ToString( "N3" ),
                          pr.RelativePerf.Item1.ToString( "N0" ),
                          pr.RelativePerf.Item2.ToString( "N0" ),
                          pr.Summary );
            return header.Union( fmt );
        }



        internal int []  MakeSeeds( int ct )
        {
            Random   rand = new Random( RandSeed );
            return (from i in Enumerable.Range( 0, NumberOfRuns )
                    let  s = rand.Next()
                    select s).ToArray();
        }

    } // end class PerfCompare


    public class PerfResult
    {
        public PerfResult()
        {
            IsValid = true;
        }

        public int                 CandidateNumber { get; internal set; }
        public string              Summary         { get; internal set; }
        public bool                IsValid         { get; internal set; }
        public long                Ticks           { get; internal set; }
        public int                 NumberOfRuns    { get; internal set; }
        public List<int>           RunFailures     { get; internal set; }
        public Tuple<float,float>  RelativePerf    { get; internal set; }

        /// <summary>Gets the elapsed milliseconds</summary>
        /// <remarks>Less accuate than Ticks as CPU frequencies may change during/afer runs</remarks>
        public float               Msecs
        {
            get { return (Ticks * 1000.0f) / Stopwatch.Frequency; }
        }

    }

} // end namespace My.Utilities
