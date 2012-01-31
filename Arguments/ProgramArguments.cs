using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace My.Utilities
{
    /// <summary>
    /// Definition of allowable command-line arguments
    /// </summary>
    public abstract class ArgDef
    {
        public enum Kind { String, Bool, Int };

        protected bool   isMandatory    = false;
        protected object defaultValue   = null;

        public ArgDef( string propName, string shortSwitch, string longSwitch,
                       string helpText )
        {
            PropertyName = propName;
            ShortSwitch  = shortSwitch;
            LongSwitch   = longSwitch;
            HelpText     = helpText;
        }

        /// <summary>Gets the internal name</summary>
        /// <remarks><see cref="ProgramArguments.this[string]"/></remarks>
        public string PropertyName   { get; private set; }

        /// <summary>Short name usuable on the command-line</summary>
        /// <remarks>One of Shortswitch or LongSwitch is required.</remarks>
        public string ShortSwitch    { get; internal set; }

        /// <summary>The longer name usable on the command-line</summary>
        /// <remarks>One of Shortswitch or LongSwitch is required.</remarks>
        public string LongSwitch     { get; internal set; }

        /// <summary>Gets the primitive-type of the argument</summary>
        public Kind   ArgKind        { get; protected set; }

        /// <summary>
        /// Gets whether this argument can be parsed without a prior
        /// ShortSwitch or LongSwitch being seen.
        /// </summary>
        /// <remarks>There can be only one argument with UnSwitched set</remarks>
        public bool   UnSwitched     { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>If a DefaultValue has been set, Mandatory is ignored.
        /// Mandatory cannot be set on boolean arguments.</remarks>
        public bool   Mandatory
        { 
            get { return isMandatory;  }
            set { isMandatory = (ArgKind != Kind.Bool ? value : false); }
        }

        /// <summary>Gets or sets the default value as a boxed object</summary>
        /// <remarks>
        /// <seealso cref="SetDefaultValue"/> on StringArgDef and IntArgDef.
        /// BoolArgDef always uses a default value of false.
        /// </remarks>
        public object DefaultValue
        { 
            get { return defaultValue;  }
            set { defaultValue = (ArgKind != Kind.Bool ? value : (object)false); }
        }

        /// <summary>Gets or sets help information about this argument</summary>
        /// <remarks>
        /// 
        /// Best to use \r\n to keep lines ~50 characters.
        /// DefaultValue and Mandatory will be emitted automatically.
        /// </remarks>
        public string HelpText       { get; set; }

        /// <summary>In formatting help-text, where should this argument go?</summary>
        /// <remarks>Anything greater than 1001 means after "Help" and "WhatIf"</remarks>
        public int    HelpOrder      { get; set; }
    }

    public class BoolArgDef : ArgDef
    {
        public BoolArgDef( string propName, string shortSwitch, string longSwitch,
                           string helpText )
            : base( propName, shortSwitch, longSwitch, helpText )
        {
            ArgKind = ArgDef.Kind.Bool;
            defaultValue = (object) false; 
        }
    }

    public class IntArgDef : ArgDef
    {
        public IntArgDef( string propName, string shortSwitch, string longSwitch,
                           string helpText )
            : base( propName, shortSwitch, longSwitch, helpText )
        {
            ArgKind = ArgDef.Kind.Int;
        }

        public void SetDefault( int defaultInt )
        {
            ArgKind      = Kind.Int;
            DefaultValue = (object) defaultInt;
        }
    }

    public class StringArgDef : ArgDef
    {
        public StringArgDef( string propName, string shortSwitch, string longSwitch,
                           string helpText )
            : base( propName, shortSwitch, longSwitch, helpText )
        {
            ArgKind = ArgDef.Kind.String;
        }

        public void SetDefault( string defaultString )
        {
            ArgKind      = Kind.String;
            DefaultValue = (object) defaultString;
        }
    }

    // ----------------------------------------------------------

    /// <summary>
    /// Command-line arguments as parsed
    /// </summary>
    class ArgValue
    {
        public static readonly ArgValue  Empty = new ArgValue();

        public ArgDef  Definition { get; set; }
        public object  Value      { get; set; }
        
        public string  GetString() 
        { return Value != null ? Value.ToString() : null; }

        public int  GetInt()
        { return Value != null ? (int) Value : 0; }

        public bool  GetBool()
        { return Value != null ? (bool) Value : false; }
    }

    // ----------------------------------------------------------


    /// <summary>Utility to manage command-line arguments</summary>
    class ProgramArguments
    {
        private const string         Unassigned    = "|";
        IList<ArgDef>                definitions   = new List<ArgDef>();
        IList<string>                remainder     = new List<string>();
        Dictionary<string,ArgValue>  dictionary    = new Dictionary<string, ArgValue>();


        public ProgramArguments( bool caseSensitive = false )
        {
            CaseSensitive = caseSensitive;

            ArgDef  helpArg = new BoolArgDef( "Help", "?", "help", 
                "Displays a help message" );
            helpArg.HelpOrder = 1000;
            Add( helpArg );

            ArgDef  whatIfArg = new BoolArgDef( "WhatIf", null, "WhatIf", 
                    "Shows what would occur without actually doing the actions" );
            whatIfArg.HelpOrder=1001;
            Add( whatIfArg );
        }


        /// <summary>Registers a new potential command-line argument</summary>
        /// <param name="def">A command-line argument definition to add</param>
        public void Add( ArgDef  def )
        {
            ValidateDefinitions( def );

            definitions.Add( def );
        }

        public ProgramArguments  AddInt(
                                   string propName,
                                   string helpString,
                                   string shortSwitch,
                                   string longSwitch   = null,
                                   int?   defaultVal   = null,
                                   bool   isMandatory  = false )
        {
            var ad       = new IntArgDef( propName, shortSwitch, longSwitch, helpString );
            ad.Mandatory = isMandatory;
            if( defaultVal != null && defaultVal.HasValue )
            {
                ad.DefaultValue = (object) defaultVal.Value;
            }
            Add( ad );
            return this;
        }


        public ProgramArguments  AddBool(
                                   string propName,
                                   string helpString, 
                                   string shortSwitch,
                                   string longSwitch   = null )
        {
            var ad       = new BoolArgDef( propName, shortSwitch, longSwitch, helpString );
            Add( ad );
            return this;
        }


        public ProgramArguments  AddString(
                                   string propName,
                                   string helpString,
                                   string shortSwitch,
                                   string longSwitch   = null,
                                   string defaultVal   = null,
                                   bool   isMandatory  = false )
        {
            var ad       = new StringArgDef( propName, shortSwitch, longSwitch, helpString );
            ad.Mandatory = isMandatory;
            if( defaultVal != null )
            {
                ad.DefaultValue = (object) defaultVal;
            }
            Add( ad );
            return this;
        }



        /// <summary>Gets whether argument switchess are case-sensitive</summary>
        /// <remarks>Windows users typically expect fase, case-insensitive</remarks>
        public bool    CaseSensitive { get; private set; }

        /// <summary>Gets or sets a summary of the application itself</summary>
        /// <remarks>If null/empty, <see cref="GetHelp"/> will 
        /// return the process-name alone.</remarks>
        public string  HelpSummary   { get; set; }


        /// <summary>Parse command-line arguments with known ArgDefs</summary>
        /// <remarks>Use the indexer with PropertyNames to get the parsed values
        ///  and the <see cref="GetRemainderArgs"/></remarks>
        /// <param name="args">The command-line args to the program</param>
        public void  Parse( string [] args )
        {
            var scMode = CaseSensitive
                            ? StringComparison.InvariantCulture
                            : StringComparison.InvariantCultureIgnoreCase;

            int       argnum     = 0;
            ArgDef    current    = null;
            string    currSwitch = null;

            AssignDefaults();

            Func<string,ArgDef,bool> stest = ( a, d ) =>
                string.Compare( a, d.ShortSwitch, scMode ) == 0;
            Func<string,ArgDef,bool> ltest = ( a, d ) =>
                string.Compare( a, d.LongSwitch, scMode ) == 0;
            Func<string,ArgDef,bool> etest = ( a, d ) =>
                stest( a, d ) || ltest( a, d );

            // If we see an argument not preceeded by a switch (/Xxx), use this def
            ArgDef  unswitchedArg = definitions.SingleOrDefault(
                                           ( ad ) => ad.UnSwitched );


            for( ;  argnum < args.Length; ++argnum )
            {
                // Already found a switch, now get its value
                if( current != null )
                {
                    AddValue( current, args[ argnum ] );
                    current    = null;
                    currSwitch = null;
                    continue;
                }

                string argToTest = args[ argnum ];

                // In search of a switch, what test should we use?
                Func<string,ArgDef,bool>  test = null;
                if( args[ argnum ].StartsWith( "/" ) )
                {
                    test = etest;
                    argToTest = argToTest.Substring( 1 );
                }
                else if( args[ argnum ].StartsWith( "--" ) )
                {
                    test = ltest;
                    argToTest = argToTest.Substring( 2 );
                }
                else if( args[ argnum ].StartsWith( "-" ) )
                {
                    test = stest;
                    argToTest = argToTest.Substring( 1 );
                }
                

                // Is there a def that matches?
                bool unmatched = true;
                foreach( var def in definitions )
                {
                    if( test( argToTest, def ) )
                    {
                        unmatched  = false;
                        current    = def;
                        currSwitch = args[ argnum ];

                        if( def.ArgKind == ArgDef.Kind.Bool )
                        {
                            // Bools never need a follow-on parse
                            AddValue( def, (object) true );
                            current = null;
                        }

                        break;
                    }

                } // end foreach def

                if( unmatched )
                {
                    if( unswitchedArg != null )
                    {
                        AddValue( unswitchedArg, args[ argnum ] );
                    }
                    else
                    {
                        throw new IndexOutOfRangeException(
                            "An unrecognized switch name or " + 
                            "a value unpaired with a swtich was encountered.  " + 
                            "Use /? to see legal command-line arguments." );
                    }
                    unswitchedArg = null;
                }

            } // end while argnum

            if( current != null )
            {
                throw new ArgumentNullException(
                    "Missing value for parameter " + currSwitch );
            }

            if( !Help )
            {
                CheckMandatorySwitches();
            }

        }


        /// <summary>Gets a value parsed for PropertyName</summary>
        /// <param name="propName">the PropertyName <see cref="ArgDef"/></param>
        /// <remarks>Typically a ProgramArguments derived class wraps indexer
        /// calls in intellisense-enabled, strong-typed properties, e.g.
        /// <see cref="Help"/>.</remarks>
        /// <returns>The parsed value or 
        /// null if unparsed and no DefaultValue defined</returns>
        public ArgValue    this[ string propName ]
        {
            get
            {
                ArgValue  av = null;
                if( !dictionary.TryGetValue( propName, out av ) )
                {
                    av = ArgValue.Empty;
                }
                return av;
            }
        }


        /// <summary>Did the user pass in /? or --help args?</summary>
        public bool  Help
        {
            get { return this["Help"].GetBool(); }
        }


        /// <summary>Did the user pass in --WhatIf args?</summary>
        public bool  WhatIf
        {
            get { return this["WhatIf"].GetBool(); }
        }


        #region Help
        public string   GetHelp( )
        {
            string  descExtrafmt  = "\r\n                      {0}";
            StringBuilder  sb = new StringBuilder();

            if( string.IsNullOrWhiteSpace( HelpSummary ) )
            {
                HelpSummary = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            }

            sb.AppendLine( HelpSummary );
            sb.AppendLine();
            sb.AppendLine( "The following switches are accepted:" );
            sb.AppendLine( CaseSensitive
                ? "(All switches are case-sensitive)"
                : "(All switches are case-insensitive)" );
            sb.AppendLine( "(Valid forms: (short) /X or -X" );
            sb.AppendLine( "              (long)  /XRay or --Xray )" );
            sb.AppendLine();
            sb.AppendLine( "  Short  Long         Description" );
            sb.AppendLine( "  -----  ----         -----------" );

            var helpDefs = from d in definitions
                           orderby d.HelpOrder
                           select d;
            foreach( var def in helpDefs )
            {
                string [] descLines = def.HelpText.Split( 
                                         new string[] { "\r\n" },
                                         StringSplitOptions.None );

                sb.AppendFormat( "  {0,3}   {1,-11}   {2}",
                    (def.ShortSwitch != Unassigned ? def.ShortSwitch : string.Empty),
                    (def.LongSwitch  != Unassigned ? def.LongSwitch  : string.Empty),
                    descLines[ 0 ] );
                foreach( var dlmore in descLines.Skip(1) )
                {
                    sb.AppendFormat( descExtrafmt, dlmore );
                }
                if( def.DefaultValue != null )
                {
                    string more = string.Format( "(default:{0})", def.DefaultValue.ToString() );
                    sb.AppendFormat( descExtrafmt, more );
                }
                if( def.Mandatory )
                {
                    sb.AppendFormat( descExtrafmt, "(Mandatory)" );
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }


        /// <summary>A diagnostic routine to format the parsed results</summary>
        /// <param name="writer">An output writer, eg. Console.Out</param>
        public void Dump( System.IO.TextWriter  writer = null )
        {
            writer = writer ?? Console.Out;
            foreach( var av in dictionary.Values )
            {
                // Displaying '/SetMeansTrue = false' is confusing
                if( av.Definition.ArgKind == ArgDef.Kind.Bool &&
                    av.GetBool() == false )
                {
                    continue;
                }

                string msg = string.Format( "   --{0}\t{1}",
                    av.Definition.LongSwitch,
                    av.Value.ToString() );
                writer.WriteLine( msg );
            }
        }
        #endregion

        #region Internals
        private void AddValue( ArgDef def, object val )
        {
            ArgValue av = new ArgValue() { Definition = def };

            try
            {
                switch( def.ArgKind )
                {
                    case ArgDef.Kind.Bool:
                        av.Value = (bool) Convert.ChangeType( val, typeof(bool) );
                        break;
                    case ArgDef.Kind.Int:
                        av.Value = (int)  Convert.ChangeType( val, typeof(int) );
                        break;
                    case ArgDef.Kind.String:
                        av.Value = (string) val;
                        break;
                }
            }
            catch( Exception ex )
            {
                throw new ArgumentOutOfRangeException(
                    "Could not convert value for argument " + def.PropertyName,
                    ex );
            }

            dictionary[ def.PropertyName ] = av;
        }


        private void AssignDefaults()
        {
            var havingDefaults = from def in definitions
                                 where def.DefaultValue != null
                                 select def;
            foreach( var dd in havingDefaults )
            {
                AddValue( dd, dd.DefaultValue );
            }
        }

        private void ValidateDefinitions( ArgDef def )
        {
            var scmode = SCMode;

            // Check  switch values
            if( string.IsNullOrWhiteSpace( def.ShortSwitch ) && 
                string.IsNullOrWhiteSpace( def.LongSwitch ) )
            {
                throw new ArgumentOutOfRangeException( "ShortSwitch or LongSwitch must be set" );
            }
            if( string.IsNullOrWhiteSpace( def.ShortSwitch ) )
            {
                def.ShortSwitch = Unassigned;
            }
            if( string.IsNullOrWhiteSpace( def.LongSwitch ) )
            {
                def.LongSwitch  = Unassigned;
            }

            // Check duplications
            if( definitions.Any( (d) => 
                  (def.ShortSwitch != Unassigned && 
                   (string.Compare( def.ShortSwitch, d.ShortSwitch, scmode ) == 0 ||
                    string.Compare( def.ShortSwitch, d.LongSwitch,  scmode ) == 0)) ||
                  (def.LongSwitch != Unassigned &&
                   (string.Compare( def.ShortSwitch, d.ShortSwitch, scmode ) == 0 ||
                    string.Compare( def.LongSwitch,  d.LongSwitch,  scmode ) == 0 )) ) )
            {
                string msg = string.Format( "Either {0} or {1} collides with an existing switch",
                    def.ShortSwitch, def.LongSwitch );
                throw new ArgumentOutOfRangeException( msg );
            }
            if( definitions.Any( (d) =>
                string.Compare( def.PropertyName, d.PropertyName, StringComparison.Ordinal ) == 0 ) )
            {
                string msg = string.Format( "PropertyName {0} is already defined", def.PropertyName );
                throw new ArgumentOutOfRangeException( msg );
            }

            // Only one can be 'unswitched', i.e. the final argument's implicit switch
            if( def.UnSwitched && definitions.Any( ( d ) => d.UnSwitched ) )
            {
                throw new ArgumentOutOfRangeException(
                    def.PropertyName + " cannot also be UnSwitched" );
            }

            //if( def.ArgKind == ArgDef.Kind.Bool && def.DefaultValue != null )
            //    throw new ArgumentOutOfRangeException( "Bool properties cannot override DefaultValue" );

            // Infer missing data...
            if( def.DefaultValue == null )
            {
                if( def.ArgKind == ArgDef.Kind.Bool )
                {
                    def.DefaultValue = (object) false;
                }
                else if( def.ArgKind == ArgDef.Kind.Int )
                {
                    def.DefaultValue = (object) 0;
                }
            }

            if( def.HelpText == null )
            {
                def.HelpText = "<no help available>";
            }
        }


        private void CheckMandatorySwitches()
        {
            var mandatories = definitions.Where( ( ad ) => ad.Mandatory );
            var gotten      = dictionary.Values.Select( (v) => v.Definition );
            var test        = mandatories.Except( gotten, (m,g) => (m == g) );

            if( test.Count() > 0 )
            {
                StringBuilder sb = new StringBuilder();
                sb.Append( "Mandatory command-line argument(s) missing:" );
                foreach( var m in test )
                {
                    sb.AppendFormat( " /{0}", m.ShortSwitch );
                }

                Exception ex = new ArgumentNullException( "args", sb.ToString() );
                ex.Data[ "UserInput" ]     = true;
                ExceptionHandler.SetSuppressStack( ex );

                throw ex;
            }
        }


        private StringComparison  SCMode
        {
            get
            {
                return CaseSensitive
                        ? StringComparison.InvariantCulture
                        : StringComparison.InvariantCultureIgnoreCase;
            }
        }
        #endregion
    }
}
