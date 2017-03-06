namespace UserGroupPermissions.ExtensionMethods
{

    // Namespaces.
    using System.Collections.Generic;


    /// <summary>
    /// Extension methods for collections of strings.
    /// </summary>
    public static class StringCollectionExtensions
    {

        #region Extension Methods

        /// <summary>
        /// Combines a collection of strings with the specified separator.
        /// </summary>
        /// <param name="values">
        /// The string values to combine.
        /// </param>
        /// <param name="separator">
        /// The separator to place between each string value.
        /// </param>
        /// <returns>
        /// The combined string.
        /// </returns>
        public static string JoinStrings(this IEnumerable<string> values, string separator)
        {
            return string.Join(separator, values);
        }

        #endregion

    }

}