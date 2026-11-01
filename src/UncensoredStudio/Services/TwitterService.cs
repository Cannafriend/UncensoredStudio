using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UncensoredStudio.Models;

namespace UncensoredStudio.Services
{
    public class TwitterService
    {
        public const int MaxTweetLength = 280;

        public List<TweetCard> SplitIntoThread(string text)
        {
            var result = new List<TweetCard>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Check if text already has explicit thread separators (e.g. "---" or numbered lines)
            string[] explicitChunks = text.Split(new[] { "\n---\n", "\r\n---\r\n", "---" }, StringSplitOptions.RemoveEmptyEntries);
            if (explicitChunks.Length > 1)
            {
                int idx = 1;
                foreach (var chunk in explicitChunks)
                {
                    string cleaned = chunk.Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        result.Add(new TweetCard
                        {
                            Index = idx++,
                            Text = cleaned
                        });
                    }
                }
                return result;
            }

            // Otherwise, split logically by paragraphs/sentences
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();
            string currentChunk = "";

            foreach (var para in paragraphs)
            {
                if (currentChunk.Length + para.Length + 2 <= 260)
                {
                    currentChunk = string.IsNullOrEmpty(currentChunk) ? para : currentChunk + "\n\n" + para;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentChunk))
                    {
                        chunks.Add(currentChunk);
                        currentChunk = "";
                    }

                    if (para.Length <= 260)
                    {
                        currentChunk = para;
                    }
                    else
                    {
                        // Split long paragraph by sentences
                        var sentences = Regex.Split(para, @"(?<=[.!?])\s+");
                        foreach (var s in sentences)
                        {
                            if (currentChunk.Length + s.Length + 1 <= 260)
                            {
                                currentChunk = string.IsNullOrEmpty(currentChunk) ? s : currentChunk + " " + s;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(currentChunk))
                                {
                                    chunks.Add(currentChunk);
                                }
                                currentChunk = s;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentChunk))
            {
                chunks.Add(currentChunk);
            }

            int total = chunks.Count;
            int cardIndex = 1;
            foreach (var c in chunks)
            {
                string formattedText = total > 1 ? $"{c.Trim()} ({cardIndex}/{total})" : c.Trim();
                result.Add(new TweetCard
                {
                    Index = cardIndex++,
                    Text = formattedText
                });
            }

            return result;
        }

        public string FormatCompleteThreadForCopy(List<TweetCard> cards)
        {
            if (cards == null || cards.Count == 0) return "";
            return string.Join("\n\n---\n\n", cards.Select(c => c.Text));
        }
    }
}
