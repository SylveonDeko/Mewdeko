﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mewdeko.Common.Replacements
{
    public class Replacer
    {
        private readonly IEnumerable<(Regex Regex, Func<Match, string> Replacement)> _regex;
        private readonly IEnumerable<(string Key, Func<string> Text)> _replacements;

        public Replacer(IEnumerable<(string, Func<string>)> replacements,
            IEnumerable<(Regex, Func<Match, string>)> regex)
        {
            _replacements = replacements;
            _regex = regex;
        }

        public SmartText Replace(SmartText data)
        {
            return data switch
            {
                SmartEmbedText embedData => Replace(embedData),
                SmartPlainText plain => Replace(plain),
                _ => throw new ArgumentOutOfRangeException(nameof(data), "Unsupported argument type")
            };
        }

        public SmartPlainText Replace(SmartPlainText plainText)
        {
            return Replace(plainText.Text);
        }

        public SmartEmbedText Replace(SmartEmbedText embedData)
        {
            var newEmbedData = new SmartEmbedText();
            newEmbedData.PlainText = Replace(embedData.PlainText);
            newEmbedData.Description = Replace(embedData.Description);
            newEmbedData.Title = Replace(embedData.Title);
            newEmbedData.Thumbnail = Replace(embedData.Thumbnail);
            newEmbedData.Image = Replace(embedData.Image);
            if (embedData.Author != null)
            {
                newEmbedData.Author = new SmartTextEmbedAuthor();
                newEmbedData.Author.Name = Replace(embedData.Author.Name);
                newEmbedData.Author.IconUrl = Replace(embedData.Author.IconUrl);
            }

            if (embedData.Fields != null)
            {
                var fields = new List<SmartTextEmbedField>();
                foreach (var f in embedData.Fields)
                {
                    var newF = new SmartTextEmbedField();
                    newF.Name = Replace(f.Name);
                    newF.Value = Replace(f.Value);
                    fields.Add(newF);
                }

                newEmbedData.Fields = fields.ToArray();
            }

            if (embedData.Footer != null)
            {
                newEmbedData.Footer = new SmartTextEmbedFooter();
                newEmbedData.Footer.Text = Replace(embedData.Footer.Text);
                newEmbedData.Footer.IconUrl = Replace(embedData.Footer.IconUrl);
            }

            newEmbedData.Color = embedData.Color;

            return newEmbedData;
        }

        public string Replace(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            foreach (var (Key, Text) in _replacements)
                if (input.Contains(Key))
                    input = input.Replace(Key, Text(), StringComparison.InvariantCulture);

            foreach (var item in _regex) input = item.Regex.Replace(input, m => item.Replacement(m));

            return input;
        }

        public void Replace(CREmbed embedData)
        {
            embedData.PlainText = Replace(embedData.PlainText);
            embedData.Description = Replace(embedData.Description);
            embedData.Title = Replace(embedData.Title);
            embedData.Thumbnail = Replace(embedData.Thumbnail);
            embedData.Image = Replace(embedData.Image);
            if (embedData.Author != null)
            {
                embedData.Author.Name = Replace(embedData.Author.Name);
                embedData.Author.IconUrl = Replace(embedData.Author.IconUrl);
            }

            if (embedData.Fields != null)
                foreach (var f in embedData.Fields)
                {
                    f.Name = Replace(f.Name);
                    f.Value = Replace(f.Value);
                }

            if (embedData.Footer != null)
            {
                embedData.Footer.Text = Replace(embedData.Footer.Text);
                embedData.Footer.IconUrl = Replace(embedData.Footer.IconUrl);
            }
        }
    }
}