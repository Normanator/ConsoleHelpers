using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace My.Utilities
{
    public static class DataBindOnce
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures" )]
        public static void SetProperties(
            object                             target,
            IEnumerable<Tuple<string,object>>  namedProperties )
        {
            var targetType  = target.GetType();
            var propSetters = targetType.GetProperties( );

            foreach( var np in namedProperties )
            {
                var propSet =
                    (from ps in propSetters
                     where ps.CanWrite && 
                           string.CompareOrdinal( ps.Name, np.Item1 ) == 0
                     select ps).SingleOrDefault();

                if( propSet != null )
                {
                    propSet.SetValue( target, np.Item2, null );
                }
            }
        }
    }
}
