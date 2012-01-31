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
    //    3) PerfCompare.InitApplication allows you to create the durable data-structures 
    //       that will be used across all the runs.  If candidates do destructive things to this,
    //       istead set your data-volume init logic in PerfCandidate.InitCandidate.
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
        public PerfCandidate( string summary )
        { this.Summary = summary; }

        /// <summary>The method to test for performance</summary>
        public Action   Run           { get; set; }

        /// <summary>A short description why you've made this candidate</summary>
        public string   Summary       { get; set; }

        /// <summary>
        /// A chance to create durable data that will be used in later runs.
        /// You can prime-the-pump here, too.
        /// The first param is the overall 'scale' value you provided,
        /// the second parameter is a pre-seeded Random object
        /// (all candidates get the same seed)</summary>
        public Action<int, Random>   InitCandidate  { get; set; }

        /// <summary>A method you can use to initialize each run.
        /// This will be called before each of the N Run calls.
        /// The first int is the run number (0..NumberOfRuns-1), 
        /// the second int is the scalePerRun value you specified,
        /// the Random object will have been pre-seeded with the same
        /// value as the corresponding run-number received for all other candidates
        /// </summary>
        public Action<int,int,Random>    InitRun        { get; set; }

        /// <summary>A way to verify the run's results (optional).
        /// Return false to mark the test as failed.</summary>
        public Func<bool>            ValidateRun    { get; set; }

        /// <summary>A way to check state after all the runs.
        /// Return false to mark the candidate as failed.</summary>
        public Func<bool>            ValidateFinal  { get; set; }

        /// <summary>Callback to tear-down the InitCandidate data</summary>
        public Action                CleanupFinal   { get; set; }

        #region internals

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
        #endregion
    }


    /// <summary>Scaffolding for running comparative performance tests</summary>
    public class PerfCompare
    {
        private int RandSeed      { get; set; }
        private int NumberOfRuns  { get; set; }
        private int ScaleOverall  { get; set; }
        private int ScalePerRun   { get; set; }

        private List<PerfCandidate>   candidates = new List<PerfCandidate>();
        private List<PerfResult>      results    = new List<PerfResult>();


        public PerfCompare( int seed, int numberOfRuns, int scaleOverall, int scalePerRun = 1 )
        {
            this.RandSeed     = seed;
            this.NumberOfRuns = numberOfRuns;
            this.ScaleOverall = scaleOverall;
            this.ScalePerRun  = scalePerRun;

            InitApplication = ( i, r ) => { };
        }

        /// <summary>
        /// A chance to create a test envrionment truly common to all candidates.
        /// (Often better than creating comparable (but tailored) data volumes 
        /// thru PerfCandidate.InitCandidate).
        /// The first param is the overall 'scale' value you provided,
        /// the second parameter is a pre-seeded Random object.</summary>
        public Action<int, Random>   InitApplication  { get; set; }


        /// <summary>Registeres a new candidate implementation.</summary>
        /// <param name="candidate">An alternative algorithm to test.</param>
        /// <returns>This PerfCompare instance (fluent-API style)</returns>
        public PerfCompare  Add( PerfCandidate candidate )
        {
            candidates.Add( candidate );
            return this;
        }


        /// <summary>Kicks off the test runs of all candidates.
        /// (Use Report() to view results.)</summary>
        /// <returns>True if all tests passed their Validate... functions</returns>
        public bool Start( )
        {
            Random  rand      = new Random( RandSeed );
            int     candSeed  = rand.Next();
            int     runSeed   = rand.Next();


            InitApplication( this.ScaleOverall, new Random( rand.Next() ) );

            bool overallSuccess = true;
            for( int i = 0; i < candidates.Count; ++i )
            {
                var  candidate = candidates[ i ];
                var  pr        = new PerfResult()
                { CandidateNumber = i, Summary = candidate.Summary };

                candidate.ProvideDefaultBehaviors();

                // Create any nifty initial data-volume to operate over.
                Random  candRand = new Random( candSeed );
                candidate.InitCandidate( ScaleOverall, candRand );

                Random      runRand   = new Random( runSeed );
                Stopwatch   stopWatch = new Stopwatch();
                for( int j = 0; j < NumberOfRuns; ++j )
                {
                    // Create the request-object for this run, done outside the timer.
                    candidate.InitRun( j, ScalePerRun, runRand );

                    stopWatch.Start();

                    // Fire in the hole!  This is the part we're actually timing
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


        /// <summary>Evaluates results of each candidate.</summary>
        /// <returns>A list of timing results for each candidate.</returns>
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


        /// <summary>Evaluates results of each candidate emitting
        /// a formatted, tab-delimited line for each</summary>
        /// <returns>A list of string representations of PerfResults,
        /// with 2 lines of comments and a header-row.</returns>
        public IEnumerable<string> FormatReport()
        {
            //                           0   1      2     3
            string [] header = new[]
            { 
                string.Format( "#   RandSeed:  {0}  ScaleOverall: {1}  ScalePerRun: {2}",
                               this.RandSeed, this.ScaleOverall, this.ScalePerRun ),
                "#",
                "Candidate\tValid\tMsec        \tbase%\tbest%\tsummary"
            };
            var fmt = from pr in Report()
                      select string.Format( "{0,9}\t{1}\t{2:12}\t{3,4}%\t{4,4}%\t{5}",
                          pr.CandidateNumber,
                          pr.IsValid.ToString(),
                          pr.Msecs.ToString( "N3" ),
                          pr.RelativePerf.Item1.ToString( "N0" ),
                          pr.RelativePerf.Item2.ToString( "N0" ),
                          pr.Summary );
            return header.Union( fmt );
        }


        #region internals
        private int []  MakeSeeds( int ct, int seed )
        {
            Random   rand = new Random( seed );
            return (from i in Enumerable.Range( 0, ct )
                    let  s = rand.Next()
                    select s).ToArray();
        }
        #endregion

    } // end class PerfCompare


    // --------------------------------------------------

    /// <summary>Summary statistics for a given PerfCandidate</summary>
    [System.Diagnostics.DebuggerDisplay("[{CandidateNumber}] Ticks={Ticks} ({Msecs})  IsValid={IsValid}")]
    public class PerfResult
    {
        public PerfResult()
        {
            IsValid = true;
        }

        /// <summary>Ordinal number of Candidate added to PerfCompare</summary>
        public int                 CandidateNumber { get; internal set; }

        /// <summary>Echo of the PerfCandidate summary text</summary>
        public string              Summary         { get; internal set; }

        /// <summary>True if no exceptions or Validate... failures</summary>
        public bool                IsValid         { get; internal set; }

        /// <summary>Raw Stopwatch/RTDCT ticks</summary>
        public long                Ticks           { get; internal set; }

        /// <summary>Echo of numberofRuns value given to PerfCompare</summary>
        public int                 NumberOfRuns    { get; internal set; }

        /// <summary>List of which run #s failed</summary>
        /// <remarks>Seed values mean runs should be repeatable; set a conditional-breakpoint on these numbers</remarks>
        public List<int>           RunFailures     { get; internal set; }

        /// <summary>Tuple of speed vs 0th candidate and vs. fastest candidate,
        /// calculated as Ti/T0 or Ti/Tj respectively.</summary>
        public Tuple<float,float>  RelativePerf    { get; internal set; }

        /// <summary>Gets the elapsed milliseconds</summary>
        /// <remarks>Less accuate than Ticks as CPU frequencies may change during/afer runs</remarks>
        public float               Msecs
        {
            get { return (Ticks * 1000.0f) / Stopwatch.Frequency; }
        }

    }

} // end namespace My.Utilities
