using System;

namespace Mewdeko.Common.Yml
{
    public class CommentAttribute : Attribute
    {
        public string Comment { get; }

        public CommentAttribute(string comment)
        {
            Comment = comment;
        }
    }
}