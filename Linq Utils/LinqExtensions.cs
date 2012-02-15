using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace My.Utilities
{
    public static class LinqExtensions
    {
        /// <summary>An Intersect that takes a user-defined equality</summary>
        /// <remarks>
        /// Linq's native Intersect expects a very-non-functional IEqualityComparer
        /// when it should just take a lambda.  This is a crude O(N^2) supplemnt for 
        /// this missing functionality
        /// </remarks>
        /// <param name="left">A sequence to intersect with</param>
        /// <param name="right">A sequence to intersect against</param>
        /// <param name="predicate">A function that returns a tuple of bool and TResult,
        /// the bool should be true if the elements are intersection-worthy.</param>
        /// <returns>A projected sequence that passes the predicate</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures" )]
        public static IEnumerable<TResult>  Intersect<TLeft,TRight,TResult>
                                                ( this IEnumerable<TLeft> left, 
                                                  IEnumerable<TRight>     right,
                                                  Func<TLeft,TRight,Tuple<bool,TResult>>  predicate )
        {
            foreach( var leftElem in left )
            {
                foreach( var rightElem in right )
                {
                    var test = predicate( leftElem, rightElem );
                    if( test.Item1 )
                        yield return test.Item2;
                }
            }
        }


        /// <summary>
        /// Returns a sub-sequence of the 'left' enumeration that 
        /// of only elements that match any in the 'right' enumeration
        /// as judged by a predicate function.  
        /// Linq's native Except does not accept a predicate lambda.  
        /// This is a crude O(n^2) remedy for this missing functionality.
        /// </summary>
        /// <typeparam name="TLeft">The type of element in the first series</typeparam>
        /// <typeparam name="TRight">The type of element in the checked-against series</typeparam>
        /// <param name="left">The original enumeration</param>
        /// <param name="right">The checked-against enumeration</param>
        /// <param name="predicate">The test.</param>
        /// <returns>A sub-sequence from the original left sequence </returns>
        public static IEnumerable<TLeft> Except<TLeft,TRight>(
                                          this IEnumerable<TLeft>    left,
                                          IEnumerable<TRight>        right,
                                          Func<TLeft,TRight,bool>    predicate )
        {
            foreach( var leftElem in left )
            {
                Func<TRight,bool>  curried = (x) => predicate( leftElem, x );

                if( !right.Any( curried ) )
                    yield return leftElem;
            }
        }

    }
}
