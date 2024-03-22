using Discord.Commands;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    /// Type reader collection that aggregates multiple type readers and attempts to parse input using each of them.
    /// </summary>
    public class TypeReaderCollection : TypeReader
    {
        private readonly IEnumerable<TypeReader> readers;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeReaderCollection"/> class.
        /// </summary>
        /// <param name="readers">The collection of type readers to aggregate.</param>
        public TypeReaderCollection(IEnumerable<TypeReader> readers) => this.readers = readers;

        /// <inheritdoc />
        public override async Task<TypeReaderResult> ReadAsync(
            ICommandContext context, string input, IServiceProvider services)
        {
            var success = new List<TypeReaderValue>(); // List to store successfully parsed values
            var errors = new List<TypeReaderResult>(); // List to store parsing errors

            // Iterate through each type reader in the collection
            foreach (var reader in readers)
            {
                // Attempt to parse input using the current type reader
                var result = await reader.ReadAsync(context, input, services).ConfigureAwait(false);
                if (result.Error is not null)
                {
                    errors.Add(result); // If parsing fails, add the error to the errors list
                }
                else
                {
                    success.AddRange(result.Values); // If parsing succeeds, add the parsed values to the success list
                }
            }

            // Determine the final result based on the success and errors lists
            return success.Count == 0 && errors.Count > 0
                ? errors.First() // If there are only errors, return the first error encountered
                : TypeReaderResult.FromSuccess(success); // Otherwise, return success with the parsed values
        }
    }
}